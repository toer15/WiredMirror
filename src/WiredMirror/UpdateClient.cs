using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace WiredMirror;

internal sealed record AppRelease(Version Version, string Tag, Uri Download, string AssetName, string Sha256, Uri Page);
internal sealed record DownloadedUpdate(string Archive, string Sha256);

internal static class UpdateClient
{
    public const string Repository = "toer15/WiredMirror";
    private static readonly Version assemblyVersion = typeof(UpdateClient).Assembly.GetName().Version!;
    public static readonly Version Current = new(assemblyVersion.Major, assemblyVersion.Minor, assemblyVersion.Build);
    private static readonly HttpClient client = CreateClient();
    private static HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WiredMirror/" + Current.ToString(3));
        return http;
    }

    public static async Task<AppRelease?> Latest(CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", deadline.Token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(deadline.Token);
        var release = Parse(json);
        if (release == null) return null;
        return release;
    }

    // Only stable, complete releases with GitHub's SHA-256 asset digest qualify.
    internal static AppRelease? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version <= Current) return null;
        var name = $"WiredMirror-{version.ToString(3)}-windows-x64.zip";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name || asset.GetProperty("state").GetString() != "uploaded") continue;
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com"
                || uri.AbsolutePath != $"/{Repository}/releases/download/{tag}/{name}")
                throw new InvalidOperationException("The release download URL does not match this app's GitHub repository.");
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() ?? "" : "";
            if (!digest.StartsWith("sha256:") || digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
                throw new InvalidOperationException("GitHub has not supplied a SHA-256 digest for this release. Try again later.");
            return new(version, tag, uri, name, digest[7..], new Uri($"https://github.com/{Repository}/releases/tag/{tag}"));
        }
        return null;
    }

    public static async Task<DownloadedUpdate> Download(AppRelease release, IProgress<string> progress, CancellationToken cancellation)
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiredMirror", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var archive = Path.Combine(folder, release.AssetName);
        try
        {
            using var response = await client.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 512L * 1024 * 1024) throw new InvalidOperationException("Update download is too large.");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long count = 0;
                var reported = Stopwatch.StartNew();
                int read;
                while ((read = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    count += read;
                    if (count > 512L * 1024 * 1024) throw new InvalidOperationException("Update download exceeds its size limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                    if (reported.ElapsedMilliseconds >= 200) { progress.Report($"Downloading {count / 1048576.0:0.0} MB..."); reported.Restart(); }
                }
            }
            progress.Report("Verifying update checksum...");
            await using var file = File.OpenRead(archive);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellation));
            if (!hash.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Update checksum mismatch. The download was not installed.");
            return new(archive, hash);
        }
        catch { if (File.Exists(archive)) File.Delete(archive); throw; }
    }

    public static void LaunchInstaller(DownloadedUpdate update)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "WiredMirror.Update.exe");
        if (!File.Exists(helper)) throw new InvalidOperationException("The update helper is missing. Extract the full app package.");
        var tools = Path.Combine(Path.GetDirectoryName(update.Archive)!, "tools");
        Directory.CreateDirectory(tools);
        var copy = Path.Combine(tools, "WiredMirror.Update.exe");
        File.Copy(helper, copy, true);
        using var current = Process.GetCurrentProcess();
        var info = new ProcessStartInfo(copy) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = tools };
        foreach (var value in new[] { AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar), update.Archive, update.Sha256,
            current.Id.ToString(), current.StartTime.ToUniversalTime().Ticks.ToString() }) info.ArgumentList.Add(value);
        using var started = Process.Start(info) ?? throw new InvalidOperationException("The update helper could not start.");
    }
}

internal sealed class UpdatesWindow : Form
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Label message = new() { Dock = DockStyle.Fill, Padding = new Padding(18), Text = "Checking GitHub releases..." };
    private readonly Button install = new() { Text = "Download, install and restart", AutoSize = true, Enabled = false };
    private readonly Button cancel = new() { Text = "Close", AutoSize = true };
    private AppRelease? release;
    private bool downloading;
    public DownloadedUpdate? Downloaded { get; private set; }

    public UpdatesWindow()
    {
        Text = "WiredMirror updates"; ClientSize = new Size(570, 220); StartPosition = FormStartPosition.CenterParent;
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(8) };
        buttons.Controls.AddRange([install, cancel]); Controls.Add(message); Controls.Add(buttons);
        cancel.Click += (_, _) => Close();
        FormClosing += (_, _) => cancellation.Cancel();
        Shown += async (_, _) =>
        {
            try
            {
                release = await UpdateClient.Latest(cancellation.Token);
                if (IsDisposed) return;
                message.Text = release == null ? $"WiredMirror {UpdateClient.Current.ToString(3)} is up to date.\r\n\r\nStable releases: github.com/{UpdateClient.Repository}"
                    : $"WiredMirror {release.Version.ToString(3)} is available. Installed: {UpdateClient.Current.ToString(3)}.\r\n\r\nThe app verifies GitHub's SHA-256 checksum, stops mirroring and restarts with the new version. Your previous app folder is kept as a backup.";
                install.Enabled = release != null;
            }
            catch (OperationCanceledException) { if (!IsDisposed) message.Text = "Update check timed out or was cancelled. Close and try again."; }
            catch (Exception error) { if (!IsDisposed) message.Text = "Could not check updates: " + error.Message; }
        };
        install.Click += async (_, _) =>
        {
            if (release == null || downloading) return;
            downloading = true; install.Enabled = false; cancel.Text = "Cancel download";
            try
            {
                Downloaded = await UpdateClient.Download(release, new Progress<string>(text => { if (!IsDisposed) message.Text = text; }), cancellation.Token);
                if (!IsDisposed) { DialogResult = DialogResult.OK; Close(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { if (!IsDisposed) { message.Text = error.Message; install.Enabled = true; } }
            finally { downloading = false; if (!IsDisposed) cancel.Text = "Close"; }
        };
    }
    protected override void Dispose(bool disposing) { if (disposing) { cancellation.Cancel(); cancellation.Dispose(); } base.Dispose(disposing); }
}
