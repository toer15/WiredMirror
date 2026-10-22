// C ABI declarations follow RayrenSX/iPhoneMirror v1.8.3, CoreApi.h (GPL-3.0-only).
using System.Runtime.InteropServices;

namespace WiredMirror;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DeviceInfo
{
    public uint StructSize, ApiVersion, DeviceId, MuxPort;
    public int State, UsbConnected, PairRecordPresent, LockdownAccessible;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Udid;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ProductType;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string OsVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ConnectionType;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 192)] public string Status;
    public override string ToString() => $"{(string.IsNullOrWhiteSpace(Name) ? "iPhone" : Name)}  ·  iOS {OsVersion}";
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CaptureStatus
{
    public uint StructSize, ApiVersion;
    public int State;
    public uint Width, Height;
    public double Fps, LatencyMs;
    public ulong VideoFrames, AudioPackets;
    public uint AudioSampleRate, AudioChannels;
    public int FailureKind, FailureStage, ErrorCode;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 192)] public string Message;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct EnvironmentInfo
{
    public uint StructSize, ApiVersion;
    public int ServiceInstalled, ServiceRunning, StandardMuxAvailable, CaptureMuxAvailable;
    public uint PhysicalDevices;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Diagnostic;
    public int LibUsbAvailable, UsbDkAvailable;
    public uint LibUsbDevices;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string LibUsbVersion;
    public int UsbDkKnown, LibUsbDevicesKnown;
}

[StructLayout(LayoutKind.Sequential)]
internal struct CaptureOptions
{
    public uint StructSize, ApiVersion, Width, Height, Fps;
    public int Audio;
    public float Volume;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)] public uint[] Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VideoOutputStatus
{
    public uint StructSize, ApiVersion, HdrCapability;
    public int SourceHdrKnown, SourceHdr, ActualHdrSurface, HdrEffective;
    public uint RequestedColor, RequestedDecoder, AppliedDecoder, DecoderSwitchState, DecoderRuntimeMode;
    public ulong RequestedGeneration, AppliedGeneration;
    public string DecoderText => DecoderRuntimeMode switch { 1 => "Hardware", 2 => "Software", 3 => "External", _ => "Starting" };
}

internal static class Native
{
    private const string Dll = "iPhoneMirror.Core.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern uint im_api_version();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_initialize();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void im_shutdown();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern nint im_last_error();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_refresh_devices_ex([Out] DeviceInfo[]? devices, ref uint count, int refreshMetadata);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int im_start_capture_ex(string udid, int audio);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_stop_capture();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_get_capture_status(ref CaptureStatus status);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_attach_preview_window(nint window);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void im_detach_preview_window();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_set_audio_enabled(int enabled);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_get_environment(ref EnvironmentInfo info);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int im_session_create(string udid, ref CaptureOptions options, out ulong handle);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_stop(ulong handle);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void im_session_destroy(ulong handle);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_get_status(ulong handle, ref CaptureStatus status);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_attach_preview(ulong handle, nint window);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void im_session_detach_preview(ulong handle, nint window);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_set_audio_enabled(ulong handle, int enabled);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_set_video_preferences(ulong handle, uint width, uint height, uint fps);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_set_pipeline_preferences(ulong handle, uint decoder, uint color);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_get_video_output_status(ulong handle, nint window, ref VideoOutputStatus status);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int im_wireless_receiver_start_ex(string receiverName, string hostPath, uint width, uint height, uint fps);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern void im_wireless_receiver_stop();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_wireless_receiver_get_status(out int running, out int ready);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_refresh_wireless_devices([Out] DeviceInfo[]? devices, ref uint count);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)] internal static extern int im_wireless_session_create(string deviceId, ref CaptureOptions options, out ulong handle);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_get_latest_video_timestamp(ulong handle, out long timestamp);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] internal static extern int im_session_copy_latest_video_frame(ulong handle, ref VideoFrameInfo info, nint buffer, ref uint capacity, uint maxWidth, uint maxHeight);
}

