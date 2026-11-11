using System.Text.RegularExpressions;

namespace HD2MM.Models;

/// <summary>One physical patch file belonging to a bundle layer.</summary>
public sealed record LayerFile(string Bundle, int Index, string Kind, string Path)
{
    public const string KindData = "data";
    public const string KindStream = "stream";
    public const string KindGpu = "gpu_resources";

    public static readonly Regex Pattern = new(
        @"^(?<b>[0-9a-f]{16})\.patch_(?<n>\d+)(?:\.(?<k>stream|gpu_resources))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static LayerFile? TryParse(string fullPath)
    {
        var m = Pattern.Match(System.IO.Path.GetFileName(fullPath));
        if (!m.Success) return null;

        return new LayerFile(
            m.Groups["b"].Value.ToLowerInvariant(),
            int.Parse(m.Groups["n"].Value),
            m.Groups["k"].Success ? m.Groups["k"].Value : KindData,
            fullPath);
    }
}
