using System.IO;
using HD2MM.Models;

namespace HD2MM.Services;

public static class UpdateChecker
{
    public static string ModUrl(string nexusId) =>
        $"https://www.nexusmods.com/{NexusClient.GameDomain}/mods/{nexusId}?tab=files";

    /// <summary>
    /// Detects updates by comparing the moment the mod was imported locally with the
    /// timestamp Nexus reports for the last file change. Version strings are also
    /// compared when both sides parse as numbers.
    /// </summary>
    public static async Task<List<UpdateInfo>> CheckAsync(
        IEnumerable<ModItem> mods, AppSettings settings, string? apiKey)
    {
        var result = new List<UpdateInfo>();
        if (string.IsNullOrWhiteSpace(apiKey)) return result;

        foreach (var mod in mods.Where(m => int.TryParse(m.NexusId, out _)).ToList())
        {
            if (!int.TryParse(mod.NexusId, out var id)) continue;

            var info = await NexusClient.GetModAsync(id, apiKey);
            if (info is null || string.IsNullOrWhiteSpace(info.Version)) continue;

            var key = mod.StateKey;

            // local side: when the folder was created (≈ when the archive was imported)
            DateTime localTime;
            try
            {
                localTime = Directory.GetCreationTimeUtc(mod.Folder);
                if (localTime.Year < 2000) localTime = Directory.GetLastWriteTimeUtc(mod.Folder);
            }
            catch
            {
                localTime = DateTime.UtcNow;
            }

            var nexusTime = info.UpdatedTimestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds(info.UpdatedTimestamp).UtcDateTime
                : DateTime.MinValue;

            // 5 minute slack covers extraction / clock skew
            var fileUpdated = nexusTime > DateTime.MinValue && nexusTime > localTime.AddMinutes(5);

            var local = mod.LocalVersion;
            var versionChanged = !string.IsNullOrWhiteSpace(local) &&
                                 !string.Equals(local, info.Version, StringComparison.OrdinalIgnoreCase);

            var newerVersion = versionChanged &&
                !(Version.TryParse(local, out var localVer) &&
                  Version.TryParse(info.Version, out var nexusVer) &&
                  nexusVer <= localVer);

            if (!newerVersion && !fileUpdated) continue;

            if (!fileUpdated &&
                settings.IgnoredUpdates.TryGetValue(key, out var ignored) &&
                string.Equals(ignored, info.Version, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new UpdateInfo
            {
                Mod = mod,
                LocalVersion = string.IsNullOrWhiteSpace(local) ? info.Version : local,
                NewVersion = info.Version,
                Url = ModUrl(mod.NexusId!),
                IsFileOnly = !newerVersion,
            });
        }

        AppState.Save(settings);
        return result;
    }
}
