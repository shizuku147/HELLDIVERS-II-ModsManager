using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using HD2MM.Models;

namespace HD2MM.Services;

public sealed record DeployLayer(string Bundle, int Index, string Mod, long Size);

public sealed record DeployResult(
    int Layers,
    long Bytes,
    int Linked,
    int Copied,
    string Mode,
    string Message,
    List<DeployLayer> Sources);

public static class DeployService
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    private static readonly string[] GameProcesses = { "helldivers2", "start_protected_game" };

    public static bool IsGameRunning() =>
        GameProcesses.Any(p => Process.GetProcessesByName(p).Length > 0);

    public static DeployResult Deploy(string gameDir, IReadOnlyList<ModItem> mods)
    {
        var data = Path.Combine(gameDir, "data");
        if (!Directory.Exists(data)) throw new DirectoryNotFoundException($"找不到游戏 data 目录：{data}");
        if (IsGameRunning()) throw new InvalidOperationException("游戏正在运行，先关掉再应用。");

        // 1. clear previous layers
        foreach (var lf in LayerIndex.Scan(data)) TryDelete(lf.Path);

        // 2. collect candidate files (dedupe: parent and sub option dirs can overlap)
        var items = new List<(string Bundle, int ModOrder, int OrigIndex, string Kind, string SourcePath, string ModName, string SourceDir)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var modOrder = 0;

        foreach (var mod in mods)
        {
            foreach (var dir in mod.SelectedDirectories())
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    if (!seen.Add(file)) continue;

                    var lf = LayerFile.TryParse(file);
                    if (lf is null) continue;
                    items.Add((lf.Bundle, modOrder, lf.Index, lf.Kind, file, mod.Name, Path.GetDirectoryName(file) ?? ""));
                }
            }
            modOrder++;
        }

        // 3. stable order, then renumber per bundle
        var ordered = items
            .OrderBy(i => i.Bundle, StringComparer.Ordinal)
            .ThenBy(i => i.ModOrder)
            .ThenBy(i => IsBaseLayerDir(i.SourceDir) ? 0 : 1)   // shared material / base first
            .ThenBy(i => i.SourceDir, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.OrigIndex)
            .ThenBy(i => i.Kind, StringComparer.Ordinal)
            .ToList();

        string? curBundle = null;
        var curMod = -1;
        var curDir = "";
        var curOrig = -1;
        var index = -1;
        int layers = 0, linked = 0, copied = 0;
        long bytes = 0;
        var mode = "hardlink";
        var sources = new List<DeployLayer>();

        foreach (var it in ordered)
        {
            if (it.Bundle != curBundle)
            {
                curBundle = it.Bundle;
                curMod = -1;
                curDir = "";
                curOrig = -1;
                index = -1;
            }

            // a new layer starts when the mod, the option folder or the original patch index changes
            if (it.ModOrder != curMod ||
                !string.Equals(it.SourceDir, curDir, StringComparison.OrdinalIgnoreCase) ||
                it.OrigIndex != curOrig)
            {
                index++;
                curMod = it.ModOrder;
                curDir = it.SourceDir;
                curOrig = it.OrigIndex;
            }

            var suffix = it.Kind == LayerFile.KindData ? "" : "." + it.Kind;
            var dest = Path.Combine(data, $"{it.Bundle}.patch_{index}{suffix}");

            if (CreateHardLinkW(dest, it.SourcePath, IntPtr.Zero))
            {
                linked++;
            }
            else
            {
                File.Copy(it.SourcePath, dest, overwrite: true);
                copied++;
                mode = "copy";
            }

            bytes += new FileInfo(it.SourcePath).Length;
            if (it.Kind == LayerFile.KindData)
            {
                layers++;
                sources.Add(new DeployLayer(it.Bundle, index, it.ModName, new FileInfo(it.SourcePath).Length));
            }
        }

        var note = mode == "hardlink"
            ? "全部使用硬链接（零拷贝）"
            : $"部分文件跨盘，{copied} 个走了复制";

        var result = new DeployResult(layers, bytes, linked, copied, mode, note, sources);
        WriteJournal(gameDir, result);
        return result;
    }

    /// <summary>Shared material / base folders must be applied before the variants that use them.</summary>
    private static bool IsBaseLayerDir(string dir)
    {
        var name = Path.GetFileName(dir);
        return name.Contains("material", StringComparison.OrdinalIgnoreCase)
               || name.Contains("do not disable", StringComparison.OrdinalIgnoreCase)
               || name.Contains("base", StringComparison.OrdinalIgnoreCase)
               || name.Contains("common", StringComparison.OrdinalIgnoreCase)
               || name.Contains("shared", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* locked file: leave it */ }
    }

    private static void WriteJournal(string gameDir, DeployResult r)
    {
        try
        {
            Directory.CreateDirectory(AppState.Dir);
            var payload = new
            {
                time = DateTime.Now.ToString("s"),
                gameDir,
                layers = r.Layers,
                bytes = r.Bytes,
                linked = r.Linked,
                copied = r.Copied,
                mode = r.Mode,
            };
            File.WriteAllText(Path.Combine(AppState.Dir, "last-deploy.json"),
                JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

            File.WriteAllText(Path.Combine(AppState.Dir, "layers.json"),
                JsonSerializer.Serialize(r.Sources, new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { /* journal is best effort */ }
    }
}
