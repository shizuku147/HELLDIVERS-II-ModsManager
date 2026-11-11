using System.IO;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using HD2MM.Services;

namespace HD2MM.Services;

public static class ArchiveImporter
{
    private static readonly string[] Allowed = { ".zip", ".rar", ".7z" };

    public static List<string> Import(IEnumerable<string> archives, string modsDir)
    {
        var created = new List<string>();
        Directory.CreateDirectory(modsDir);

        foreach (var file in archives)
        {
            if (!File.Exists(file)) continue;

            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (!Allowed.Contains(ext)) continue;

            var name = Sanitize(Path.GetFileNameWithoutExtension(file));
            var dest = Path.Combine(modsDir, name);

            // Replace in place: keep the previous Guid so enabled/options/notes survive an update.
            string? oldGuid = null;
            if (Directory.Exists(dest))
            {
                oldGuid = TryReadGuid(Path.Combine(dest, "manifest.json"));

                try
                {
                    Directory.Delete(dest, recursive: true);
                }
                catch
                {
                    // locked (game running / antivirus): move it aside instead of leaving stale files
                    var trash = dest + ".old-" + DateTime.Now.ToString("HHmmss");
                    Directory.Move(dest, trash);
                }
            }

            Directory.CreateDirectory(dest);
            try
            {
                if (ext == ".zip") ZipFile.ExtractToDirectory(file, dest, overwriteFiles: true);
                else ExtractWithTar(file, dest);

                EnsureManifest(dest, oldGuid);
                created.Add(dest);
            }
            catch
            {
                TryDeleteDir(dest);
                throw;
            }
        }

        return created;
    }

    /// <summary>
    /// Old mods ship no manifest and use sub folder names as options.
    /// Generate a legacy manifest so the option picker works for them too.
    /// </summary>
    private static void EnsureManifest(string dest, string? fallbackGuid = null)
    {
        if (File.Exists(Path.Combine(dest, "manifest.json"))) return;

        var subDirs = Directory.GetDirectories(dest)
            .Select(d => Path.GetFileName(d))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (subDirs.Length == 0) return;

        var manifest = new
        {
            Guid = string.IsNullOrWhiteSpace(fallbackGuid) ? Guid.NewGuid().ToString() : fallbackGuid,
            Name = Path.GetFileName(dest),
            Description = "A locally imported mod.",
            Options = subDirs,
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dest, "manifest.json"), json);
    }

    private static string? TryReadGuid(string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath)) return null;

            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return doc.RootElement.TryGetProperty("Guid", out var g) && g.ValueKind == JsonValueKind.String
                ? g.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static void ExtractWithTar(string archive, string dest)
    {
        // Windows ships bsdtar (libarchive) which reads rar/7z.
        var psi = new ProcessStartInfo("tar")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-xf");
        psi.ArgumentList.Add(archive);
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(dest);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("无法启动 tar 解压器。");
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new InvalidOperationException($"解压失败（{Path.GetFileName(archive)}）：{err.Trim()}");
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim().TrimEnd('.');
        if (name.Length > 80) name = name[..80];
        return string.IsNullOrWhiteSpace(name) ? "mod" : name;
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { }
    }
}
