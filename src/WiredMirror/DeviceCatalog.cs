namespace WiredMirror;

internal static class DeviceIdentity
{
    // Apple serials sometimes omit the UDID separator in PnP. Never match by name.
    public static string Key(string value) => new(value.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    public static string Suffix(string key) => key.Length <= 6 ? key.ToUpperInvariant() : key[^6..].ToUpperInvariant();
}

internal enum ConnectionKind { Usb, Bluetooth, AirPlay }
internal enum DeviceReadiness { UsbOnly, NeedsTrust, NeedsUnlock, Available, BluetoothOnly, InSession }

internal sealed record PhoneDevice(string Key, string Udid, string Name, string Model,
    string Ios, DeviceReadiness Readiness, string Guidance, ConnectionKind Connection = ConnectionKind.Usb)
{
    public bool CanConnect => Connection != ConnectionKind.Bluetooth && Readiness == DeviceReadiness.Available && !string.IsNullOrWhiteSpace(Udid);
    public string ShortId => DeviceIdentity.Suffix(Key);
    public string ReadinessText => Readiness switch
    {
        DeviceReadiness.UsbOnly => "USB detected · setup needed",
        DeviceReadiness.NeedsTrust => "Tap Trust on phone",
        DeviceReadiness.NeedsUnlock => "Unlock phone",
        DeviceReadiness.BluetoothOnly => "Bluetooth · no screen transport",
        DeviceReadiness.InSession => "Capture active / connecting",
        _ => "Available to connect"
    };
    public override string ToString() => $"{Name} · ID …{ShortId} · {ReadinessText}";
}

internal sealed record PhysicalPhone(string Key, string Name, bool HasDriverProblem);
internal sealed record DiscoverySnapshot(PhoneDevice[] Devices, string Summary, string? Warning = null);

internal static class DeviceCatalog
{
    public static PhoneDevice[] WithActiveSession(PhoneDevice[] discovered, PhoneDevice? running)
    {
        if (running == null) return discovered;
        // USB capture can temporarily remove the phone from Apple's normal pairing
        // list. Retain only the device backed by the current native session handle.
        var active = (discovered.FirstOrDefault(device => device.Key == running.Key) ?? running) with
        {
            Readiness = DeviceReadiness.InSession,
            Guidance = "This is the active capture source. USB reconfiguration can temporarily remove it from Apple's pairing list while video starts."
        };
        return discovered.Where(device => device.Key != active.Key).Append(active).OrderBy(device => device.Name).ToArray();
    }

    public static PhoneDevice[] Merge(IEnumerable<DeviceInfo> native, IEnumerable<PhysicalPhone> physical,
        bool appleServiceInstalled, bool appleServiceRunning)
    {
        var result = new Dictionary<string, PhoneDevice>(StringComparer.Ordinal);
        foreach (var device in native.Where(device => device.UsbConnected != 0))
        {
            var key = DeviceIdentity.Key(device.Udid ?? "");
            if (key.Length == 0) continue;
            var readiness = device.LockdownAccessible != 0 && device.PairRecordPresent != 0
                ? DeviceReadiness.Available : device.PairRecordPresent != 0
                ? DeviceReadiness.NeedsUnlock : DeviceReadiness.NeedsTrust;
            var guidance = readiness switch
            {
                DeviceReadiness.Available => "Select Mirror selected phone. USB capture will be verified when connecting.",
                DeviceReadiness.NeedsUnlock => "Unlock this iPhone. Keep it awake while connecting; discovery updates automatically.",
                _ => "Unlock this iPhone and tap Trust This Computer. Enter your passcode on the phone if asked."
            };
            result[key] = new PhoneDevice(key, device.Udid ?? "", string.IsNullOrWhiteSpace(device.Name) ? "iPhone / iPad" : device.Name,
                device.ProductType ?? "", device.OsVersion ?? "", readiness, guidance);
        }
        foreach (var device in physical)
        {
            if (string.IsNullOrEmpty(device.Key) || result.ContainsKey(device.Key)) continue;
            var guidance = !appleServiceInstalled ? "Windows sees the USB device. Apple USB support is missing; open USB setup."
                : !appleServiceRunning ? "Windows sees the USB device. Apple's pairing service is stopped; open Apple Devices or USB setup."
                : device.HasDriverProblem ? "Windows reports a USB driver problem for this device. Open USB setup."
                : "USB device detected, but Apple has not exposed a paired phone yet. Unlock it, tap Trust, or open USB setup.";
            result[device.Key] = new PhoneDevice(device.Key, "", device.Name, "", "", DeviceReadiness.UsbOnly, guidance);
        }
        return result.Values.OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(device => device.Key, StringComparer.Ordinal).ToArray();
    }
}
