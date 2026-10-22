using System.Diagnostics;
using WiredMirror.Updates;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        string? target = null;
        try
        {
            if (args.Length != 5) throw new InvalidOperationException("Start updates from WiredMirror's Check for updates window.");
            target = Path.GetFullPath(args[0]);
            UpdateInstallation.ValidateTarget(target);
            var archive = Path.GetFullPath(args[1]);
            if (archive.StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The update archive must be outside the app folder.");
            // Extract beside the target so directory replacement stays on the same volume.
            var stage = target + ".update-" + Guid.NewGuid().ToString("N");
            var staged = UpdateInstallation.ExtractVerified(archive, args[2], stage);
            Process? old = null;
            try { old = Process.GetProcessById(int.Parse(args[3])); }
            catch (ArgumentException) { }
            if (old != null)
            {
                using (old)
                {
                    if (old.StartTime.ToUniversalTime().Ticks != long.Parse(args[4]))
                        throw new InvalidOperationException("The app process changed. Retry the update from WiredMirror.");
                    if (!old.WaitForExit(120000)) throw new InvalidOperationException("WiredMirror did not close. The update was not installed.");
                }
            }
            string? backup = null;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                try { backup = UpdateInstallation.Replace(target, staged); break; }
                catch (IOException) when (attempt < 29) { Thread.Sleep(1000); }
            }
            File.WriteAllText(Path.Combine(target, "update-result.txt"), "Update installed. Previous version: " + backup);
            Process.Start(new ProcessStartInfo(Path.Combine(target, "WiredMirror.exe")) { UseShellExecute = true, WorkingDirectory = target });
            return 0;
        }
        catch (Exception error)
        {
            MessageBox.Show("The update could not finish. Your previous installation has been kept.\r\n\r\n" + error.Message,
                "WiredMirror update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (target != null && File.Exists(Path.Combine(target, "WiredMirror.exe")))
                Process.Start(new ProcessStartInfo(Path.Combine(target, "WiredMirror.exe")) { UseShellExecute = true, WorkingDirectory = target });
            return 1;
        }
    }
}
