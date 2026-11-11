using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using HD2MM.Models;

namespace HD2MM.Services;

public static class ModScanner
{
    public static List<ModItem> Scan(string modsDir)
    {
        var list = new List<ModItem>();
        if (string.IsNullOrWhiteSpace(modsDir) || !Directory.Exists(modsDir)) return list;

        foreach (var dir in Directory.GetDirectories(modsDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var folderName = Path.GetFileName(dir);
            var manifestPath = Path.Combine(dir, "manifest.json");
            string? name = null, desc = null, guid = null;
            var options = new List<ModOption>();
            var hasManifest = File.Exists(manifestPath);

            if (hasManifest)
            {
                try
                {
                    using var doc = JsonDocument.Parse(
                        File.ReadAllText(manifestPath),
                        new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String) name = n.GetString();
                    if (root.TryGetProperty("Description", out var d) && d.ValueKind == JsonValueKind.String) desc = d.GetString();
                    if (root.TryGetProperty("Guid", out var g) && g.ValueKind == JsonValueKind.String) guid = g.GetString();
                    if (root.TryGetProperty("Options", out var o) && o.ValueKind == JsonValueKind.Array) options = ParseOptions(o);
                }
                catch
                {
                    // broken manifest -> treat as legacy package
                }
            }

            list.Add(new ModItem
            {
                Folder = dir,
                Name = string.IsNullOrWhiteSpace(name) ? folderName : name!,
                Description = desc ?? "",
                Guid = guid ?? "",
                NexusId = NexusIdParser.Parse(folderName),
                Options = options,
                HasManifest = hasManifest,
                AddedTime = Directory.GetCreationTime(dir),
            });
        }

        return list;
    }

    private static List<ModOption> ParseOptions(JsonElement arr)
    {
        var res = new List<ModOption>();

        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString() ?? "";
                res.Add(new ModOption { Name = s, Include = new List<string> { s } });
                continue;
            }

            if (el.ValueKind != JsonValueKind.Object) continue;

            var opt = new ModOption
            {
                Name = el.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "",
                Include = new List<string>(),
            };

            if (el.TryGetProperty("Include", out var inc) && inc.ValueKind == JsonValueKind.Array)
                foreach (var i in inc.EnumerateArray())
                    if (i.ValueKind == JsonValueKind.String) opt.Include.Add(i.GetString()!);

            if (el.TryGetProperty("SubOptions", out var sub) && sub.ValueKind == JsonValueKind.Array)
                opt.SubOptions.AddRange(ParseOptions(sub));

            res.Add(opt);
        }

        return res;
    }
}

public static class NexusIdParser
{
    // 3..6 digits, not glued to letters (416GP / 74M / 40K are skipped), excluding year-like tokens.
    private static readonly Regex Token = new(@"(?<![A-Za-z0-9])(\d{3,6})(?![A-Za-z0-9])", RegexOptions.Compiled);

    public static string? Parse(string text)
    {
        foreach (Match m in Token.Matches(text))
        {
            var value = m.Value;
            if (value.Length == 4 && int.TryParse(value, out var v) && v >= 2000 && v <= 2035) continue;
            return value;
        }
        return null;
    }
}
