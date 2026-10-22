using System.Diagnostics;
using System.Text.Json;

namespace WiredMirror;

internal sealed class MirrorWindow : Form
{
    private readonly ListView phones = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false,
        HideSelection = false, ShowItemToolTips = true, BackColor = Color.FromArgb(28, 35, 47),
        ForeColor = Color.White, BorderStyle = BorderStyle.None
    };
    private readonly Button refresh = MakeButton("Scan now");
    private readonly Button connect = MakeButton("Mirror selected phone");
    private readonly Button stop = MakeButton("Stop");
    private readonly Button fullscreen = MakeButton("Fullscreen / TV");
    private readonly Button setup = MakeButton("USB setup");
    private readonly Button diagnostics = MakeButton("Save diagnostics");
    private readonly Button updates = MakeButton("Check for updates");
    private readonly Button networkSetup = MakeButton("AirPlay setup");
    private readonly Button restartAirPlay = MakeButton("Restart AirPlay");
    private readonly ComboBox orientation = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Preview orientation" };
    private readonly OrientedPreview orientedPreview = new();
    private AppPreferences preferences = AppPreferences.Load();
    private PreviewOrientation Orientation => (PreviewOrientation)orientation.SelectedIndex;
    private readonly ComboBox connectionType = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Connection type" };
    private readonly ComboBox devicePicker = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, DropDownWidth = 700, AccessibleName = "Choose device" };
    private readonly CheckBox audio = new() { Text = "Sound", Checked = true, AutoSize = true, Padding = new Padding(6) };
    private readonly CheckBox reconnect = new() { Text = "Reconnect the chosen phone", Checked = true, AutoSize = true, Padding = new Padding(4) };
    private readonly ComboBox quality = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label status = new() { Text = "Checking USB support...", Dock = DockStyle.Fill, Padding = new Padding(14, 8, 14, 0) };
    private readonly TextBox details = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true,
        ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(20, 25, 35), ForeColor = Color.White };
    private readonly Panel preview = new() { BackColor = Color.Black, Dock = DockStyle.Fill };
    private readonly Panel previewArea = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly Label overlay = new()
    {
        Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Padding = new Padding(25),
        ForeColor = Color.FromArgb(199, 210, 226), Font = new Font("Segoe UI", 15),
        Text = "Connect your iPhone with a USB data cable.\n\nUnlock it and tap Trust This Computer.\nChoose the exact phone on the left, then Mirror selected phone."
    };
    private readonly TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
    private readonly FlowLayoutPanel toolbar = new() { Dock = DockStyle.Fill, Padding = new Padding(8), WrapContents = true };
    private readonly SplitContainer body = new() { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1, SplitterWidth = 6 };
    private readonly Label footer = new()
    {
        Dock = DockStyle.Fill, Padding = new Padding(14, 6, 14, 4),
        Text = "TV: move this window to the PC-connected TV, then F11. Esc exits fullscreen. Control apps on the iPhone.\nVideo uses the phone's capture output. Zero delay and TikTok compatibility cannot be guaranteed."
    };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 500 };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private UsbEngine? engine;
    private MirrorSession? session;
    private DiscoverySnapshot? discovery;
    private bool userBusy, closing, closed, isFullscreen, updatingList, hotplugPending, previewPending, fullscreenPending;
    private Rectangle savedBounds;
    private FormWindowState savedWindowState;
    private double lastScan = -10, lastMetadata = -10, lastFrameAt, lastSampleAt;
    private ulong lastFrames;
    private string? lastFailure;
    private PendingConnection? pending;
    private ConnectionKind activeConnection = ConnectionKind.Usb;
    private VideoProfile Profile => quality.SelectedItem as VideoProfile ?? VideoProfile.Native;
    private ConnectionKind Connection => activeConnection;

    public MirrorWindow()
    {
        Text = $"WiredMirror {UpdateClient.Current.ToString(3)} — iPhone screen receiver";
        ClientSize = new Size(1240, 780);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(20, 25, 35);
        ForeColor = Color.FromArgb(235, 240, 248);
        Font = new Font("Segoe UI", 10);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        toolbar.Controls.AddRange([connect, stop, audio, fullscreen, setup, diagnostics, updates]);
        phones.Columns.Add("Device", 130);
        phones.Columns.Add("ID ends", 66);
        phones.Columns.Add("Status", 145);
        quality.Items.AddRange([VideoProfile.Native, VideoProfile.Smooth, VideoProfile.FullHd, VideoProfile.Compatible]);
        quality.SelectedIndex = 0;
        orientation.Items.AddRange(["Orientation: follow phone", "Portrait / upright", "Landscape / clockwise", "Landscape / counterclockwise"]);
        orientation.SelectedIndex = (int)preferences.Orientation;
        connectionType.Items.AddRange(["USB · screen mirroring", "Wi-Fi / AirPlay · screen mirroring", "Bluetooth · discovery / pairing only"]);
        connectionType.SelectedIndex = 0;

        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 11, Padding = new Padding(8) };
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        sidebar.Controls.Add(connectionType, 0, 0);
        sidebar.Controls.Add(devicePicker, 0, 1);
        sidebar.Controls.Add(new Label { Text = "Detected devices · choose one above", Dock = DockStyle.Fill, Padding = new Padding(4) }, 0, 2);
        sidebar.Controls.Add(phones, 0, 3);
        sidebar.Controls.Add(details, 0, 4);
        sidebar.Controls.Add(new Label { Text = "Preview / decoder", Dock = DockStyle.Fill }, 0, 5);
        sidebar.Controls.Add(quality, 0, 6);
        sidebar.Controls.Add(orientation, 0, 7);
        var networkButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        networkButtons.Controls.AddRange([networkSetup, restartAirPlay]);
        sidebar.Controls.Add(networkButtons, 0, 8);
        sidebar.Controls.Add(reconnect, 0, 9);
        sidebar.Controls.Add(refresh, 0, 10);
        previewArea.Controls.Add(preview);
        previewArea.Controls.Add(orientedPreview);
        previewArea.Controls.Add(overlay);
        overlay.BringToFront();
        body.Panel1.Controls.Add(sidebar);
        body.Panel2.Controls.Add(previewArea);
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(status, 0, 1);
        layout.Controls.Add(body, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        body.SplitterDistance = 365;
        body.Panel1MinSize = 350;
        body.Panel2MinSize = 400;

        refresh.Click += async (_, _) => await Run(() => Scan(true), true);
        connect.Click += async (_, _) => await Run(ConnectSelected, true);
        stop.Click += async (_, _) => await Run(StopCapture, true);
        phones.SelectedIndexChanged += async (_, _) =>
        {
            if (updatingList || userBusy || session == null || phones.SelectedItems.Count == 0) return;
            var choice = (PhoneDevice)phones.SelectedItems[0].Tag!;
            await Run(() => { Choose(choice.Key); return Task.CompletedTask; }, true);
        };
        devicePicker.SelectionChangeCommitted += async (_, _) =>
        {
            if (updatingList || userBusy || session == null || devicePicker.SelectedItem is not PhoneDevice choice) return;
            await Run(() => { Choose(choice.Key); return Task.CompletedTask; }, true);
        };
        connectionType.SelectionChangeCommitted += async (_, _) =>
        {
            var requested = connectionType.SelectedIndex switch { 1 => ConnectionKind.AirPlay, 2 => ConnectionKind.Bluetooth, _ => ConnectionKind.Usb };
            await Run(async () =>
            {
                if (session == null || engine == null) return;
                pending = null;
                await Task.Run(session.Stop);
                session.Select(null);
                activeConnection = requested;
                if (Connection == ConnectionKind.AirPlay) await Task.Run(engine.StartWireless);
                else await Task.Run(engine.StopWireless);
                Cover(ConnectionInstructions());
                await Scan(true);
            }, true);
        };
        audio.CheckedChanged += async (_, _) =>
        {
            if (session == null) return;
            var enabled = audio.Checked;
            await Run(async () =>
            {
                session.RememberPlayback(Profile, enabled);
                if (pending != null) pending = pending with { Audio = enabled };
                if (session.IsActive) await Task.Run(() => engine!.Audio(enabled));
            }, true);
        };
        quality.SelectedIndexChanged += async (_, _) =>
        {
            if (session == null) return;
            var profile = Profile;
            await Run(async () =>
            {
                session.RememberPlayback(profile, audio.Checked);
                if (pending != null) pending = pending with { Profile = profile };
                if (session.IsActive) await Task.Run(() => engine!.ApplyProfile(profile));
            }, true);
        };
        reconnect.CheckedChanged += async (_, _) =>
        {
            var enabled = reconnect.Checked;
            if (session != null) await Run(() => { session.ReconnectEnabled = enabled; return Task.CompletedTask; }, true);
        };
        fullscreen.Click += (_, _) => ToggleFullscreen();
        setup.Click += async (_, _) => await Run(OpenSetup, true);
        diagnostics.Click += (_, _) => SaveDiagnostics();
        networkSetup.Click += (_, _) => { using var window = new AirPlaySetup(); window.ShowDialog(this); };
        restartAirPlay.Click += async (_, _) => await Run(async () =>
        {
            if (engine == null || session == null) return;
            await StopCapture();
            Cover("Restarting AirPlay...\n\nOn iPhone, select WiredMirror in Screen Mirroring after the receiver is ready.");
            await Task.Run(() => { engine.StopWireless(); engine.StartWireless(); });
            lastFailure = null;
            Cover(ConnectionInstructions());
            await Scan(true);
        }, true);
        orientation.SelectionChangeCommitted += async (_, _) => await Run(async () =>
        {
            preferences = preferences with { Orientation = Orientation }; preferences.Save();
            var follow = Orientation == PreviewOrientation.FollowPhone;
            if (engine != null) await Task.Run(() => engine.UseNativePreview(follow));
            UpdatePreview();
        }, true);
        updates.Click += async (_, _) => await CheckUpdates();
        preview.HandleCreated += (_, _) => previewPending = true;
        KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.F11 || (args.KeyCode == Keys.Escape && isFullscreen))
            { ToggleFullscreen(); args.Handled = true; args.SuppressKeyPress = true; }
        };
        Shown += async (_, _) => await Run(async () =>
        {
            engine = await Task.Run(() => new UsbEngine());
            engine.PreparePreview(preview.Handle);
            engine.UseNativePreview(Orientation == PreviewOrientation.FollowPhone);
            session = new MirrorSession(engine) { ReconnectEnabled = reconnect.Checked };
            await Scan(true);
            timer.Start();
            _ = CheckUpdatesInBackground();
        }, true);
        timer.Tick += async (_, _) => await Run(Tick);
        FormClosing += OnClosing;
        Cover(ConnectionInstructions());
        UpdateControls();
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0219) hotplugPending = true;
        base.WndProc(ref message);
    }

    private static Button MakeButton(string text) => new()
    {
        Text = text, AutoSize = true, Height = 34, Padding = new Padding(7, 2, 7, 2),
        BackColor = Color.FromArgb(47, 61, 82), ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat, Margin = new Padding(4)
    };

    private string ConnectionInstructions() => Connection switch
    {
        ConnectionKind.AirPlay => "Wireless screen mirroring\n\nUse the same Wi-Fi network.\nOn iPhone: Control Centre → Screen Mirroring → WiredMirror.\nThen choose that sender here and press Connect selected.",
        ConnectionKind.Bluetooth => "Bluetooth discovery and pairing\n\nOpen iPhone Settings → Bluetooth to make it discoverable.\nBluetooth alone does not provide its screen.\nFor the centre preview, choose USB or Wi-Fi / AirPlay.",
        _ => "Connect your iPhone with a USB data cable.\n\nChoose it from the device dropdown.\nRun USB setup if needed, then Connect selected.\nUnlock the phone and approve Trust when asked."
    };

    private void Choose(string key)
    {
        session!.Select(key);
        if (pending?.Key != key) pending = null;
        updatingList = true;
        try
        {
            devicePicker.SelectedItem = session.Selected;
            foreach (ListViewItem item in phones.Items) item.Selected = ((PhoneDevice)item.Tag!).Key == key;
        }
        finally { updatingList = false; }
    }

    private void UpdateControls()
    {
        var selected = session?.Selected;
        var running = session?.Running;
        var active = running != null;
        connect.Enabled = engine != null && !userBusy && selected != null && session?.CleanupRequired != true
            && (!active || running!.Key != selected.Key);
        connect.Text = Connection == ConnectionKind.Bluetooth ? "Pair in Windows" : active && selected?.Key != running!.Key ? "Switch to selected phone" : "Connect selected";
        stop.Enabled = !userBusy && (active || pending != null || session?.WaitingForKey != null || session?.CleanupRequired == true);
        refresh.Enabled = engine != null && !userBusy;
        phones.Enabled = !userBusy;
        connectionType.Enabled = devicePicker.Enabled = !userBusy;
        audio.Enabled = quality.Enabled = reconnect.Enabled = fullscreen.Enabled = !userBusy;
        setup.Enabled = Connection == ConnectionKind.Usb && !userBusy && !active && session?.CleanupRequired != true;
        diagnostics.Enabled = !userBusy;
        orientation.Enabled = !userBusy;
        networkSetup.Enabled = restartAirPlay.Enabled = engine != null && !userBusy && Connection == ConnectionKind.AirPlay;
        details.Text = (selected != null
            ? $"Selected: {selected.Name}\nID ends: {selected.ShortId}" +
              (string.IsNullOrEmpty(selected.Model) ? "" : $" · {selected.Model} · iOS {selected.Ios}") +
              $"\n{selected.ReadinessText}\n\n{selected.Guidance}"
            : session?.SelectedKey != null
                ? $"Chosen phone …{DeviceIdentity.Suffix(session.SelectedKey)} is disconnected.\n\nChoose another phone explicitly to change the source."
                : "Choose a phone above.\n\nNames can be identical; use the ID ending to distinguish devices. No phone is chosen automatically.").Replace("\n", Environment.NewLine);
    }

    private async Task Run(Func<Task> work, bool userAction = false)
    {
        if (closing || (userAction && userBusy)) return;
        if (userAction)
        {
            userBusy = true;
            lastFailure = null;
            UpdateControls();
            await gate.WaitAsync();
        }
        else if (!gate.Wait(0)) return;
        if (closing) { gate.Release(); return; }
        try { await work(); }
        catch (Exception error)
        {
            lastFailure = error.Message;
            SetStatus(error.Message, true);
            if (session?.IsActive != true) Cover("Connection needs attention\n\n" + error.Message);
        }
        finally
        {
            gate.Release();
            if (userAction) userBusy = false;
            if (!closing)
            {
                UpdateControls();
                if (!userBusy && fullscreenPending) { fullscreenPending = false; ToggleFullscreen(); }
            }
        }
    }

    private async Task Scan(bool metadata)
    {
        if (engine == null || session == null) return;
        hotplugPending = false;
        var mode = Connection;
        discovery = await Task.Run(() => mode switch
        {
            ConnectionKind.AirPlay => engine.DiscoverWireless(),
            ConnectionKind.Bluetooth => WindowsBluetoothDiscovery.Scan(metadata),
            _ => engine.Discover(metadata)
        });
        discovery = discovery with { Devices = DeviceCatalog.WithActiveSession(discovery.Devices, session.Running) };
        lastScan = clock.Elapsed.TotalSeconds;
        if (metadata) lastMetadata = lastScan;
        session.UpdateDevices(discovery.Devices);
        updatingList = true;
        phones.BeginUpdate();
        try
        {
            var current = phones.Items.Cast<ListViewItem>().Select(item => (PhoneDevice)item.Tag!).ToArray();
            if (!current.SequenceEqual(discovery.Devices))
            {
                phones.Items.Clear();
                devicePicker.Items.Clear();
                foreach (var device in discovery.Devices)
                {
                    devicePicker.Items.Add(device);
                    var item = new ListViewItem([device.Name, device.ShortId, device.ReadinessText])
                    { Tag = device, ToolTipText = $"{device.Name}\nID ends: {device.ShortId}\n{device.Guidance}" };
                    phones.Items.Add(item);
                    item.Selected = device.Key == session.SelectedKey;
                    if (device.Key == session.SelectedKey) devicePicker.SelectedItem = device;
                }
            }
        }
        finally { phones.EndUpdate(); updatingList = false; }
        if (!session.IsActive)
        {
            if (session.WaitingForKey != null)
                SetStatus($"Waiting up to 60 seconds for the chosen phone …{DeviceIdentity.Suffix(session.WaitingForKey)}. Other phones will not be used.");
            else if (lastFailure == null && pending == null) SetStatus(discovery.Summary + (discovery.Warning == null ? "" : " · " + discovery.Warning));
        }
        UpdateControls();
    }

    private async Task ConnectSelected()
    {
        if (session == null || engine == null) return;
        var requestedKey = session.SelectedKey;
        await Scan(true);
        if (requestedKey == null || session.Selected?.Key != requestedKey) throw new InvalidOperationException("The selected phone is no longer available. Choose a connected phone.");
        if (session.Selected.Connection == ConnectionKind.Bluetooth)
        {
            var device = session.Selected;
            var owner = Handle;
            await Task.Run(() => WindowsBluetoothDiscovery.Pair(device, owner));
            Cover("Bluetooth pairing completed\n\nFor screen viewing, choose USB or Wi-Fi / AirPlay.\nPairing does not provide an iPhone screen stream.");
            SetStatus("Bluetooth pairing completed. Choose USB or Wi-Fi / AirPlay for screen mirroring.");
            return;
        }
        var profile = Profile;
        var withAudio = audio.Checked;
        if (!session.Selected.CanConnect)
        {
            if (session.IsActive) await Task.Run(session.Stop);
            pending = PendingConnection.Request(session.Selected, profile, withAudio, DateTime.UtcNow);
            Cover("Waiting for the chosen phone\nID ends: " + session.Selected.ShortId + "\n\n" + session.Selected.Guidance + "\n\nThe preview starts automatically when this phone is ready.");
            SetStatus("Waiting up to two minutes for the selected phone's USB setup / unlock / Trust. Stop cancels this request.");
            return;
        }
        pending = null;
        Cover($"Connecting to {session.Selected.Name}\nID ends: {session.Selected.ShortId}\n\nKeep this phone unlocked.");
        SetStatus("Opening the selected phone's screen stream...");
        await Task.Run(() => session.StartSelected(profile, withAudio));
        ResetMeasurements();
    }

    private async Task StopCapture()
    {
        pending = null;
        Cover("Mirroring stopped\n\nChoose a phone and press Mirror selected phone to start again.");
        if (session != null) await Task.Run(session.Stop);
        SetStatus("Stopped. No phone is being mirrored.");
    }

    private void ResetMeasurements()
    { lastFrames = 0; lastFrameAt = lastSampleAt = clock.Elapsed.TotalSeconds; }

    private async Task Tick()
    {
        if (session == null || engine == null) return;
        if (previewPending)
        {
            previewPending = false;
            var window = preview.Handle;
            await Task.Run(() => engine.PreparePreview(window));
        }
        if (session.IsActive)
        {
            var capture = await Task.Run(() => session.Poll(DateTime.UtcNow));
            if (!session.IsActive)
            {
                var ended = string.IsNullOrWhiteSpace(capture.Message) ? "Phone disconnected or capture ended." : capture.Message;
                Cover(ended + (session.WaitingForKey != null ? "\n\nWaiting for the same phone to reconnect..." : "\n\nChoose the phone and connect again."));
                lastFailure = ended;
                SetStatus(ended, true);
                hotplugPending = true;
            }
            else if (capture.VideoFrames > 0)
            {
                overlay.Visible = false;
                UpdatePreview();
                var now = clock.Elapsed.TotalSeconds;
                var delta = now - lastSampleAt;
                var fps = delta > 0 && capture.VideoFrames >= lastFrames ? (capture.VideoFrames - lastFrames) / delta : 0;
                if (capture.VideoFrames != lastFrames) lastFrameAt = now;
                lastFrames = capture.VideoFrames;
                lastSampleAt = now;
                var output = await Task.Run(engine.OutputStatus);
                SetStatus($"Mirroring: {session.Running!.Name} · ID …{session.Running.ShortId} · {capture.Width} × {capture.Height}\n" +
                    $"Incoming video {fps:0} fps · {output.DecoderText} decoder · Decode {capture.LatencyMs:0.0} ms (PC only) · Audio packets {capture.AudioPackets}" +
                    (now - lastFrameAt >= 5 ? " · No new frames; the phone may be still or stalled." : ""));
            }
            else if (clock.Elapsed.TotalSeconds - lastFrameAt >= 45)
            {
                await Task.Run(session.Stop);
                lastFailure = Connection == ConnectionKind.AirPlay
                    ? "No AirPlay video arrived within 45 seconds. Stop Screen Mirroring on the iPhone, Restart AirPlay, then select WiredMirror again. Check AirPlay setup if needed."
                    : "No video arrived within 45 seconds. Unlock the phone, check USB setup, then connect again.";
                Cover("No video received\n\n" + lastFailure);
                SetStatus(lastFailure, true);
            }
            else
            {
                var stage = capture.State switch { 1 => "Activating USB", 2 => "Waiting for the device", 3 => "Negotiating video", _ => "Waiting for the first frame" };
                SetStatus($"{stage} · Chosen phone ID …{session.Running!.ShortId}. Keep it unlocked.\n{capture.Message}");
            }
        }
        var elapsed = clock.Elapsed.TotalSeconds;
        if (hotplugPending || elapsed - lastScan >= 3)
            await Scan(!session.IsActive && (hotplugPending || pending != null || elapsed - lastMetadata >= 10));
        if (pending is { } request)
        {
            if (!request.IsCurrent(session.SelectedKey, DateTime.UtcNow))
            {
                pending = null;
                Cover("Connection request ended. Choose a phone and press Connect selected to try again.");
            }
            else if (request.Ready(session.SelectedKey, session.Devices, DateTime.UtcNow) != null)
            {
                pending = null;
                Cover("Starting the chosen phone's screen...\nID ends: " + DeviceIdentity.Suffix(request.Key));
                await Task.Run(() => session.StartSelected(request.Profile, request.Audio));
                ResetMeasurements();
            }
        }
        if (session.WaitingForKey != null)
        {
            var reconnected = await Task.Run(() => session.TryReconnect(DateTime.UtcNow));
            if (reconnected)
            {
                Cover($"Reconnecting to the chosen phone\nID ends: {session.Running!.ShortId}");
                lastFailure = null;
                ResetMeasurements();
            }
            else if (session.WaitingForKey == null)
            {
                lastFailure = "Automatic reconnection ended. Choose a connected phone and press Mirror selected phone to try again.";
                Cover(lastFailure);
                SetStatus(lastFailure, true);
            }
        }
    }

    private void Cover(string text) { orientedPreview.Configure(engine, Orientation, false); overlay.Text = text; overlay.Visible = true; overlay.BringToFront(); }
    private void UpdatePreview()
    {
        var visible = session?.IsActive == true && lastFrames > 0 && !overlay.Visible;
        orientedPreview.Configure(engine, Orientation, visible);
        if (visible && Orientation == PreviewOrientation.FollowPhone) preview.BringToFront();
    }

    private async Task CheckUpdatesInBackground()
    {
        if (!preferences.CheckUpdates) return;
        try
        {
            var release = await UpdateClient.Latest(CancellationToken.None);
            if (!closing && release != null) updates.Text = "Update available: " + release.Version.ToString(3);
        }
        catch (Exception error) { Debug.WriteLine(error); } // A startup check never interrupts capture.
    }

    private async Task CheckUpdates()
    {
        updates.Enabled = false;
        try
        {
            using var window = new UpdatesWindow();
            if (window.ShowDialog(this) == DialogResult.OK && window.Downloaded is { } downloaded)
                await Run(async () =>
                {
                    await StopCapture();
                    if (engine != null) await Task.Run(engine.StopWireless);
                    UpdateClient.LaunchInstaller(downloaded);
                    Close();
                }, true);
        }
        finally { if (!closing) updates.Enabled = true; }
    }
    private void SetStatus(string text, bool error = false)
    { status.Text = text; status.ForeColor = error ? Color.FromArgb(255, 199, 136) : ForeColor; }

    private void ToggleFullscreen()
    {
        if (userBusy) { fullscreenPending = !fullscreenPending; return; }
        if (!isFullscreen)
        {
            savedBounds = Bounds;
            savedWindowState = WindowState;
            var display = Screen.FromControl(this);
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            toolbar.Visible = status.Visible = footer.Visible = false;
            body.Panel1Collapsed = true;
            layout.RowStyles[0].Height = layout.RowStyles[1].Height = layout.RowStyles[3].Height = 0;
            Bounds = display.Bounds;
            isFullscreen = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            toolbar.Visible = status.Visible = footer.Visible = true;
            body.Panel1Collapsed = false;
            layout.RowStyles[0].Height = 60;
            layout.RowStyles[1].Height = 70;
            layout.RowStyles[3].Height = 62;
            Bounds = savedBounds;
            WindowState = savedWindowState;
            isFullscreen = false;
        }
        previewPending = true;
    }

    private async Task OpenSetup()
    {
        var chosen = session?.Selected;
        using var window = new UsbSetupWindow(chosen?.Connection == ConnectionKind.Usb ? chosen.Key : null);
        window.ShowDialog(this);
        await Scan(true);
        if (window.InstalledSuccessfully && !window.RequiresReplug && chosen != null && chosen.Key == session?.SelectedKey)
        {
            pending = PendingConnection.Request(chosen, Profile, audio.Checked, DateTime.UtcNow);
            Cover("USB components installed\n\nUnlock the chosen phone and tap Trust when asked.\nIts screen will appear here automatically when ready.");
        }
        else if (window.RequiresReplug)
            Cover("USB setup needs a reconnect\n\nUnplug and reconnect the same phone.\nRun USB setup again if capture support is still missing, then Connect selected.");
    }

    private void SaveDiagnostics()
    {
        using var dialog = new SaveFileDialog { FileName = "WiredMirror-diagnostics.json", Filter = "JSON report|*.json" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new
            {
                version = UpdateClient.Current.ToString(3), utc = DateTime.UtcNow, summary = discovery?.Summary, orientation = Orientation.ToString(),
                devices = session?.Devices.Select(device => new { device.ShortId, device.Model, device.Ios, connection = device.Connection.ToString(), readiness = device.ReadinessText }),
                selectedIdEnding = session?.Selected?.ShortId,
                runningIdEnding = session?.Running?.ShortId, profile = Profile.Name,
                note = "ID endings only. PC decode timing is not end-to-end or input latency. No phone screen content is saved."
            }, new JsonSerializerOptions { WriteIndented = true }));
            SetStatus("Diagnostics saved. No screen content or full device IDs were included.");
        }
        catch (Exception error) { SetStatus(error.Message, true); }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs args)
    {
        if (closed) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        timer.Stop();
        Enabled = false;
        await orientedPreview.Shutdown();
        await gate.WaitAsync();
        try { if (engine != null) await Task.Run(engine.Dispose); }
        catch (Exception error) { Debug.WriteLine(error); }
        finally { gate.Release(); timer.Dispose(); closed = true; Close(); }
    }
}
