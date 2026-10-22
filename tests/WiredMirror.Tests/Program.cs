using System.Runtime.InteropServices;
using WiredMirror;

var passed = 0;
var failed = 0;
var phoneA = Phone("phone-a", "Same name");
var phoneB = Phone("phone-b", "Same name");
var now = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);

Test("Discovery never chooses a phone automatically", () =>
{
    var (_, session) = Fixture(); session.UpdateDevices([phoneA, phoneB]);
    Assert(session.Selected == null); Throws(() => session.StartSelected(VideoProfile.Native, true));
});
Test("Same names still start the exact selected UDID", () =>
{
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA, phoneB]); session.Select(phoneB.Key);
    session.StartSelected(VideoProfile.Native, true); Assert(backend.Started.Single() == phoneB.Udid);
});
Test("Removed selection never falls back to the other phone", () =>
{
    var (_, session) = Fixture(); session.UpdateDevices([phoneA, phoneB]); session.Select(phoneA.Key);
    session.UpdateDevices([phoneB]); Assert(session.Selected == null && session.SelectedKey == phoneA.Key);
    Throws(() => session.StartSelected(VideoProfile.Native, true));
});
Test("Reordering the list preserves identity", () =>
{
    var (_, session) = Fixture(); session.UpdateDevices([phoneA, phoneB]); session.Select(phoneA.Key);
    session.UpdateDevices([phoneB, phoneA]); Assert(session.Selected == phoneA);
});
Test("Switching stops the old source before opening the next", () =>
{
    var (backend, session) = Started(); session.Select(phoneB.Key); session.StartSelected(VideoProfile.FullHd, false);
    Assert(backend.Events.SequenceEqual(["start:phone-a", "stop", "start:phone-b"]));
    Assert(session.Running == phoneB);
});
Test("A failed Stop prevents opening another phone", () =>
{
    var (backend, session) = Started(); backend.StopFails = true; session.Select(phoneB.Key);
    Throws(() => session.StartSelected(VideoProfile.Native, true));
    Assert(session.CleanupRequired && !session.IsActive && backend.Started.Count == 1);
    Throws(() => session.StartSelected(VideoProfile.Native, true));
    backend.StopFails = false; session.Stop(); session.StartSelected(VideoProfile.Native, true);
    Assert(session.Running == phoneB);
});
Test("Failed Start rolls back and never reports active video", () =>
{
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA]); session.Select(phoneA.Key); backend.StartFails = true;
    Throws(() => session.StartSelected(VideoProfile.Native, true));
    Assert(!session.IsActive && backend.Events.Last() == "stop");
});
Test("Failed Start with failed cleanup blocks another Start", () =>
{
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA]); session.Select(phoneA.Key);
    backend.StartFails = backend.StopFails = true; Throws(() => session.StartSelected(VideoProfile.Native, true));
    Assert(session.CleanupRequired); var calls = backend.Started.Count;
    Throws(() => session.StartSelected(VideoProfile.Native, true)); Assert(backend.Started.Count == calls);
});
Test("An untrusted phone cannot start capture", () =>
{
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA with { Readiness = DeviceReadiness.NeedsTrust }]);
    session.Select(phoneA.Key); Throws(() => session.StartSelected(VideoProfile.Native, true)); Assert(backend.Started.Count == 0);
});
Test("USB-only detection cannot be mistaken for a capture-ready phone", () =>
{
    var merged = DeviceCatalog.Merge([], [new PhysicalPhone("aabbcc", "Apple mobile USB device", false)], false, false);
    Assert(merged.Length == 1 && !merged[0].CanConnect && merged[0].Guidance.Contains("missing"));
});
Test("PnP and paired records merge across UDID separators", () =>
{
    var native = NativePhone("00008110-AABBCC", paired: true, unlocked: true);
    var merged = DeviceCatalog.Merge([native], [new PhysicalPhone("00008110aabbcc", "Apple USB", false)], true, true);
    Assert(merged.Length == 1 && merged[0].CanConnect && merged[0].Udid == "00008110-AABBCC");
});
Test("Phone without a pair record asks for Trust", () =>
{
    var devices = DeviceCatalog.Merge([NativePhone("aa", false, true)], [], true, true);
    Assert(devices.Single().Readiness == DeviceReadiness.NeedsTrust && !devices[0].CanConnect);
});
Test("Paired phone without access asks to unlock", () =>
{
    var devices = DeviceCatalog.Merge([NativePhone("aa", true, false)], [], true, true);
    Assert(devices.Single().Readiness == DeviceReadiness.NeedsUnlock);
});
Test("Wireless records are excluded from USB selection", () =>
{
    var native = NativePhone("aa", true, true); native.UsbConnected = 0;
    Assert(DeviceCatalog.Merge([native], [], true, true).Length == 0);
});
Test("Apple audio adapters and non-Apple devices are excluded", () =>
{
    Assert(WindowsUsbDiscovery.MobileSerial(@"USB\VID_05AC&PID_110A\ADAPTER") == null);
    Assert(WindowsUsbDiscovery.MobileSerial(@"USB\VID_1234&PID_12A8\PHONE") == null);
});
Test("Composite child interfaces do not become duplicate phones", () =>
{
    Assert(WindowsUsbDiscovery.MobileSerial(@"USB\VID_05AC&PID_12A8&MI_01\PHONE") == null);
    Assert(WindowsUsbDiscovery.MobileSerial(@"USB\VID_05AC&PID_12A8\PHONE") == "PHONE");
});
Test("Disconnect cannot reconnect to a different same-named phone", () =>
{
    var (backend, session) = Started(); backend.NextStatus = Disconnected(); session.Poll(now);
    session.UpdateDevices([phoneB]); Assert(!session.TryReconnect(now.AddSeconds(1)) && backend.Started.Count == 1);
});
Test("Returning chosen phone reconnects once with its playback settings", () =>
{
    var (backend, session) = Started(); session.RememberPlayback(VideoProfile.Smooth, false);
    backend.NextStatus = Disconnected(); session.Poll(now); session.UpdateDevices([phoneB, phoneA]);
    Assert(session.TryReconnect(now.AddSeconds(2))); Assert(session.Running == phoneA && backend.Started.Last() == phoneA.Udid);
    Assert(backend.LastProfile == VideoProfile.Smooth && !backend.LastAudio);
    Assert(!session.TryReconnect(now.AddSeconds(3)) && backend.Started.Count == 2);
});
Test("Stop cancels pending reconnection", () =>
{
    var (backend, session) = Started(); backend.NextStatus = Disconnected(); session.Poll(now); session.Stop();
    Assert(!session.TryReconnect(now.AddSeconds(1)) && session.WaitingForKey == null);
});
Test("Choosing another phone cancels pending reconnection", () =>
{
    var (backend, session) = Started(); backend.NextStatus = Disconnected(); session.Poll(now); session.Select(phoneB.Key);
    Assert(session.WaitingForKey == null && !session.TryReconnect(now.AddSeconds(1)));
});
Test("Reconnection expires after 60 seconds", () =>
{
    var (backend, session) = Started(); backend.NextStatus = Disconnected(); session.Poll(now);
    Assert(!session.TryReconnect(now.AddSeconds(61)) && session.WaitingForKey == null);
});
Test("Disabled reconnection never starts another capture", () =>
{
    var (backend, session) = Started(); session.ReconnectEnabled = false; backend.NextStatus = Disconnected(); session.Poll(now);
    Assert(session.WaitingForKey == null && !session.TryReconnect(now.AddSeconds(1)));
});
Test("Decoder errors do not trigger a reconnect loop", () =>
{
    var (backend, session) = Started(); backend.NextStatus = new CaptureStatus { State = 7, FailureKind = 4, Message = "decoder error" };
    session.Poll(now); Assert(session.WaitingForKey == null && !session.IsActive);
});
Test("A status read failure closes the session", () =>
{
    var (backend, session) = Started(); backend.StatusFails = true; Throws(() => session.Poll(now));
    Assert(!session.IsActive && backend.Events.Last() == "stop");
});
Test("ABI sizes match the pinned native header", () =>
{
    Assert(Marshal.SizeOf<DeviceInfo>() == 1184); Assert(Marshal.SizeOf<CaptureStatus>() == 464);
    Assert(Marshal.SizeOf<CaptureOptions>() == 48); Assert(Marshal.SizeOf<VideoOutputStatus>() == 64);
    Assert(Marshal.SizeOf<EnvironmentInfo>() == 1136);
});

