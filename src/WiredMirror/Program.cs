using System.Text.Json;

namespace WiredMirror;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiredMirror", "Logs");
        Directory.CreateDirectory(logs);
        Environment.SetEnvironmentVariable("IPHONE_MIRROR_LOG_FILE", Path.Combine(logs, $"engine-{Environment.ProcessId}.log"));
        if (args.Contains("--check") || args.Contains("--check-wireless"))
        {
            var reportPath = Path.Combine(AppContext.BaseDirectory, args.Contains("--check-wireless") ? "wireless-check.json" : "startup-check.json");
            try
            {
                File.WriteAllText(reportPath, "{\"stage\":\"initializing\",\"passed\":false}");
                using var engine = new UsbEngine();
                File.WriteAllText(reportPath, "{\"stage\":\"discovering\",\"passed\":false}");
                var discovery = engine.Discover(true);
                var setup = UsbSetupClient.Run(false, null).GetAwaiter().GetResult();
                if (!setup.Success || setup.State == null) throw new InvalidOperationException("USB setup inspection failed: " + setup.Message);
                var bluetooth = WindowsBluetoothDiscovery.Scan(false);
                bool? wirelessReady = null;
                if (args.Contains("--check-wireless"))
                {
                    engine.StartWireless();
                    var started = System.Diagnostics.Stopwatch.StartNew();
                    while (started.Elapsed.TotalSeconds < 10)
                    {
                        if (Native.im_wireless_receiver_get_status(out _, out var ready) == 0 && ready != 0) { wirelessReady = true; break; }
                        Thread.Sleep(200);
                    }
                    engine.StopWireless();
                    if (wirelessReady != true) throw new InvalidOperationException("The AirPlay receiver did not report ready within ten seconds.");
                }
                var nativeLibrary = System.Runtime.InteropServices.NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "iPhoneMirror.Core.dll"));
                try
                {
                    foreach (var export in new[] { "im_session_create", "im_session_stop", "im_session_destroy", "im_session_attach_preview",
                        "im_session_detach_preview", "im_session_get_status", "im_session_set_audio_enabled",
                        "im_session_get_video_output_status", "im_session_set_video_preferences", "im_session_set_pipeline_preferences",
                        "im_wireless_receiver_start_ex", "im_wireless_session_create", "im_refresh_wireless_devices" })
                        _ = System.Runtime.InteropServices.NativeLibrary.GetExport(nativeLibrary, export);
                }
                finally { System.Runtime.InteropServices.NativeLibrary.Free(nativeLibrary); }
                File.WriteAllText(reportPath, "{\"stage\":\"attaching-preview\",\"passed\":false}");
                using var panel = new Panel();
                engine.Attach(panel.Handle);
                File.WriteAllText(reportPath, "{\"stage\":\"detaching-preview\",\"passed\":false}");
                engine.Detach();
                File.WriteAllText(reportPath, JsonSerializer.Serialize(new
                {
                    passed = discovery.Warning == null, version = UpdateClient.Current.ToString(3), engineApi = Native.im_api_version(),
                    deviceCount = discovery.Devices.Length, discovery.Summary, discovery.Warning,
                    deviceDiscovery = "Windows PnP and Apple USB pairing", sessionExports = "passed",
                    previewAttachment = "passed",
                    deviceStructBytes = System.Runtime.InteropServices.Marshal.SizeOf<DeviceInfo>(),
                    statusStructBytes = System.Runtime.InteropServices.Marshal.SizeOf<CaptureStatus>(),
                    optionsStructBytes = System.Runtime.InteropServices.Marshal.SizeOf<CaptureOptions>(),
                    setupInspection = "passed", appleSupportReady = setup.State.SupportReady,
                    bluetoothDeviceCount = bluetooth.Devices.Length, bluetoothWarning = bluetooth.Warning,
                    wirelessReady,
                    hardwareCaptureTested = false,
                    note = "Startup and native rendering check only. No capture is started."
                }, new JsonSerializerOptions { WriteIndented = true }));
                return discovery.Warning == null ? 0 : 1;
            }
            catch (Exception error)
            {
                File.WriteAllText(reportPath, JsonSerializer.Serialize(new { passed = false, error = error.Message }));
                return 1;
            }
        }
        using var instance = new Mutex(true, @"Local\WiredMirror-Receiver", out var first);
        if (!first)
        {
            MessageBox.Show("WiredMirror is already open. Use its existing window. Close older copies before starting AirPlay.", "WiredMirror");
            return 0;
        }
        Application.Run(new MirrorWindow());
        return 0;
    }
}
