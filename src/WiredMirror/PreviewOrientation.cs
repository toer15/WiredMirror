namespace WiredMirror;

internal enum PreviewOrientation { FollowPhone, Portrait, Landscape, LandscapeReverse }

internal static class OrientationPolicy
{
    public static int Turns(PreviewOrientation mode, int width, int height) => mode switch
    {
        PreviewOrientation.Portrait => width > height ? 3 : 0,
        PreviewOrientation.Landscape => height > width ? 1 : 0,
        PreviewOrientation.LandscapeReverse => height > width ? 3 : 2,
        _ => 0
    };

    public static Rectangle Fit(Size canvas, Size source, int turns)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0 || source.Width <= 0 || source.Height <= 0) return Rectangle.Empty;
        var width = (turns & 1) != 0 ? source.Height : source.Width;
        var height = (turns & 1) != 0 ? source.Width : source.Height;
        var scale = Math.Min((double)canvas.Width / width, (double)canvas.Height / height);
        var fitted = new Size(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
        return new Rectangle((canvas.Width - fitted.Width) / 2, (canvas.Height - fitted.Height) / 2, fitted.Width, fitted.Height);
    }

    public static void Draw(Graphics graphics, Size canvas, Bitmap frame, PreviewOrientation mode)
    {
        var turns = Turns(mode, frame.Width, frame.Height);
        var rectangle = Fit(canvas, frame.Size, turns);
        if (rectangle.IsEmpty) return;
        var state = graphics.Save();
        graphics.TranslateTransform(rectangle.Left + rectangle.Width / 2f, rectangle.Top + rectangle.Height / 2f);
        graphics.RotateTransform(turns * 90);
        var width = (turns & 1) != 0 ? rectangle.Height : rectangle.Width;
        var height = (turns & 1) != 0 ? rectangle.Width : rectangle.Height;
        graphics.DrawImage(frame, new RectangleF(-width / 2f, -height / 2f, width, height));
        graphics.Restore(state);
    }
}

// Reuses two BGRA buffers and reads only the latest native frame. No frame queue.
// Follow-phone mode retains the native NV12/D3D renderer; forced rotation uses this
// path because API 18's window-rotation export accepts only zero/180 degrees.
internal sealed class OrientedPreview : Control
{
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    private Bitmap? front, spare;
    private long timestamp;
    private bool busy, stopping;
    private Task? pendingRead;
    private PreviewOrientation orientation;
    private UsbEngine? engine;

    public OrientedPreview()
    {
        Dock = DockStyle.Fill;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        timer.Tick += (_, _) => { if (!busy) pendingRead = ReadFrame(); };
    }

    public void Configure(UsbEngine? source, PreviewOrientation mode, bool active)
    {
        if (engine != source || orientation != mode || !active) timestamp = 0;
        engine = source;
        orientation = mode;
        Visible = active && mode != PreviewOrientation.FollowPhone;
        timer.Enabled = Visible && !stopping;
        if (!active && !busy) { front?.Dispose(); front = null; spare?.Dispose(); spare = null; }
        if (Visible) BringToFront();
        Invalidate();
    }

    private async Task ReadFrame()
    {
        if (engine == null || !Visible || stopping || busy) return;
        busy = true;
        var source = engine;
        var last = timestamp;
        var mode = orientation;
        // Limit conversion to the on-screen size, capped at 1920 pixels per edge.
        var edge = (uint)Math.Clamp(Math.Max(ClientSize.Width, ClientSize.Height), 64, 1920);
        Bitmap? next = spare;
        spare = null;
        try
        {
            var result = await Task.Run(() => source.ReadPreview(ref next, last, edge));
            if (stopping || engine != source || orientation != mode || !Visible) { next?.Dispose(); return; }
            if (result != 0 && next != null)
            {
                spare = front; front = next; timestamp = result;
                Invalidate();
            }
            else spare = next;
        }
        catch (Exception error) { next?.Dispose(); System.Diagnostics.Debug.WriteLine(error); }
        finally { busy = false; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
        if (front == null) return;
        OrientationPolicy.Draw(e.Graphics, ClientSize, front, orientation);
    }

    public async Task Shutdown()
    {
        stopping = true; timer.Stop();
        if (pendingRead != null) await pendingRead;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Dispose(); front?.Dispose(); spare?.Dispose(); }
        base.Dispose(disposing);
    }
}
