using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace HD2MM.Services;

public sealed record NexusInfo(int ModId, string Name, string Summary, string Version, string PictureUrl, string Description, int CategoryId, long UpdatedTimestamp);

public static class NexusClient
{
    public const string GameDomain = "helldivers2";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };

    public static async Task<NexusInfo?> GetModAsync(int modId, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.nexusmods.com/v1/games/{GameDomain}/mods/{modId}.json");
            req.Headers.TryAddWithoutValidation("apikey", apiKey);

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var text = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            string Str(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() ?? ""
                    : "";

            int Int(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                    ? v.GetInt32()
                    : 0;

            long Long(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                    ? v.GetInt64()
                    : 0;

            return new NexusInfo(modId, Str("name"), Str("summary"), Str("version"),
                Str("picture_url"), Str("description"), Int("category_id"), Long("updated_timestamp"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Nexus category id -> display name for this game.</summary>
    public static async Task<Dictionary<int, string>> GetCategoriesAsync(string? apiKey)
    {
        var map = new Dictionary<int, string>();
        if (string.IsNullOrWhiteSpace(apiKey)) return map;

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://api.nexusmods.com/v1/games/{GameDomain}.json");
            req.Headers.TryAddWithoutValidation("apikey", apiKey);

            using var resp = await Http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return map;

            var text = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(text);

            if (!doc.RootElement.TryGetProperty("categories", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
                return map;

            foreach (var el in arr.EnumerateArray())
            {
                if (el.TryGetProperty("category_id", out var id) &&
                    id.ValueKind == JsonValueKind.Number &&
                    el.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String)
                {
                    map[id.GetInt32()] = name.GetString() ?? "";
                }
            }
        }
        catch
        {
            // offline / no access: empty map
        }

        return map;
    }

    public static async Task<string?> DownloadIconAsync(NexusInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.PictureUrl)) return null;

        var path = Path.Combine(AppState.Dir, "icons", $"{info.ModId}.png");
        if (File.Exists(path)) return path;

        try
        {
            var bytes = await Http.GetByteArrayAsync(info.PictureUrl);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static string? CachedIconPath(string? nexusId) =>
        int.TryParse(nexusId, out var id) && File.Exists(Path.Combine(AppState.Dir, "icons", $"{id}.png"))
            ? Path.Combine(AppState.Dir, "icons", $"{id}.png")
            : null;
}
