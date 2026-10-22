using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace WiredMirror;

internal sealed record SetupDevice(string Serial, bool CaptureFilter);
internal sealed record SetupState(bool SupportReady, bool ServiceInstalled, bool ServiceRunning,
    bool UsbDriverInstalled, bool CaptureFilesReady, SetupDevice[] Devices)
{
    public bool CaptureReady(string? serial) => SupportReady && CaptureFilesReady && serial != null &&
        Devices.Any(device => DeviceIdentity.Key(device.Serial) == serial && device.CaptureFilter);
}
internal sealed record SetupResult(bool Success, bool RequiresReplug, string Message, SetupState? State);

internal static class UsbSetupClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    internal static string HelperPath => Path.Combine(AppContext.BaseDirectory, "Setup", "WiredMirror.Setup.exe");

    public static async Task<SetupResult> Run(bool install, string? serial, IProgress<string>? progress = null)
    {
        if (!File.Exists(HelperPath)) throw new FileNotFoundException("The USB setup helper is missing. Extract the full app folder.");
        var checksumPath = HelperPath + ".sha256";
        if (!File.Exists(checksumPath)) throw new FileNotFoundException("The USB setup helper's integrity file is missing.");
        var expected = (await File.ReadAllTextAsync(checksumPath)).Trim();
        await using (var stream = File.OpenRead(HelperPath))
            if (!Convert.ToHexString(await SHA256.HashDataAsync(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("USB setup helper checksum mismatch. Re-extract the app folder.");
        var start = new ProcessStartInfo(HelperPath)
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true,
            RedirectStandardError = true, WorkingDirectory = Path.GetDirectoryName(HelperPath)!
        };
        start.ArgumentList.Add(install ? "--install" : "--inspect");
        if (install && serial != null)
        {
            if (serial.Length is < 8 or > 40 || !serial.All(char.IsAsciiLetterOrDigit))
                throw new InvalidOperationException("Select an iPhone with a stable USB ID before installing its capture driver.");
            start.ArgumentList.Add("--serial");
            start.ArgumentList.Add(serial.ToUpperInvariant());
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("USB setup could not start.");
        var errors = process.StandardError.ReadToEndAsync();
        SetupState? state = null;
        bool? success = null;
        var replug = false;
        var message = "";
        while (await process.StandardOutput.ReadLineAsync() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var item = document.RootElement;
            switch (item.GetProperty("kind").GetString())
            {
                case "state": state = item.GetProperty("state").Deserialize<SetupState>(JsonOptions); break;
                case "progress": progress?.Report(item.GetProperty("message").GetString() ?? "Working..."); break;
                case "complete":
                    success = item.GetProperty("success").GetBoolean();
                    replug = item.GetProperty("requiresReplug").GetBoolean();
                    message = item.GetProperty("message").GetString() ?? "Setup finished.";
                    break;
            }
        }
        await process.WaitForExitAsync();
        _ = await errors; // Read concurrently so diagnostics cannot fill and block the child pipe.
        var ok = process.ExitCode == 0 && (success ?? !install) && (!install || success == true);
        if (!ok && message.Length == 0) message = "USB setup did not finish successfully. Exit code: " + process.ExitCode;
        return new SetupResult(ok, replug, message, state);
    }
}