Test("Bluetooth pairing can never become a screen capture session", () =>
{
    var bluetoothPhone = phoneA with { Connection = ConnectionKind.Bluetooth, Readiness = DeviceReadiness.Available };
    Assert(!bluetoothPhone.CanConnect);
    var (backend, session) = Fixture(); session.UpdateDevices([bluetoothPhone]); session.Select(phoneA.Key);
    Throws(() => session.StartSelected(VideoProfile.Native, true)); Assert(backend.Started.Count == 0);
});
Test("AirPlay source is selected by its own sender ID", () =>
{
    var wireless = phoneA with { Key = "airplay:sender-a", Udid = "sender-a", Connection = ConnectionKind.AirPlay };
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA, wireless]); session.Select(wireless.Key);
    session.StartSelected(VideoProfile.Native, true); Assert(backend.Started.Single() == "sender-a");
});
Test("Waiting for Trust starts only the exact chosen USB phone", () =>
{
    var request = PendingConnection.Request(phoneA, VideoProfile.Smooth, false, now);
    Assert(request.Ready(phoneA.Key, [phoneB], now.AddSeconds(1)) == null);
    Assert(request.Ready(phoneA.Key, [phoneA with { Readiness = DeviceReadiness.NeedsTrust }], now.AddSeconds(1)) == null);
    Assert(request.Ready(phoneA.Key, [phoneB, phoneA], now.AddSeconds(2)) == phoneA);
    Assert(request.Profile == VideoProfile.Smooth && !request.Audio);
});
Test("Changing selected phone cancels waiting for Trust", () =>
{
    var request = PendingConnection.Request(phoneA, VideoProfile.Native, true, now);
    Assert(!request.IsCurrent(phoneB.Key, now) && request.Ready(phoneB.Key, [phoneA, phoneB], now) == null);
});
Test("An unapproved USB connection request expires in two minutes", () =>
{
    var request = PendingConnection.Request(phoneA, VideoProfile.Native, true, now);
    Assert(request.Ready(phoneA.Key, [phoneA], now.AddSeconds(121)) == null);
});
Test("Bluetooth and AirPlay cannot arm a USB Trust request", () =>
{
    Throws(() => PendingConnection.Request(phoneA with { Connection = ConnectionKind.Bluetooth }, VideoProfile.Native, true, now));
    Throws(() => PendingConnection.Request(phoneA with { Connection = ConnectionKind.AirPlay }, VideoProfile.Native, true, now));
});
Test("Setup readiness requires support, matching files and this device's filter", () =>
{
    var state = new SetupState(true, true, true, true, true, [new SetupDevice("PHONE-A", true)]);
    Assert(state.CaptureReady("phonea")); Assert(!state.CaptureReady("phoneb")); Assert(!state.CaptureReady(null));
    Assert(!(state with { SupportReady = false }).CaptureReady("phonea"));
    Assert(!(state with { CaptureFilesReady = false }).CaptureReady("phonea"));
    Assert(!(state with { Devices = [new SetupDevice("PHONE-A", false)] }).CaptureReady("phonea"));
});