[StructLayout(LayoutKind.Sequential)]
internal struct VideoFrameInfo
{
    public uint StructSize, ApiVersion, Width, Height, Stride, PixelFormat;
    public long Timestamp;
}

internal sealed class UsbEngine : IDisposable, IMirrorBackend
{
    private bool disposed;
    private ulong session;
    private nint previewWindow;
    private bool nativePreview = true;
    private bool wirelessStarted;
    private readonly object frameGate = new();
    internal const string ReceiverName = "WiredMirror";

    public UsbEngine()
    {
        if (Native.im_api_version() != 18)
            throw new InvalidOperationException("The USB engine version does not match this app. Re-extract the full WiredMirror folder.");
        Check(Native.im_initialize());
    }

    private static void Check(int result)
    {
        if (result == 0) return;
        var message = Marshal.PtrToStringUni(Native.im_last_error());
        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? $"USB engine error {result}." : message);
    }

    public DeviceInfo[] Devices(bool refreshMetadata = true)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint count = 0;
            Check(Native.im_refresh_devices_ex(null, ref count, 0));
            if (count == 0) return [];
            if (count > 256) throw new InvalidOperationException("Unexpected device count from the USB engine.");
            var devices = new DeviceInfo[count];
            for (var index = 0; index < devices.Length; index++)
                devices[index] = new DeviceInfo
                {
                    StructSize = (uint)Marshal.SizeOf<DeviceInfo>(), ApiVersion = 18,
                    Udid = "", Name = "", ProductType = "", OsVersion = "", ConnectionType = "", Status = ""
                };
            var capacity = count;
            var result = Native.im_refresh_devices_ex(devices, ref capacity, refreshMetadata ? 1 : 0);
            if (result == -3) continue;
            Check(result);
            return devices.Take((int)Math.Min(capacity, count)).Where(device => device.UsbConnected != 0).ToArray();
        }
        throw new InvalidOperationException("The phone connection changed. Press Refresh to try again.");
    }

    public DiscoverySnapshot Discover(bool refreshMetadata)
    {
        var warnings = new List<string>();
        PhysicalPhone[] physical = [];
        DeviceInfo[] native = [];
        var environment = new EnvironmentInfo
        {
            StructSize = (uint)Marshal.SizeOf<EnvironmentInfo>(), ApiVersion = 18, Diagnostic = "", LibUsbVersion = ""
        };
        try { physical = WindowsUsbDiscovery.Scan(); }
        catch (Exception error) { warnings.Add(error.Message); }
        try { Check(Native.im_get_environment(ref environment)); }
        catch (Exception error) { warnings.Add(error.Message); }
        try { native = Devices(refreshMetadata); }
        catch (Exception error) { warnings.Add(error.Message); }
        var devices = DeviceCatalog.Merge(native, physical, environment.ServiceInstalled != 0, environment.ServiceRunning != 0);
        var service = environment.ServiceInstalled == 0 ? "Apple USB support: missing"
            : environment.ServiceRunning == 0 ? "Apple pairing service: stopped" : "Apple pairing service: running";
        var pairing = environment.StandardMuxAvailable != 0 || environment.CaptureMuxAvailable != 0 ? "pairing channel available" : "pairing channel unavailable";
        return new DiscoverySnapshot(devices, $"{devices.Length} USB mobile device(s) · {service} · {pairing}",
            warnings.Count == 0 ? null : string.Join(" ", warnings));
    }

    public void StartWireless()
    {
        Check(Native.im_wireless_receiver_get_status(out var running, out var ready));
        if (wirelessStarted && running != 0 && ready != 0) return;
        StopWireless();
        var occupied = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners().Any(endpoint => endpoint.Port is 5001 or 7001);
        if (occupied) throw new InvalidOperationException("AirPlay ports 5001/7001 are in use. Close other WiredMirror/AirPlay receiver windows, then press Restart AirPlay.");
        var host = Path.Combine(AppContext.BaseDirectory, "Wireless", "iPhoneMirror.WirelessHost.exe");
        if (!File.Exists(host)) throw new FileNotFoundException("The AirPlay receiver is missing. Extract the full WiredMirror package.", host);
        Check(Native.im_wireless_receiver_start_ex(ReceiverName, host, 1920, 1080, 60));
        wirelessStarted = true;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.Elapsed.TotalSeconds < 12)
        {
            Check(Native.im_wireless_receiver_get_status(out running, out ready));
            if (running != 0 && ready != 0) return;
            if (timeout.Elapsed.TotalSeconds > 2 && running == 0) break;
            Thread.Sleep(100);
        }
        StopWireless();
        throw new InvalidOperationException("AirPlay could not start. Close other receivers and use AirPlay setup, then Restart AirPlay.");
    }

    public void StopWireless()
    {
        if (!wirelessStarted) return;
        Native.im_wireless_receiver_stop();
        wirelessStarted = false;
    }

    public DiscoverySnapshot DiscoverWireless()
    {
        Check(Native.im_wireless_receiver_get_status(out var running, out var ready));
        if (running == 0 || ready == 0)
        {
            StartWireless();
            Check(Native.im_wireless_receiver_get_status(out running, out ready));
        }
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint count = 0;
            Check(Native.im_refresh_wireless_devices(null, ref count));
            if (count > 256) throw new InvalidOperationException("Unexpected AirPlay sender count.");
            if (count == 0) return new([], ready != 0 ? "AirPlay ready · On iPhone: Control Centre → Screen Mirroring → WiredMirror. Then select that sender here."
                : running != 0 ? "AirPlay receiver is starting..." : "AirPlay receiver stopped. Choose another connection mode and retry.");
            var devices = new DeviceInfo[count];
            for (var index = 0; index < devices.Length; index++) devices[index] = new DeviceInfo
            {
                StructSize = (uint)Marshal.SizeOf<DeviceInfo>(), ApiVersion = 18,
                Udid = "", Name = "", ProductType = "", OsVersion = "", ConnectionType = "", Status = ""
            };
            var capacity = count;
            var result = Native.im_refresh_wireless_devices(devices, ref capacity);
            if (result == -3) continue;
            Check(result);
            var phones = devices.Take((int)Math.Min(count, capacity)).Where(device => !string.IsNullOrWhiteSpace(device.Udid))
                .Select(device => new PhoneDevice("airplay:" + DeviceIdentity.Key(device.Udid), device.Udid,
                    string.IsNullOrWhiteSpace(device.Name) ? "AirPlay sender" : device.Name, device.ProductType, device.OsVersion,
                    DeviceReadiness.Available, "This phone is sending AirPlay. Choose it and press Connect selected to show it in the centre preview.", ConnectionKind.AirPlay)).ToArray();
            return new(phones, $"{phones.Length} active AirPlay sender(s). Select the phone you want to display.");
        }
        throw new InvalidOperationException("The AirPlay sender list changed. Scan again.");
    }

    public void PreparePreview(nint window)
    {
        if (window == previewWindow) return;
        if (session != 0 && nativePreview) Native.im_session_detach_preview(session, previewWindow);
        previewWindow = window;
        if (session != 0 && nativePreview) Check(Native.im_session_attach_preview(session, previewWindow));
    }

    public void UseNativePreview(bool enabled)
    {
        if (enabled == nativePreview) return;
        if (session != 0 && nativePreview) Native.im_session_detach_preview(session, previewWindow);
        nativePreview = enabled;
        if (session != 0 && nativePreview) Check(Native.im_session_attach_preview(session, previewWindow));
    }

    public void Start(PhoneDevice device, VideoProfile profile, bool audio)
    {
        if (session != 0) throw new InvalidOperationException("Stop the current phone before opening another USB session.");
        if (!device.CanConnect) throw new InvalidOperationException(device.Guidance);
        if (previewWindow == 0) throw new InvalidOperationException("The preview window is not ready.");
        var options = new CaptureOptions
        {
            StructSize = (uint)Marshal.SizeOf<CaptureOptions>(), ApiVersion = 18,
            Width = profile.Width, Height = profile.Height, Fps = profile.Fps,
            Audio = audio ? 1 : 0, Volume = 1,
            // Native source sizing, USB screen mode, hardware-preferred decode, automatic color.
            Reserved = [0, 0, 0, profile.Decoder, 0]
        };
        Check(device.Connection == ConnectionKind.AirPlay
            ? Native.im_wireless_session_create(device.Udid, ref options, out session)
            : Native.im_session_create(device.Udid, ref options, out session));
        try { if (nativePreview) Check(Native.im_session_attach_preview(session, previewWindow)); }
        catch { Stop(); throw; }
    }

    public CaptureStatus Status()
    {
        var status = new CaptureStatus { StructSize = (uint)Marshal.SizeOf<CaptureStatus>(), ApiVersion = 18, Message = "" };
        Check(Native.im_session_get_status(session, ref status));
        return status;
    }

    public void Stop()
    {
        lock (frameGate) StopLocked();
    }

    private void StopLocked()
    {
        if (session == 0) return;
        Native.im_session_detach_preview(session, previewWindow);
        var result = Native.im_session_stop(session);
        if (result != 0 && result != -12) Check(result); // Retain failed teardown for an explicit Stop retry.
        var warning = result == -12 ? Marshal.PtrToStringUni(Native.im_last_error()) : null;
        Native.im_session_destroy(session);
        session = 0;
        if (warning != null) throw new InvalidOperationException(warning);
    }

    public void Attach(nint window) => Check(Native.im_attach_preview_window(window));
    public void Detach() => Native.im_detach_preview_window();
    public void Audio(bool enabled) { if (session != 0) Check(Native.im_session_set_audio_enabled(session, enabled ? 1 : 0)); }
    public void ApplyProfile(VideoProfile profile)
    {
        if (session == 0) return;
        Check(Native.im_session_set_video_preferences(session, profile.Width, profile.Height, profile.Fps));
        Check(Native.im_session_set_pipeline_preferences(session, profile.Decoder, 0));
    }

    public VideoOutputStatus OutputStatus()
    {
        var output = new VideoOutputStatus { StructSize = (uint)Marshal.SizeOf<VideoOutputStatus>(), ApiVersion = 18 };
        Check(Native.im_session_get_video_output_status(session, nativePreview ? previewWindow : 0, ref output));
        return output;
    }

    public long ReadPreview(ref Bitmap? bitmap, long previous, uint edge)
    {
        lock (frameGate)
        {
            if (session == 0 || disposed) return 0;
            if (Native.im_session_get_latest_video_timestamp(session, out var timestamp) != 0 || timestamp == 0 || timestamp == previous) return 0;
            var info = new VideoFrameInfo { StructSize = (uint)Marshal.SizeOf<VideoFrameInfo>(), ApiVersion = 18 };
            uint capacity = 0;
            if (Native.im_session_copy_latest_video_frame(session, ref info, 0, ref capacity, edge, edge) != -3) return 0;
            if (info.Width == 0 || info.Height == 0 || info.Width > 1920 || info.Height > 1920) return 0;
            if (bitmap == null || bitmap.Width != info.Width || bitmap.Height != info.Height)
            {
                bitmap?.Dispose();
                bitmap = new Bitmap((int)info.Width, (int)info.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            }
            var pixels = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            try
            {
                capacity = checked((uint)(pixels.Stride * bitmap.Height));
                var result = Native.im_session_copy_latest_video_frame(session, ref info, pixels.Scan0, ref capacity, edge, edge);
                // Orientation can change between the size query and copy. Discard
                // that frame instead of displaying a partial/differently sized buffer.
                return result == 0 && info.Width == bitmap.Width && info.Height == bitmap.Height ? info.Timestamp : 0;
            }
            finally { bitmap.UnlockBits(pixels); }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        try { Stop(); }
        finally
        {
            StopWireless();
            Native.im_detach_preview_window();
            Native.im_shutdown();
            disposed = true;
        }
    }
}
