using System.IO;
using System.Text.Json;

namespace HD2MM.Services;

public class AppSettings
{
    public string GameDir { get; set; } = @"E:\SteamLibrary\steamapps\common\Helldivers 2";
    public string ModsDir { get; set; } = "";
    public Dictionary<string, bool> Enabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Notes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Per-mod selected include paths (empty/absent = default everything).</summary>
    public Dictionary<string, string[]> Choices { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Nexus version recorded when the mod was first checked (update baseline).</summary>
    public Dictionary<string, string> NexusVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Versions the user chose to ignore.</summary>
    public Dictionary<string, string> IgnoredUpdates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>User tags for equipment codes: 护甲 / 武器 / 战备 …</summary>
    public Dictionary<string, string> Tags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Unix timestamp of the last seen Nexus file update, per mod.</summary>
    public Dictionary<string, long> NexusUpdated { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Equipment codes the user hid from the occupancy view.</summary>
    public HashSet<string> HiddenCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>State key -> Nexus category name (persisted so Kind survives restarts).</summary>
    public Dictionary<string, string> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Folder path -> "fileCount:totalBytes" fingerprint recorded at import time.</summary>
    public Dictionary<string, string> Fingerprints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Close button behaviour: hide to tray instead of exiting.</summary>
    public bool CloseToTray { get; set; }
}

public static class AppState
{
    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HD2MM");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Json);
                if (s is not null) return s;
            }
        }
        catch { /* fall through to defaults */ }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Json));
        }
        catch { /* settings are best effort */ }
    }
}