Test("An active session retains only its known phone during USB reconfiguration", () =>
{
    var devices = DeviceCatalog.WithActiveSession([phoneB], phoneA);
    Assert(devices.Length == 2 && devices.Single(phone => phone.Key == phoneA.Key).Readiness == DeviceReadiness.InSession);
});
Test("Discovery without an active session never invents a disconnected phone", () =>
{
    Assert(DeviceCatalog.WithActiveSession([], null).Length == 0);
});
Test("An active-session row cannot be opened as a second capture", () =>
{
    var devices = DeviceCatalog.WithActiveSession([phoneA], phoneA);
    Assert(devices.Length == 1 && !devices[0].CanConnect);
});

Test("Portrait and landscape follow changes in the incoming aspect ratio", () =>
{
    Assert(OrientationPolicy.Turns(PreviewOrientation.Portrait, 1080, 1920) == 0);
    Assert(OrientationPolicy.Turns(PreviewOrientation.Portrait, 1920, 1080) == 3);
    Assert(OrientationPolicy.Turns(PreviewOrientation.Landscape, 1080, 1920) == 1);
    Assert(OrientationPolicy.Turns(PreviewOrientation.Landscape, 1920, 1080) == 0);
    Assert(OrientationPolicy.Turns(PreviewOrientation.LandscapeReverse, 1080, 1920) == 3);
    Assert(OrientationPolicy.Turns(PreviewOrientation.LandscapeReverse, 1920, 1080) == 2);
    Assert(OrientationPolicy.Turns(PreviewOrientation.FollowPhone, 1080, 1920) == 0);
});
Test("Rotated fullscreen fitting preserves aspect and stays within the canvas", () =>
{
    var fitted = OrientationPolicy.Fit(new(1920, 1080), new(1080, 1920), 1);
    Assert(fitted == new System.Drawing.Rectangle(0, 0, 1920, 1080));
    fitted = OrientationPolicy.Fit(new(1000, 1000), new(1080, 1920), 0);
    Assert(fitted.Width == 562 && fitted.Height == 1000 && fitted.Left == 219);
    Assert(OrientationPolicy.Fit(new(0, 0), new(1080, 1920), 0).IsEmpty);
    Assert(Marshal.SizeOf<VideoFrameInfo>() == 32);
});
Test("Update checks accept only a newer complete stable release", () =>
{
    Assert(UpdateClient.Parse(ReleaseJson("v0.5.0"))?.Version == new Version(0, 5, 0));
    Assert(UpdateClient.Parse(ReleaseJson("v0.4.0")) == null);
    Assert(UpdateClient.Parse(ReleaseJson("v0.3.0")) == null);
    Assert(UpdateClient.Parse(ReleaseJson("v0.5.0", prerelease: true)) == null);
    Assert(UpdateClient.Parse(ReleaseJson("v0.5.0", draft: true)) == null);
});
Test("Updates reject a foreign repository or missing GitHub digest", () =>
{
    Throws(() => UpdateClient.Parse(ReleaseJson("v0.5.0").Replace("toer15/WiredMirror", "someone/other")));
    Throws(() => UpdateClient.Parse(ReleaseJson("v0.5.0").Replace("sha256:", "invalid:")));
});
Test("Update extraction verifies the archive and required components", () => WithUpdateFolder(folder =>
{
    var (zip, hash) = MakeArchive(folder);
    var staged = WiredMirror.Updates.UpdateInstallation.ExtractVerified(zip, hash, Path.Combine(folder, "stage"));
    Assert(File.Exists(Path.Combine(staged, "WiredMirror.exe")));
    Throws(() => WiredMirror.Updates.UpdateInstallation.ExtractVerified(zip, new string('0', 64), Path.Combine(folder, "bad")));
}));
Test("Update extraction rejects traversal and incomplete packages", () => WithUpdateFolder(folder =>
{
    var (zip, hash) = MakeArchive(folder, extra: "WiredMirror/../../outside.txt");
    Throws(() => WiredMirror.Updates.UpdateInstallation.ExtractVerified(zip, hash, Path.Combine(folder, "traversal")));
    Assert(!File.Exists(Path.Combine(folder, "outside.txt")));
    (zip, hash) = MakeArchive(folder, incomplete: true);
    Throws(() => WiredMirror.Updates.UpdateInstallation.ExtractVerified(zip, hash, Path.Combine(folder, "incomplete")));
}));
Test("Successful update retains the old installation as a backup", () => WithUpdateFolder(folder =>
{
    var old = MakeInstall(Path.Combine(folder, "installed"), "old");
    var next = MakeInstall(Path.Combine(folder, "new"), "new");
    var backup = WiredMirror.Updates.UpdateInstallation.Replace(old, next);
    Assert(File.ReadAllText(Path.Combine(old, "WiredMirror.exe")) == "new");
    Assert(File.ReadAllText(Path.Combine(backup, "WiredMirror.exe")) == "old");
}));
Test("A failed replacement restores the exact old app and its extra files", () => WithUpdateFolder(folder =>
{
    var old = MakeInstall(Path.Combine(folder, "installed"), "old");
    File.WriteAllText(Path.Combine(old, "extra.txt"), "preserve");
    var next = MakeInstall(Path.Combine(folder, "new"), "new");
    var moves = 0;
    try
    {
        WiredMirror.Updates.UpdateInstallation.Replace(old, next, (source, destination) =>
        { if (++moves == 2) throw new IOException("Simulated locked file"); Directory.Move(source, destination); });
        Assert(false);
    }
    catch (IOException) { }
    Assert(File.ReadAllText(Path.Combine(old, "WiredMirror.exe")) == "old");
    Assert(File.ReadAllText(Path.Combine(old, "extra.txt")) == "preserve");
}));
Test("Updater refuses unmarked targets and staging inside an installation", () => WithUpdateFolder(folder =>
{
    Throws(() => WiredMirror.Updates.UpdateInstallation.ValidateTarget(folder));
    var old = MakeInstall(Path.Combine(folder, "installed"), "old");
    var next = MakeInstall(Path.Combine(old, "nested"), "new");
    Throws(() => WiredMirror.Updates.UpdateInstallation.Replace(old, next));
}));

