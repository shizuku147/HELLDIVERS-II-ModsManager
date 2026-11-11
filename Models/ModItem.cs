using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HD2MM.Models;

public class ModOption
{
    public string Name { get; init; } = "";
    public List<string> Include { get; init; } = new();
    public List<ModOption> SubOptions { get; init; } = new();
}

/// <summary>A single mod in the library.</summary>
public class ModItem : INotifyPropertyChanged
{
    public string Folder { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Guid { get; init; } = "";
    public string? NexusId { get; init; }
    public List<ModOption> Options { get; init; } = new();
    public bool HasManifest { get; init; }

    /// <summary>When this mod folder was created (≈ archive import time).</summary>
    public DateTime AddedTime { get; init; } = DateTime.MinValue;

    private string _categoryName = "";
    /// <summary>Nexus category (Weapons / Armour / Audio …), empty when unknown.</summary>
    public string CategoryName
    {
        get => _categoryName;
        set
        {
            if (_categoryName == value) return;
            _categoryName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CategoryText));
        }
    }

    public string CategoryText => string.IsNullOrWhiteSpace(CategoryName) ? "" : CategoryName;

    /// <summary>Rough kind used by the occupancy view: 武器 / 护甲 / 外观 / 音效 / 装备.</summary>
    public string Kind
    {
        get
        {
            var c = CategoryName.ToLowerInvariant();
            if (c.Contains("armour") || c.Contains("armor")) return "护甲";
            if (c.Contains("weapon")) return "武器";
            if (c.Contains("visual")) return "外观";
            if (c.Contains("audio")) return "音效";
            return "装备";
        }
    }

    public string Initials =>
        string.IsNullOrWhiteSpace(Name) ? "?" : Name[..1].ToUpperInvariant();

    public string NexusText =>
        string.IsNullOrWhiteSpace(NexusId) ? "本地模组" : $"Nexus #{NexusId}";

    public string MetaText =>
        HasManifest ? $"{Options.Count} 个选项" : "无 manifest（整包）";

    public string MetaLine => $"{NexusText}  ·  {MetaText}";

    public bool HasNexus => !string.IsNullOrWhiteSpace(NexusId);

    private bool _isSuspect;
    /// <summary>Folder contents no longer match the fingerprint recorded at import.</summary>
    public bool IsSuspect
    {
        get => _isSuspect;
        set
        {
            if (_isSuspect == value) return;
            _isSuspect = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SuspectText));
        }
    }

    public string SuspectText => IsSuspect ? "⚠ 文件不完整，建议重新导入" : "";

    public string StatusText => IsEnabled ? "已启用" : "未启用";

    private string _note = "";
    public string Note
    {
        get => _note;
        set
        {
            if (_note == value) return;
            _note = value;
            _targets = null;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CardText));
        }
    }

    /// <summary>Note wins over the manifest description on the card.</summary>
    public string CardText => string.IsNullOrWhiteSpace(Note) ? Description : Note;

    private string? _iconPath;
    public string? IconPath
    {
        get => _iconPath;
        set
        {
            if (_iconPath == value) return;
            _iconPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIcon));
        }
    }

    public bool HasIcon => !string.IsNullOrEmpty(IconPath);

    /// <summary>Stable key used for persisted per-mod state.</summary>
    public string StateKey => string.IsNullOrEmpty(Guid) ? Folder : Guid;

    private static readonly System.Text.RegularExpressions.Regex VerRx =
        new(@"\b(\d+\.\d+(?:\.\d+)?)\b", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Best-effort version parsed from the package/folder name, e.g. "… 1.87 2026-08-12".</summary>
    public string? LocalVersion
    {
        get
        {
            var m = VerRx.Match(Path.GetFileName(Folder));
            return m.Success ? m.Value : null;
        }
    }

    private bool _isEnabled;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private HashSet<string> _checkedIncludes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Explicit selection of include paths; empty means "everything".</summary>
    public HashSet<string> CheckedIncludes
    {
        get => _checkedIncludes;
        set
        {
            _checkedIncludes = value;
            _targets = null;
            _activeTargets = null;
        }
    }

    private HashSet<string>? _activeTargets;

    /// <summary>Codes that are actually deployed (only the selected options).</summary>
    public HashSet<string> ActiveTargets
    {
        get
        {
            if (_activeTargets is not null) return _activeTargets;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!HasNexus || !string.IsNullOrWhiteSpace(Note))
            {
                // a written note is the single source of truth
                if (!string.IsNullOrWhiteSpace(Note))
                    Services.TargetParser.ExtractLoose(Note, set);

                return _activeTargets = set;
            }

            var enabled = CheckedIncludes.Count > 0
                ? CheckedIncludes
                : Options.SelectMany(o => o.Include).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var opt in Options)
            {
                if (opt.Include.Any(enabled.Contains))
                    Services.TargetParser.Extract(opt.Name, set);

                foreach (var sub in opt.SubOptions)
                    if (sub.Include.Any(enabled.Contains))
                        Services.TargetParser.Extract(sub.Name, set);
            }

            // the title often names the replaced item even when an option label does not
            Services.TargetParser.Extract(Name, set);
            Services.TargetParser.Extract(Description, set);

            return _activeTargets = set;
        }
    }

    public bool HasOptions => Options.Count > 0;

    private HashSet<string>? _targets;

    /// <summary>In-game equipment codes this mod touches (SR-24, DS-191 …).</summary>
    public HashSet<string> Targets
    {
        get
        {
            if (_targets is not null) return _targets;

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!HasNexus || !string.IsNullOrWhiteSpace(Note))
            {
                // local mods (and any mod with a note) trust the note only
                if (!string.IsNullOrWhiteSpace(Note))
                    Services.TargetParser.ExtractLoose(Note, set);

                return _targets = set;
            }

            if (HasNexus)
            {
                // Nexus mods without a note: option labels are the in-game names
                Services.TargetParser.Extract(Name, set);
                Services.TargetParser.Extract(Description, set);

                foreach (var opt in Options)
                {
                    Services.TargetParser.Extract(opt.Name, set);
                    foreach (var sub in opt.SubOptions)
                        Services.TargetParser.Extract(sub.Name, set);
                }
            }
            else
            {
                // local mods: only the user note is trusted (folder names are misleading)
                Services.TargetParser.ExtractLoose(Note, set);
            }

            return _targets = set;
        }
    }

    /// <summary>Flattened checkable entries for the options dialog.</summary>
    public List<OptionEntry> BuildOptionEntries()
    {
        var list = new List<OptionEntry>();

        foreach (var opt in Options)
        {
            foreach (var inc in opt.Include)
                list.Add(new OptionEntry { Label = opt.Name, Path = inc, Level = 0 });

            foreach (var sub in opt.SubOptions)
                foreach (var inc in sub.Include)
                    list.Add(new OptionEntry { Label = sub.Name, Path = inc, Level = 1 });
        }

        return list;
    }

    /// <summary>Options the author marked as required (e.g. "Material DO NOT DISABLE").</summary>
    public IEnumerable<string> MandatoryIncludes =>
        Options
            .Where(o => o.Name.Contains("do not disable", StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Include);

    /// <summary>Directories whose .patch_* files should be deployed.</summary>
    public IEnumerable<string> SelectedDirectories()
    {
        if (!HasManifest || Options.Count == 0)
        {
            yield return Folder;
            yield break;
        }

        var includes = CheckedIncludes.Count > 0
            ? CheckedIncludes
            : Options.SelectMany(o => o.Include).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // mandatory shared layers are always deployed, even if the user unticked them
        foreach (var inc in includes.Concat(MandatoryIncludes).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return Path.Combine(Folder, inc.Replace('/', Path.DirectorySeparatorChar));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
