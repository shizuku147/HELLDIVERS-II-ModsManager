using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HD2MM.Models;

/// <summary>One in-game equipment code and which mods touch it.</summary>
public class WeaponUsage : INotifyPropertyChanged
{
    public string Code { get; init; } = "";

    /// <summary>武器 / 护甲 / 战备 … (user tag, defaults to the Nexus category).</summary>
    private string _tag = "";
    public string Tag
    {
        get => _tag;
        set
        {
            if (_tag == value) return;
            _tag = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TagText));
            OnPropertyChanged(nameof(TagBrush));
        }
    }

    public string TagText => string.IsNullOrWhiteSpace(Tag) ? "未分类" : Tag;

    public List<string> Owners { get; init; } = new();

    public bool IsOccupied => Owners.Count > 0;

    public string OwnerText => IsOccupied ? string.Join("、", Owners) : "空闲";

    public string StatusText => Owners.Count > 1
        ? $"冲突（{Owners.Count} 个模组）"
        : IsOccupied ? "已占用" : "空闲";

    public string StatusBrush => IsOccupied ? "#E5484D" : "#4CAF50";

    public string TagBrush => Tag switch
    {
        "护甲" => "#C792EA",
        "武器" => "#FF9F0A",

        _ => "#8A8A8A",
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
