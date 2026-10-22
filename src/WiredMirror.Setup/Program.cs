using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using IPhoneMirror.DriverInstaller.Models;
using IPhoneMirror.DriverInstaller.Services;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (ElevatedDriverHost.IsRequested(args)) return ElevatedDriverHost.Run(args);
        try { return Run(args).GetAwaiter().GetResult(); }
        catch (Exception error) { Console.Error.WriteLine(error); Complete(false, error.Message); return 1; }
    }

    private static async Task<int> Run(string[] args)
    {
        if (args.SequenceEqual(["--verify-payload"]))
        {
            foreach (var (name, expected) in new[]
            {
                ("DriverPayload.amd64.install-filter.exe", DriverConstants.InstallerHash),
                ("DriverPayload.amd64.libusb0.sys", DriverConstants.DriverHash),
                ("DriverPayload.amd64.libusb0.dll", DriverConstants.Dll64Hash),
                ("DriverPayload.x86.libusb0_x86.dll", DriverConstants.Dll32Hash)
            })
            {
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                    ?? throw new InvalidOperationException("Missing embedded driver: " + name);
                if (!Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Driver checksum mismatch: " + name);
            }
            Complete(true, "All four embedded capture-driver payload hashes match upstream.");
            return 0;
        }
        var catalog = new DeviceCatalog();
        if (args.SequenceEqual(["--inspect"])) { State(catalog); return 0; }
        if (args.Length != 1 && args.Length != 3 || args[0] != "--install"
            || args.Length == 3 && (args[1] != "--serial" || !DriverConstants.IsValidSerial(args[2])))
            throw new ArgumentException("Use --inspect, --verify-payload, or --install [--serial EXACT_NORMALIZED_SERIAL].");

        // Pin the executable and its directories before it can launch an elevated copy.
        if (!string.Equals(Path.GetFileName(Environment.ProcessPath), "WiredMirror.Setup.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Run the published WiredMirror.Setup.exe to install drivers.");
        var boundaryError = DriverOperationClient.InitializeElevationBoundary();
        if (boundaryError != null) throw new InvalidOperationException("Setup could not protect its executable before elevation.", boundaryError);
        var serial = args.Length == 3 ? args[2] : null;
        if (serial != null && Find(catalog, serial) == null)
            throw new InvalidOperationException("The selected USB phone is no longer connected. No other phone was chosen.");
        Emit(new { kind = "progress", message = "Checking and installing Apple Mobile Device Support. Approve Windows' administrator prompt if shown." });
        var progress = new InlineProgress(message => Emit(new { kind = "progress", message }));
        var support = await new AppleSupportInstaller(catalog).InstallAsync(progress);
        if (!support.Success) { State(catalog); Complete(false, support.Message); return 1; }
        if (serial == null)
        {
            State(catalog);
            Complete(true, "Apple USB support is installed. Connect your iPhone, choose it, and run USB setup again to prepare that phone's capture driver.");
            return 0;
        }
        var device = Find(catalog, serial) ?? throw new InvalidOperationException("Apple support was installed, but the selected phone disconnected. Reconnect that phone and run setup again.");
        var operations = new DriverOperationClient();
        if (!device.Service.Equals("usbccgp", StringComparison.OrdinalIgnoreCase))
        {
            if (!DriverConstants.IsKnownReplaceableParentService(device.Service))
                throw new InvalidOperationException("This phone's USB parent is not in a supported state. Reconnect it and check Apple Devices before retrying.");
            Emit(new { kind = "progress", message = "Restoring the selected phone's Apple USB parent driver." });
            var repair = await operations.RunAsync(DriverOperationKind.ParentRepair, device);
            State(catalog);
            Complete(repair.Success, repair.Message, repair.Success || repair.RequiresReplug);
            return repair.Success ? 0 : 1;
        }
        var stack = catalog.InspectLibUsbStack();
        if (!device.HasLibUsb0Filter || !stack.FilesMatch)
        {
            Emit(new { kind = "progress", message = "Installing capture support for the chosen phone only. Approve Windows' administrator prompt if shown." });
            var result = await operations.RunAsync(device.HasLibUsb0Filter ? DriverOperationKind.Repair : DriverOperationKind.Install, device);
            State(catalog);
            Complete(result.Success, result.Message, result.RequiresReplug);
            return result.Success ? 0 : 1;
        }
        State(catalog);
        Complete(true, "Apple USB support and this phone's capture driver are installed. Unlock the phone and approve Trust to start mirroring.");
        return 0;
    }

    private static AppleDeviceRecord? Find(DeviceCatalog catalog, string serial) => catalog.GetAppleDevices(includeMetadata: false)
        .SingleOrDefault(device => device.IsPresent && device.Serial.Equals(serial, StringComparison.OrdinalIgnoreCase));

    private static void State(DeviceCatalog catalog)
    {
        var support = catalog.InspectAppleSupport();
        var stack = catalog.InspectLibUsbStack();
        Emit(new { kind = "state", state = new
        {
            supportReady = support.Ready, serviceInstalled = support.ServiceInstalled,
            serviceRunning = support.ServiceRunning, usbDriverInstalled = support.UsbDriverInstalled,
            captureFilesReady = stack.FilesMatch,
            devices = catalog.GetAppleDevices(includeMetadata: false).Where(device => device.IsPresent)
                .Select(device => new { serial = device.Serial, captureFilter = device.HasLibUsb0Filter })
        } });
    }
    private static void Complete(bool success, string message, bool requiresReplug = false) => Emit(new { kind = "complete", success, message, requiresReplug });
    private static void Emit(object value) { Console.WriteLine(JsonSerializer.Serialize(value)); Console.Out.Flush(); }
    private sealed class InlineProgress(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }
}
