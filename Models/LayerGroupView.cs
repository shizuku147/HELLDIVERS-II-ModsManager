using HD2MM.Services;

namespace HD2MM.Models;

public sealed record LayerGroupView(string Bundle, int Layers, long Bytes)
{
    public string Sources { get; init; } = "";
    public bool HasConflict { get; init; }

    public string SizeText => LayerIndex.HumanSize(Bytes);
    public string LayerText => $"{Layers} 层";
    public string ConflictText => HasConflict ? "⚠ 多层覆盖" : "";
}
