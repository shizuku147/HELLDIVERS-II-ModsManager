using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HD2MM.Models;

/// <summary>A flattened, checkable entry of a mod's option tree.</summary>
public class OptionEntry : INotifyPropertyChanged
{
    public string Label { get; init; } = "";

    /// <summary>Relative include path inside the mod folder.</summary>
    public string Path { get; init; } = "";

    /// <summary>0 = top level option, 1 = sub option.</summary>
    public int Level { get; init; }

    public string Display => Level > 0 ? "    └ " + Label : Label;

    /// <summary>Equipment codes this entry targets.</summary>
    public string TargetCodes { get; set; } = "";

    /// <summary>Names of other enabled mods already touching the same equipment.</summary>
    public string OccupiedBy { get; set; } = "";

    public bool IsOccupied => !string.IsNullOrEmpty(OccupiedBy);

    public string OccupiedText => IsOccupied ? $"已被占用：{OccupiedBy}" : "";

    private bool _isChecked;
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
