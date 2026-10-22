namespace WiredMirror;

internal sealed record VideoProfile(string Name, uint Width, uint Height, uint Fps, uint Decoder)
{
    public static readonly VideoProfile Native = new("Native · up to 60 fps", 0, 0, 60, 1);
    public static readonly VideoProfile Smooth = new("720p preview · up to 60 fps", 1280, 720, 60, 1);
    public static readonly VideoProfile FullHd = new("1080p preview · up to 60 fps", 1920, 1080, 60, 1);
    public static readonly VideoProfile Compatible = new("Software decoder · up to 60 fps", 0, 0, 60, 2);
    public override string ToString() => Name;
}

internal interface IMirrorBackend
{
    void Start(PhoneDevice device, VideoProfile profile, bool audio);
    void Stop();
    CaptureStatus Status();
}

// All calls are serialized by the UI operation gate. Selection is a stable ID,
// separate from the running capture, and is never inferred from a device's name.
internal sealed class MirrorSession(IMirrorBackend backend)
{
    public PhoneDevice[] Devices { get; private set; } = [];
    public string? SelectedKey { get; private set; }
    public PhoneDevice? Selected => Devices.FirstOrDefault(device => device.Key == SelectedKey);
    public PhoneDevice? Running { get; private set; }
    public bool CleanupRequired { get; private set; }
    public bool ReconnectEnabled { get; set; } = true;
    public string? WaitingForKey { get; private set; }
    public bool IsActive => Running != null;
    private DateTime reconnectUntil;
    private VideoProfile profile = VideoProfile.Native;
    private bool audio = true;

    public void UpdateDevices(PhoneDevice[] devices) => Devices = devices.DistinctBy(device => device.Key).ToArray();

    public void Select(string? key)
    {
        if (key != null && Devices.All(device => device.Key != key)) throw new InvalidOperationException("That phone is no longer connected.");
        SelectedKey = key;
        if (WaitingForKey != key) WaitingForKey = null;
    }

    public void StartSelected(VideoProfile selectedProfile, bool withAudio)
    {
        var target = Selected ?? throw new InvalidOperationException("Choose a connected phone first. No device was selected automatically.");
        if (!target.CanConnect) throw new InvalidOperationException(target.Guidance);
        if (CleanupRequired) throw new InvalidOperationException("The previous USB session has not stopped cleanly. Press Stop again before connecting.");
        WaitingForKey = null;
        if (Running != null) Stop();
        profile = selectedProfile;
        audio = withAudio;
        try { backend.Start(target, selectedProfile, withAudio); }
        catch
        {
            try { backend.Stop(); }
            catch { CleanupRequired = true; }
            throw;
        }
        Running = target;
    }

    public void RememberPlayback(VideoProfile selectedProfile, bool withAudio)
    { profile = selectedProfile; audio = withAudio; }

    public void Stop()
    {
        WaitingForKey = null;
        if (Running == null && !CleanupRequired) return;
        Running = null; // The UI covers the old frame before any other device can connect.
        CleanupRequired = true;
        backend.Stop();
        CleanupRequired = false;
    }

    public CaptureStatus Poll(DateTime utcNow)
    {
        if (Running == null) throw new InvalidOperationException("No active session.");
        var previous = Running;
        CaptureStatus status;
        try { status = backend.Status(); }
        catch { Stop(); throw; }
        if (status.State is 6 or 7)
        {
            Stop();
            if (ReconnectEnabled && status.FailureKind == 8 && SelectedKey == previous.Key)
            {
                WaitingForKey = previous.Key;
                reconnectUntil = utcNow.AddSeconds(60);
            }
        }
        return status;
    }

    public bool TryReconnect(DateTime utcNow)
    {
        if (!ReconnectEnabled || utcNow > reconnectUntil || SelectedKey != WaitingForKey)
        { WaitingForKey = null; return false; }
        if (WaitingForKey == null || Running != null || CleanupRequired || Selected?.CanConnect != true) return false;
        // One attempt after this phone returns. Failure needs an explicit user retry.
        WaitingForKey = null;
        StartSelected(profile, audio);
        return true;
    }
}
