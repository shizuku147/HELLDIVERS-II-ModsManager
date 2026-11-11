using System.Text.RegularExpressions;
using HD2MM.Models;

namespace HD2MM.Services;

/// <summary>
/// Extracts in-game equipment codes (SR-24, AR-15, DS-191 …) from mod text,
/// then works out who occupies what.
/// </summary>
public static class TargetParser
{
    private static readonly Regex WithDash = new(
        @"(?<![A-Za-z0-9])[A-Z]{1,3}-\d{1,3}(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    // "AR15" / "DP8" style — only for known equipment prefixes to avoid noise like "GFL2"
    private static readonly Regex NoDash = new(
        @"(?<![A-Za-z0-9])(AR|SMG|MP|SG|BR|MG|APW|RS|EAT|GR|FAF|RL|MLS|LAS|AC|ARC|DS|SR|CE|CM|CPG|DP|IE|FS|SA|PH|UF|HX|CB|EX|TD|MS)(\d{1,3})(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    // loose form for user-written notes: "CPH26", "SR24", "AR-15" …
    private static readonly Regex Loose = new(
        @"(?<![A-Za-z0-9])([A-Z]{2,4})-?(\d{1,3})(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    public static HashSet<string> Extract(string? text, HashSet<string>? into = null)
    {
        var set = into ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return set;

        foreach (Match m in WithDash.Matches(text))
            set.Add(m.Value.ToUpperInvariant());

        foreach (Match m in NoDash.Matches(text))
            set.Add($"{m.Groups[1].Value}-{m.Groups[2].Value}".ToUpperInvariant());

        return set;
    }

    /// <summary>Loose extraction used for user notes (any 2-4 letter prefix).</summary>
    public static HashSet<string> ExtractLoose(string? text, HashSet<string>? into = null)
    {
        var set = into ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return set;

        foreach (Match m in Loose.Matches(text))
            set.Add($"{m.Groups[1].Value}-{m.Groups[2].Value}".ToUpperInvariant());

        return set;
    }

    /// <summary>code -> mods that touch it.</summary>
    public static Dictionary<string, List<ModItem>> BuildUsage(IEnumerable<ModItem> mods)
    {
        var map = new Dictionary<string, List<ModItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            foreach (var code in mod.Targets)
            {
                if (!map.TryGetValue(code, out var list))
                    map[code] = list = new List<ModItem>();

                list.Add(mod);
            }
        }

        return map;
    }
}
