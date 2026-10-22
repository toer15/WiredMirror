namespace WiredMirror;

internal sealed class UsbSetupWindow : Form
{
    private readonly string? serial;
    private readonly Label checks = new() { Dock = DockStyle.Fill, Padding = new Padding(14), Text = "Checking installed USB components..." };
    private readonly TextBox log = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 25 };
    private readonly Button install = new() { Text = "Install needed components", AutoSize = true, Enabled = false, Padding = new Padding(8) };
    private readonly Button done = new() { Text = "Done", AutoSize = true, Padding = new Padding(8) };
    private bool working;
    internal bool InstalledSuccessfully { get; private set; }
    internal bool RequiresReplug { get; private set; }

    public UsbSetupWindow(string? selectedKey)
    {
        serial = selectedKey is { Length: >= 8 and <= 40 } && selectedKey.All(char.IsAsciiLetterOrDigit) ? selectedKey : null;
        Text = "WiredMirror — USB setup";
        ClientSize = new Size(700, 480);
        MinimumSize = new Size(640, 440);
        Font = new Font("Segoe UI", 10);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(10) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.Controls.Add(checks, 0, 0);
        layout.Controls.Add(log, 0, 1);
        layout.Controls.Add(progress, 0, 2);
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(4), Text =
            "Installs Apple USB support and the selected iPhone's capture driver. Windows may request administrator approval. Unlock/Trust and unplug/replug may still be needed. ADB is for Android." }, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill };
        actions.Controls.AddRange([install, done]);
        layout.Controls.Add(actions, 0, 4);
        Controls.Add(layout);
        install.Click += async (_, _) => await Run(true);
        done.Click += (_, _) => Close();
        Shown += async (_, _) => await Run(false);
        FormClosing += (_, args) =>
        {
            if (!working) return;
            args.Cancel = true;
            log.AppendText("\r\nSetup is still working. Complete or decline the Windows prompt before closing.\r\n");
        };
    }

    private async Task Run(bool installing)
    {
        if (working) return;
        working = true;
        install.Enabled = done.Enabled = false;
        progress.Style = ProgressBarStyle.Marquee;
        try
        {
            var update = new Progress<string>(message =>
            {
                if (log.TextLength > 25000) log.Clear();
                log.AppendText(message + "\r\n");
            });
            var result = await UsbSetupClient.Run(installing, serial, update);
            if (result.State != null)
            {
                var state = result.State;
                checks.Text = $"Apple USB driver: {(state.UsbDriverInstalled ? "installed" : "missing")}\n" +
                    $"Apple pairing service: {(state.ServiceRunning ? "running" : state.ServiceInstalled ? "stopped" : "missing")}\n" +
                    (serial == null ? "Capture driver: connect and select a USB iPhone first."
                        : $"Chosen phone …{DeviceIdentity.Suffix(serial)} capture driver: {(state.CaptureReady(serial) ? "installed" : "setup / reconnect needed")}");
            }
            else checks.Text = "USB setup could not verify the installed components.";
            if (result.Message.Length > 0) log.AppendText(result.Message + "\r\n");
            if (installing)
            {
                InstalledSuccessfully = result.Success;
                RequiresReplug = result.RequiresReplug;
                if (result.RequiresReplug) log.AppendText("Unplug and reconnect the same phone, then run setup again if capture support is still missing.\r\n");
            }
        }
        catch (Exception error) { checks.Text = "USB setup needs attention."; log.AppendText(error.Message + "\r\n"); }
        finally
        {
            working = false;
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 100;
            install.Enabled = done.Enabled = true;
        }
    }
}
