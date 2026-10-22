using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WiredMirror;

// Reads Windows' present-device records. Does not open USB endpoints or change drivers.
internal static class WindowsUsbDiscovery
{
    // Same mobile-device PID table as upstream v1.8.3 AppleUsbDiscovery.h.
    private static readonly HashSet<int> MobilePids = [0x1290, 0x1291, 0x1292, 0x1293, 0x1294, 0x1297,
        0x1299, 0x129A, 0x129C, 0x129D, 0x129E, 0x129F, 0x12A0, 0x12A1, 0x12A2, 0x12A3,
        0x12A4, 0x12A5, 0x12A6, 0x12A8, 0x12A9, 0x12AA, 0x12AB, 0x12AC];

    internal static string? MobileSerial(string instanceId)
    {
        var parts = instanceId.Split('\\');
        const string prefix = "VID_05AC&PID_";
        if (parts.Length != 3 || !parts[0].Equals("USB", StringComparison.OrdinalIgnoreCase)
            || !parts[1].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || parts[1].Length != prefix.Length + 4
            || !int.TryParse(parts[1].AsSpan(prefix.Length), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var pid) || !MobilePids.Contains(pid)) return null;
        // Location-generated IDs are not stable phone serials; never merge or reconnect by those.
        return parts[2].Length > 0 ? parts[2] : null;
    }

    public static PhysicalPhone[] Scan()
    {
        var set = SetupDiGetClassDevsW(0, "USB", 0, 0x02 | 0x04);
        if (set == -1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows USB discovery failed.");
        try
        {
            var phones = new List<PhysicalPhone>();
            for (uint index = 0; ; index++)
            {
                var info = new DevInfo { Size = (uint)Marshal.SizeOf<DevInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref info))
                {
                    if (Marshal.GetLastWin32Error() == 259) break;
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not enumerate Windows USB devices.");
                }
                var instance = new StringBuilder(1024);
                if (!SetupDiGetDeviceInstanceIdW(set, ref info, instance, instance.Capacity, out _)) continue;
                var serial = MobileSerial(instance.ToString());
                if (serial == null) continue;
                var label = Property(set, ref info, 12);
                if (label.Length == 0) label = Property(set, ref info, 0);
                if (label.Length == 0) label = "Apple mobile USB device";
                var key = serial.Contains('&') ? "pnp:" + instance.ToString().ToLowerInvariant() : DeviceIdentity.Key(serial);
                var problem = CM_Get_DevNode_Status(out _, out var problemCode, info.DevInst, 0) == 0 && problemCode != 0;
                phones.Add(new PhysicalPhone(key, label, problem));
            }
            return phones.DistinctBy(device => device.Key).ToArray();
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    private static string Property(nint set, ref DevInfo info, uint property)
    {
        var data = new byte[4096];
        return SetupDiGetDeviceRegistryPropertyW(set, ref info, property, out _, data, (uint)data.Length, out _)
            ? Encoding.Unicode.GetString(data).Split('\0')[0] : "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevInfo { public uint Size; public Guid ClassGuid; public uint DevInst; public nint Reserved; }
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetupDiGetClassDevsW(nint classGuid, string enumerator, nint parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiEnumDeviceInfo(nint set, uint index, ref DevInfo info);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceInstanceIdW(nint set, ref DevInfo info, StringBuilder id, int size, out uint required);
    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiGetDeviceRegistryPropertyW(nint set, ref DevInfo info, uint property, out uint type, byte[] data, uint size, out uint required);
    [DllImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetupDiDestroyDeviceInfoList(nint set);
    [DllImport("cfgmgr32.dll")] private static extern uint CM_Get_DevNode_Status(out uint status, out uint problem, uint devInst, uint flags);
}