Console.WriteLine($"{passed} passed; {failed} failed. These are logic tests, not physical iPhone capture tests.");
return failed == 0 ? 0 : 1;

void Test(string name, Action check)
{
    try { check(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
static void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
static void Throws(Action action)
{
    try { action(); } catch (InvalidOperationException) { return; }
    throw new Exception("Expected operation to be rejected");
}
static string ReleaseJson(string tag, bool prerelease = false, bool draft = false) => System.Text.Json.JsonSerializer.Serialize(new
{
    tag_name = tag, prerelease, draft,
    assets = new[] { new { name = $"WiredMirror-{tag[1..]}-windows-x64.zip", state = "uploaded",
        browser_download_url = $"https://github.com/toer15/WiredMirror/releases/download/{tag}/WiredMirror-{tag[1..]}-windows-x64.zip", digest = "sha256:" + new string('a', 64) } }
});
static void WithUpdateFolder(Action<string> work)
{
    var folder = Path.Combine(Path.GetTempPath(), "WiredMirror-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    try { work(folder); } finally { Directory.Delete(folder, true); }
}
static string MakeInstall(string folder, string content)
{
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "WiredMirror.exe"), content);
    File.WriteAllText(Path.Combine(folder, WiredMirror.Updates.UpdateInstallation.Marker), WiredMirror.Updates.UpdateInstallation.MarkerText);
    return folder;
}
static (string, string) MakeArchive(string folder, string? extra = null, bool incomplete = false)
{
    var zip = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".zip");
    using (var archive = System.IO.Compression.ZipFile.Open(zip, System.IO.Compression.ZipArchiveMode.Create))
    {
        foreach (var name in new[] { "WiredMirror.exe", ".wiredmirror-install", "WiredMirror.dll", "iPhoneMirror.Core.dll", "WiredMirror.Update.exe", "Wireless/iPhoneMirror.WirelessHost.exe", "Setup/WiredMirror.Setup.exe" })
        {
            if (incomplete && name == "WiredMirror.dll") continue;
            using var writer = new StreamWriter(archive.CreateEntry("WiredMirror/" + name).Open());
            writer.Write(name == ".wiredmirror-install" ? WiredMirror.Updates.UpdateInstallation.MarkerText : "test fixture");
        }
        if (extra != null) archive.CreateEntry(extra);
    }
    return (zip, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(zip))));
}
static PhoneDevice Phone(string id, string name) => new(DeviceIdentity.Key(id), id, name, "iPhone test model", "test", DeviceReadiness.Available, "Available");
static DeviceInfo NativePhone(string id, bool paired, bool unlocked) => new()
{
    Udid = id, Name = "iPhone", ProductType = "iPhone-test", OsVersion = "test", UsbConnected = 1,
    PairRecordPresent = paired ? 1 : 0, LockdownAccessible = unlocked ? 1 : 0
};
static CaptureStatus Disconnected() => new() { State = 7, FailureKind = 8, Message = "device disconnected" };
static (FakeBackend, MirrorSession) Fixture()
{ var backend = new FakeBackend(); return (backend, new MirrorSession(backend)); }
(FakeBackend, MirrorSession) Started()
{
    var (backend, session) = Fixture(); session.UpdateDevices([phoneA, phoneB]); session.Select(phoneA.Key);
    session.StartSelected(VideoProfile.Native, true); return (backend, session);
}

internal sealed class FakeBackend : IMirrorBackend
{
    public List<string> Started = [], Events = [];
    public bool StartFails, StopFails, StatusFails, LastAudio;
    public VideoProfile? LastProfile;
    public CaptureStatus NextStatus = new() { State = 4, VideoFrames = 1 };
    public void Start(PhoneDevice device, VideoProfile profile, bool audio)
    {
        Started.Add(device.Udid); Events.Add("start:" + device.Udid); LastProfile = profile; LastAudio = audio;
        if (StartFails) throw new InvalidOperationException("test start failure");
    }
    public void Stop() { Events.Add("stop"); if (StopFails) throw new InvalidOperationException("test stop failure"); }
    public CaptureStatus Status() { if (StatusFails) throw new InvalidOperationException("test status failure"); return NextStatus; }
}
