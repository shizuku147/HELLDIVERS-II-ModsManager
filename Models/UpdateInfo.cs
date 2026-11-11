namespace HD2MM.Models;

public sealed class UpdateInfo
{
    public ModItem Mod { get; init; } = new();
    public string LocalVersion { get; init; } = "";
    public string NewVersion { get; init; } = "";
    public string Url { get; init; } = "";

    public string Name => Mod.Name;
    public string VersionText => string.IsNullOrWhiteSpace(LocalVersion)
        ? $"→ v{NewVersion}"
        : $"v{LocalVersion} → v{NewVersion}";
    public string NexusText => Mod.NexusText;

    /// <summary>True when the version string did not change but the file did.</summary>
    public bool IsFileOnly { get; init; }

    public string NoteText => IsFileOnly ? "文件已更新（版本号未变）" : "";

    public bool HasNote => IsFileOnly;
}
