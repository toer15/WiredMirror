using System.Text.Json;

namespace WiredMirror;

internal sealed record AppPreferences(PreviewOrientation Orientation = PreviewOrientation.FollowPhone, bool CheckUpdates = true)
{
    private static readonly string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiredMirror", "preferences.json");
    public static AppPreferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(file));
            return value != null && Enum.IsDefined(value.Orientation) ? value : new();
        }
        catch { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temporary = file + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temporary, JsonSerializer.Serialize(this));
        File.Move(temporary, file, true);
    }
}
