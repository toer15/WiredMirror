using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WiredMirror;

internal static class WindowsBluetoothDiscovery
{
    public static DiscoverySnapshot Scan(bool inquiry)
    {
        var search = new SearchParameters
        {
            Size = (uint)Marshal.SizeOf<SearchParameters>(), Authenticated = 1, Remembered = 1,
            Unknown = 1, Connected = 1, Inquiry = inquiry ? 1 : 0, TimeoutMultiplier = 2
        };
        var device = new BluetoothDevice { Size = (uint)Marshal.SizeOf<BluetoothDevice>(), Name = "" };
        var handle = BluetoothFindFirstDevice(ref search, ref device);
        var result = new List<PhoneDevice>();
        if (handle != 0)
        {
            try
            {
                do
                {
                    var address = device.Address.ToString("X12");
                    var label = string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth device" : device.Name;
                    var paired = device.Authenticated != 0 ? "Paired in Windows." : "Select Pair in Windows to approve pairing on both devices.";
                    result.Add(new PhoneDevice("bluetooth:" + address.ToLowerInvariant(), address, label, "", "",
                        DeviceReadiness.BluetoothOnly,
                        paired + " Bluetooth pairing does not provide an iPhone screen stream. Use USB or Wi-Fi / AirPlay for the preview.", ConnectionKind.Bluetooth));
                    device.Size = (uint)Marshal.SizeOf<BluetoothDevice>();
                } while (BluetoothFindNextDevice(handle, ref device));
            }
            finally { BluetoothFindDeviceClose(handle); }
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 0 && error != 259 && error != 1168)
                return new([], "Bluetooth discovery unavailable. Check Windows Bluetooth settings.", new Win32Exception(error).Message);
        }
        return new(result.DistinctBy(phone => phone.Key).OrderBy(phone => phone.Name).ToArray(),
            $"{result.Count} discovered / remembered Bluetooth device(s) · Open iPhone Bluetooth settings for discovery. Bluetooth does not carry its screen.");
    }

    public static void Pair(PhoneDevice phone, nint owner)
    {
        if (phone.Connection != ConnectionKind.Bluetooth) throw new InvalidOperationException("Select a Bluetooth device first.");
        var device = new BluetoothDevice { Size = (uint)Marshal.SizeOf<BluetoothDevice>(),
            Address = Convert.ToUInt64(phone.Udid, 16), Name = "" };
        var result = BluetoothAuthenticateDeviceEx(owner, 0, ref device, 0, 0);
        if (result != 0 && result != 259) throw new Win32Exception((int)result, "Bluetooth pairing did not complete.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SearchParameters
    {
        public uint Size;
        public int Authenticated, Remembered, Unknown, Connected, Inquiry;
        public byte TimeoutMultiplier;
        public nint Radio;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime { public ushort Year, Month, DayOfWeek, Day, Hour, Minute, Second, Milliseconds; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct BluetoothDevice
    {
        public uint Size;
        public ulong Address;
        public uint Class;
        public int Connected, Remembered, Authenticated;
        public SystemTime LastSeen, LastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)] public string Name;
    }
    [DllImport("bthprops.cpl", SetLastError = true)] private static extern nint BluetoothFindFirstDevice(ref SearchParameters search, ref BluetoothDevice device);
    [DllImport("bthprops.cpl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindNextDevice(nint find, ref BluetoothDevice device);
    [DllImport("bthprops.cpl")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool BluetoothFindDeviceClose(nint find);
    [DllImport("bthprops.cpl")] private static extern uint BluetoothAuthenticateDeviceEx(nint parent, nint radio, ref BluetoothDevice device, nint oob, int requirements);
}
