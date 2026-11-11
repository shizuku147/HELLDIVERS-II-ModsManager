using System.IO;
using HD2MM.Models;

namespace HD2MM.Services;

public static class LayerIndex
{
    public static List<LayerFile> Scan(string dataDir)
    {
        var list = new List<LayerFile>();
        if (!Directory.Exists(dataDir)) return list;

        foreach (var f in Directory.EnumerateFiles(dataDir))
        {
            var lf = LayerFile.TryParse(f);
            if (lf is not null) list.Add(lf);
        }

        return list;
    }

    public static (int Layers, long Bytes) Summary(string dataDir)
    {
        var all = Scan(dataDir);
        var layers = all.Count(x => x.Kind == LayerFile.KindData);
        long bytes = 0;
        foreach (var f in all)
        {
            try { bytes += new FileInfo(f.Path).Length; } catch { /* ignore */ }
        }
        return (layers, bytes);
    }

    public static string HumanSize(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / 1024d / 1024d / 1024d:0.00} GB",
        >= 1L << 20 => $"{bytes / 1024d / 1024d:0.0} MB",
        >= 1L << 10 => $"{bytes / 1024d:0.0} KB",
        _ => $"{bytes} B",
    };
}
