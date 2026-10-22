using System.IO.Compression;
using System.Security.Cryptography;

namespace WiredMirror.Updates;

internal static class UpdateInstallation
{
    public const string Marker = ".wiredmirror-install";
    public const string MarkerText = "WiredMirror portable installation v1";

    public static void ValidateTarget(string target)
    {
        var full = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if (string.Equals(full, Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(full, "WiredMirror.exe"))
            || !File.Exists(Path.Combine(full, Marker)) || File.ReadAllText(Path.Combine(full, Marker)).Trim() != MarkerText)
            throw new InvalidOperationException("The destination is not a WiredMirror portable installation.");
        for (DirectoryInfo? directory = new(full); directory != null; directory = directory.Parent)
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Update through linked directories is not supported. Extract WiredMirror into a regular folder.");
    }

    public static string ExtractVerified(string zip, string hash, string stage)
    {
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidOperationException("Invalid update checksum.");
        using var file = new FileStream(zip, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (!Convert.ToHexString(SHA256.HashData(file)).Equals(hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The update download did not match its SHA-256 checksum.");
        file.Position = 0;
        using var archive = new ZipArchive(file, ZipArchiveMode.Read);
        var root = Path.GetFullPath(stage).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Directory.Exists(root)) throw new InvalidOperationException("Update staging folder already exists.");
        Directory.CreateDirectory(root);
        long total = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (archive.Entries.Count > 2000) throw new InvalidOperationException("Too many files in update.");
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('\\', '/');
            if (!relative.StartsWith("WiredMirror/", StringComparison.Ordinal) || relative.Contains(':')
                || relative.Split('/').Any(part => part is ".." or ".")
                || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidOperationException("Invalid path in update archive.");
            var destination = Path.GetFullPath(Path.Combine(root, relative));
            if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !seen.Add(destination))
                throw new InvalidOperationException("Invalid or duplicate path in update archive.");
            total = checked(total + entry.Length);
            if (total > 1024L * 1024 * 1024) throw new InvalidOperationException("Update exceeds its size limit.");
            if (relative.EndsWith('/')) Directory.CreateDirectory(destination);
            else { Directory.CreateDirectory(Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination); }
        }
        var app = Path.Combine(root, "WiredMirror");
        ValidateTarget(app);
        foreach (var required in new[] { "WiredMirror.dll", "iPhoneMirror.Core.dll", "WiredMirror.Update.exe", "Wireless/iPhoneMirror.WirelessHost.exe", "Setup/WiredMirror.Setup.exe" })
            if (!File.Exists(Path.Combine(app, required))) throw new InvalidOperationException("The update is missing required app components.");
        return app;
    }

    public static string Replace(string target, string staged, Action<string, string>? move = null)
    {
        target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        staged = Path.GetFullPath(staged).TrimEnd(Path.DirectorySeparatorChar);
        ValidateTarget(target); ValidateTarget(staged);
        if (staged.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || target.StartsWith(staged + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(staged, target, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Update staging must be outside the installation.");
        move ??= Directory.Move;
        var backup = target + ".backup-" + Guid.NewGuid().ToString("N");
        move(target, backup);
        try { move(staged, target); }
        catch
        {
            Directory.Move(backup, target);
            throw;
        }
        return backup; // Retain the complete previous installation for recovery.
    }
}
