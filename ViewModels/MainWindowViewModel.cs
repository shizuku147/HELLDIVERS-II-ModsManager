using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using HD2MM.Models;
using HD2MM.Services;
using HD2MM.Views;
using Microsoft.Win32;

namespace HD2MM.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private List<DeployLayer> _lastSources = LoadLayerSources();

    private static List<DeployLayer> LoadLayerSources()
    {
        try
        {
            var path = Path.Combine(AppState.Dir, "layers.json");
            if (!File.Exists(path)) return new();

            return System.Text.Json.JsonSerializer.Deserialize<List<DeployLayer>>(File.ReadAllText(path))
                   ?? new();
        }
        catch
        {
            return new();
        }
    }

    public ObservableCollection<ModItem> Mods { get; } = new();

    public ICommand ApplyCommand { get; }
    public ICommand RescanCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand EditNoteCommand { get; }
    public ICommand EditOptionsCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand ApiKeyCommand { get; }
    public ICommand RestoreVanillaCommand { get; }
    public ICommand ToggleCloseBehaviorCommand { get; }

    public bool CloseToTray
    {
        get => _settings.CloseToTray;
        set
        {
            if (_settings.CloseToTray == value) return;
            _settings.CloseToTray = value;
            AppState.Save(_settings);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CloseBehaviorText));
        }
    }

    public string CloseBehaviorText => _settings.CloseToTray ? "关闭行为：挂后台" : "关闭行为：直接退出";

    public ICollectionView ModsView { get; }

    private string _filter = "";
    public string Filter
    {
        get => _filter;
        set
        {
            if (_filter == value) return;
            _filter = value;
            OnPropertyChanged();
            ModsView.Refresh();
        }
    }

    private ModItem? _selectedMod;
    public ModItem? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (ReferenceEquals(_selectedMod, value)) return;
            _selectedMod = value;
            OnPropertyChanged();
        }
    }

    private string _detailText = "";
    public string DetailText
    {
        get => _detailText;
        set
        {
            if (_detailText == value) return;
            _detailText = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Loads the details pane content (Nexus description when available).</summary>
    public async Task OpenDetailAsync(ModItem mod)
    {
        SelectedMod = mod;
        DetailText = string.IsNullOrWhiteSpace(mod.Note) ? mod.Description : mod.Note;

        if (!int.TryParse(mod.NexusId, out var id)) return;

        var key = SecretStore.Load();
        if (string.IsNullOrWhiteSpace(key)) return;

        var info = await NexusClient.GetModAsync(id, key);
        if (info is not null && !string.IsNullOrWhiteSpace(info.Description))
            DetailText = HtmlToText(info.Description);
    }

    public static string HtmlToText(string src)
    {
        if (string.IsNullOrWhiteSpace(src)) return "";

        var text = src;

        // Nexus descriptions are BBCode, not HTML.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[img\].*?\[/img\]", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[\*\]", "\n• ");
        text = System.Text.RegularExpressions.Regex.Replace(text,
            @"\[/?(b|i|u|s|list|center|left|right|quote|spoiler|size|color|font|url|background|email|youtube)\b[^\]]*\]",
            "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // leftovers + HTML tags
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[/?[^\]]{1,40}\]", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, "<br\\s*/?>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "</p\\s*>", "\n\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, "[ \\t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, "\n{3,}", "\n\n");
        return text.Trim();
    }

    private string _statusLine = "";
    public string StatusLine
    {
        get => _statusLine;
        set { if (_statusLine == value) return; _statusLine = value; OnPropertyChanged(); }
    }

    private string _modsDirText = "";
    public string ModsDirText
    {
        get => _modsDirText;
        set { if (_modsDirText == value) return; _modsDirText = value; OnPropertyChanged(); }
    }

    private string _gameDirText = "";
    public string GameDirText
    {
        get => _gameDirText;
        set { if (_gameDirText == value) return; _gameDirText = value; OnPropertyChanged(); }
    }

    public MainWindowViewModel()
    {
        _settings = AppState.Load();

        ApplyCommand = new RelayCommand(_ => Apply());
        RescanCommand = new RelayCommand(_ => Load());
        ImportCommand = new RelayCommand(_ => ImportArchives());
        EditNoteCommand = new RelayCommand(p => EditNote(p as ModItem));
        EditOptionsCommand = new RelayCommand(p => EditOptions(p as ModItem));
        ApiKeyCommand = new RelayCommand(_ => EditApiKey());
        CheckUpdatesCommand = new RelayCommand(_ => _ = CheckUpdatesAsync(true));
        RestoreVanillaCommand = new RelayCommand(_ => RestoreVanilla());
        ToggleCloseBehaviorCommand = new RelayCommand(_ => CloseToTray = !CloseToTray);
        SetFilterCommand = new RelayCommand(p => SetFilter(p as string));
        InspectLayersCommand = new RelayCommand(_ => InspectLayers());
        InspectOccupancyCommand = new RelayCommand(_ => InspectOccupancy());

        ModsView = CollectionViewSource.GetDefaultView(Mods);
        ModsView.Filter = FilterMod;
        ModsView.SortDescriptions.Add(new SortDescription(
            nameof(ModItem.AddedTime), ListSortDirection.Descending));

        Load();
    }

    public ICommand SetFilterCommand { get; }
    public ICommand InspectLayersCommand { get; }
    public ICommand InspectOccupancyCommand { get; }

    private string _filterMode = "All";
    public string FilterMode
    {
        get => _filterMode;
        set
        {
            if (_filterMode == value) return;
            _filterMode = value;
            OnPropertyChanged();
            ModsView.Refresh();
        }
    }

    private void SetFilter(string? mode)
    {
        FilterMode = string.IsNullOrWhiteSpace(mode) ? "All" : mode;
        StatusLine = FilterMode switch
        {
            "Enabled" => "仅显示已启用的模组",
            "Disabled" => "仅显示未启用的模组",
            "Noted" => "仅显示带备注的模组",
            "Recent" => "仅显示最近 30 天内变动的模组",
            _ => "显示全部模组",
        };
    }

    private void InspectOccupancy()
    {
        new OccupancyDialog(BuildOccupancy(), SaveWeaponTag, DeleteOccupancyEntry, RefreshOccupancy)
        {
            Owner = Application.Current.MainWindow,
        }.ShowDialog();
    }

    private List<WeaponUsage> RefreshOccupancy()
    {
        Load();
        return BuildOccupancy();
    }

    private List<WeaponUsage> DeleteOccupancyEntry(WeaponUsage usage)
    {
        _settings.HiddenCodes.Add(usage.Code);
        AppState.Save(_settings);
        return BuildOccupancy();
    }

    private List<WeaponUsage> BuildOccupancy()
    {
        var codes = Mods
            .SelectMany(m => m.Targets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = codes
            .Select(code =>
            {
                var owners = Mods.Where(m => m.ActiveTargets.Contains(code)).ToList();

                var defaultTag = owners
                    .Select(o => o.Kind)
                    .FirstOrDefault(k => k is "武器" or "护甲") ?? "";

                var tag = _settings.Tags.TryGetValue(code, out var saved) && !string.IsNullOrWhiteSpace(saved)
                    ? saved
                    : defaultTag;

                return new WeaponUsage
                {
                    Code = code,
                    Tag = tag,
                    Owners = owners
                        .Where(m => m.IsEnabled)
                        .Select(m => m.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                };
            })
            .Where(x => !_settings.HiddenCodes.Contains(x.Code))
            .OrderByDescending(x => x.IsOccupied)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return items;
    }

    private void SaveWeaponTag(WeaponUsage usage, string tag)
    {
        usage.Tag = tag;

        if (string.IsNullOrWhiteSpace(tag)) _settings.Tags.Remove(usage.Code);
        else _settings.Tags[usage.Code] = tag;

        AppState.Save(_settings);
    }

    private void InspectLayers()
    {
        var data = Path.Combine(_settings.GameDir, "data");

        var groups = LayerIndex.Scan(data)
            .GroupBy(f => f.Bundle)
            .Select(g =>
            {
                var sources = _lastSources.Where(s => s.Bundle == g.Key).ToList();
                var mods = sources.Select(s => s.Mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                return new LayerGroupView(
                    g.Key,
                    g.Count(f => f.Kind == LayerFile.KindData),
                    g.Sum(f => { try { return new FileInfo(f.Path).Length; } catch { return 0L; } }))
                {
                    Sources = string.Join("、", mods),
                    HasConflict = mods.Count > 1,
                };
            })
            .OrderByDescending(x => x.HasConflict)
            .ThenByDescending(x => x.Bytes)
            .ToList();

        new LayersDialog(groups) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    private static string Key(ModItem m) => m.StateKey;

    /// <summary>"fileCount:totalBytes" — cheap integrity fingerprint.</summary>
    private static string Fingerprint(string folder)
    {
        try
        {
            long bytes = 0;
            var count = 0;

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                count++;
                try { bytes += new FileInfo(file).Length; } catch { /* ignore */ }
            }

            return $"{count}:{bytes}";
        }
        catch
        {
            return "";
        }
    }

    private void Load()
    {
        foreach (var m in Mods) m.PropertyChanged -= OnModChanged;
        Mods.Clear();

        foreach (var m in ModScanner.Scan(_settings.ModsDir))
        {
            if (_settings.Categories.TryGetValue(Key(m), out var cachedCategory))
                m.CategoryName = cachedCategory;

            var fingerprint = Fingerprint(m.Folder);
            if (fingerprint.Length > 0)
            {
                if (_settings.Fingerprints.TryGetValue(m.Folder, out var saved) &&
                    !string.Equals(saved, fingerprint, StringComparison.Ordinal))
                {
                    m.IsSuspect = true;
                }
                else
                {
                    _settings.Fingerprints[m.Folder] = fingerprint;
                }
            }

            if (_settings.Enabled.TryGetValue(Key(m), out var on)) m.IsEnabled = on;
            if (_settings.Notes.TryGetValue(Key(m), out var note)) m.Note = note;
            if (_settings.Choices.TryGetValue(Key(m), out var choice) && choice.Length > 0)
                m.CheckedIncludes = new HashSet<string>(choice, StringComparer.OrdinalIgnoreCase);
            m.IconPath = NexusClient.CachedIconPath(m.NexusId);
            m.PropertyChanged += OnModChanged;
            Mods.Add(m);
        }

        ModsDirText = string.IsNullOrWhiteSpace(_settings.ModsDir) ? "（未设置模组库）" : _settings.ModsDir;
        GameDirText = _settings.GameDir;

        AppState.Save(_settings);   // persist first-seen fingerprints
        UpdateStatus();
    }

    private void OnModChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ModItem.IsEnabled) || sender is not ModItem m) return;

        _settings.Enabled[Key(m)] = m.IsEnabled;
        AppState.Save(_settings);
        UpdateStatus();
    }

    private void Apply()
    {
        try
        {
            var enabled = Mods.Where(m => m.IsEnabled).ToList();
            var r = DeployService.Deploy(_settings.GameDir, enabled);
            var size = LayerIndex.HumanSize(r.Bytes);
            _lastSources = r.Sources;

            StatusLine = $"已应用 {enabled.Count} 个模组 → {r.Layers} 层 · {size} · {r.Message}";

            new DoneDialog(enabled.Count, r.Layers, size)
            {
                Owner = Application.Current.MainWindow,
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusLine = "应用失败：" + ex.Message;
        }
    }

    private void ImportArchives()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "模组压缩包 (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|所有文件 (*.*)|*.*",
            Multiselect = true,
            Title = "选择要导入的模组压缩包",
        };

        if (dlg.ShowDialog() != true) return;
        ImportFiles(dlg.FileNames);
    }

    /// <summary>Used by both the file dialog and the drag-and-drop zone.</summary>
    public void ImportFiles(IEnumerable<string> paths)
    {
        var archives = paths.Where(p =>
            p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
            p.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (archives.Length == 0)
        {
            StatusLine = "拖入的文件里没有支持的压缩包（zip / rar / 7z）";
            return;
        }

        try
        {
            StatusLine = $"正在解包导入 {archives.Length} 个压缩包…";
            var created = ArchiveImporter.Import(archives, _settings.ModsDir);

            foreach (var dir in created)
                _settings.Fingerprints[dir] = Fingerprint(dir);
            AppState.Save(_settings);

            Load();
            StatusLine = $"已导入 {created.Count} 个模组 · 库内共 {Mods.Count} 个";
            _ = FetchNexusAsync();
        }
        catch (Exception ex)
        {
            StatusLine = "导入失败：" + ex.Message;
        }
    }

    private async Task FetchNexusAsync()
    {
        var key = SecretStore.Load();
        if (string.IsNullOrWhiteSpace(key)) return;

        var categories = await NexusClient.GetCategoriesAsync(key);

        foreach (var m in Mods.Where(x => int.TryParse(x.NexusId, out _)).ToList())
        {
            if (!int.TryParse(m.NexusId, out var id)) continue;

            var info = await NexusClient.GetModAsync(id, key);
            if (info is null) continue;

            if (categories.TryGetValue(info.CategoryId, out var category) &&
                !string.IsNullOrWhiteSpace(category))
            {
                m.CategoryName = category;
                _settings.Categories[Key(m)] = category;
            }

            var icon = await NexusClient.DownloadIconAsync(info);
            if (icon is not null) m.IconPath = icon;
            if (string.IsNullOrWhiteSpace(m.Note) && !string.IsNullOrWhiteSpace(info.Summary))
                m.Note = info.Summary;
        }
    }

    /// <summary>Removes a mod folder and its stored state.</summary>
    public void DeleteMod(ModItem? mod)
    {
        if (mod is null) return;

        var ok = ConfirmDialog.Show(Application.Current.MainWindow, "删除模组？",
            $"将删除「{mod.Name}」的整个文件夹：\n{mod.Folder}\n\n此操作不可撤销，但原始压缩包不会被动。",
            "当然", "还是算了");

        if (!ok) return;

        try
        {
            Directory.Delete(mod.Folder, recursive: true);
        }
        catch (Exception ex)
        {
            StatusLine = "删除失败（文件可能被占用）：" + ex.Message;
            return;
        }

        _settings.Enabled.Remove(mod.StateKey);
        _settings.Notes.Remove(mod.StateKey);
        _settings.Choices.Remove(mod.StateKey);
        AppState.Save(_settings);

        Load();
        StatusLine = $"已删除「{mod.Name}」";
    }

    public void EditNote(ModItem? mod)
    {
        if (mod is null) return;

        var text = InputDialog.Show(Application.Current.MainWindow, "编辑备注",
            $"给「{mod.Name}」写点备注（留空则清除）：", mod.Note);
        if (text is null) return;

        mod.Note = text;
        if (string.IsNullOrWhiteSpace(text)) _settings.Notes.Remove(Key(mod));
        else _settings.Notes[Key(mod)] = text;
        AppState.Save(_settings);
    }

    public void EditOptions(ModItem? mod)
    {
        if (mod is null || !mod.HasOptions) return;

        var entries = mod.BuildOptionEntries();
        var defaults = mod.CheckedIncludes.Count > 0
            ? mod.CheckedIncludes
            : mod.Options.SelectMany(o => o.Include).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
            entry.IsChecked = defaults.Contains(entry.Path);

        // occupancy: who else (enabled) touches the same equipment code
        var others = Mods.Where(m => m.IsEnabled && !ReferenceEquals(m, mod)).ToList();
        foreach (var entry in entries)
        {
            var codes = TargetParser.Extract(entry.Label);
            entry.TargetCodes = string.Join(", ", codes);

            var owners = new List<string>();
            foreach (var code in codes)
                foreach (var other in others)
                    if (other.ActiveTargets.Contains(code))
                        owners.Add($"{other.Name}（{code}）");

            entry.OccupiedBy = string.Join("、", owners.Distinct());
        }

        var dlg = new OptionsDialog(mod.Name, entries) { Owner = Application.Current.MainWindow };
        if (dlg.ShowDialog() != true) return;

        mod.CheckedIncludes = entries
            .Where(x => x.IsChecked)
            .Select(x => x.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _settings.Choices[Key(mod)] = mod.CheckedIncludes.ToArray();
        AppState.Save(_settings);
        StatusLine = $"{mod.Name}：已选 {mod.CheckedIncludes.Count} 项，点「应用更改」生效";
    }

    private bool FilterMod(object item)
    {
        if (item is not ModItem mod) return true;

        switch (FilterMode)
        {
            case "Enabled" when !mod.IsEnabled:
            case "Disabled" when mod.IsEnabled:
            case "Noted" when string.IsNullOrWhiteSpace(mod.Note):
                return false;
            case "Recent":
                try
                {
                    if (DateTime.Now - Directory.GetLastWriteTime(mod.Folder) > TimeSpan.FromDays(30)) return false;
                }
                catch
                {
                    return false;
                }
                break;
        }

        if (string.IsNullOrWhiteSpace(Filter)) return true;

        return mod.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase)
               || (mod.NexusId?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false)
               || mod.Note.Contains(Filter, StringComparison.OrdinalIgnoreCase)
               || mod.Description.Contains(Filter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Removes every patch layer, leaving the game completely vanilla.</summary>
    private void RestoreVanilla()
    {
        var data = Path.Combine(_settings.GameDir, "data");
        var (layers, bytes) = LayerIndex.Summary(data);

        if (layers == 0)
        {
            StatusLine = "当前已经是原版（0 层），无事可做。";
            return;
        }

        var ok = ConfirmDialog.Show(Application.Current.MainWindow,
            "确认执行？",
            $"这将移除游戏 data 目录下全部 {layers} 层补丁（{LayerIndex.HumanSize(bytes)}），" +
            "让游戏回到官方原版。\n\n模组文件不会被删除，重新点「应用更改」就能装回去。");

        if (!ok)
        {
            StatusLine = "已取消，什么都没动。";
            return;
        }

        try
        {
            DeployService.Deploy(_settings.GameDir, new List<ModItem>());
            StatusLine = $"已恢复原版：移除 {layers} 层 · 释放 {LayerIndex.HumanSize(bytes)}";
        }
        catch (Exception ex)
        {
            StatusLine = "恢复失败：" + ex.Message;
        }
    }

    public void AutoCheckUpdates() => _ = CheckUpdatesAsync(false);

    /// <summary>Pulls icons, summaries and Nexus categories in the background.</summary>
    public void RefreshNexusInfo() => _ = FetchNexusAsync();

    private async Task CheckUpdatesAsync(bool manual)
    {
        var key = SecretStore.Load();
        if (string.IsNullOrWhiteSpace(key))
        {
            if (manual) StatusLine = "未配置 API Key，无法检查更新";
            return;
        }

        if (manual) StatusLine = "正在检查模组更新…";

        try
        {
            var updates = await UpdateChecker.CheckAsync(Mods, _settings, key);

            if (updates.Count == 0)
            {
                if (manual) StatusLine = "已是最新：没有发现可用更新";
                return;
            }

            StatusLine = $"发现 {updates.Count} 个可用更新";
            var dlg = new UpdatesDialog(updates, OnIgnoreUpdate)
            {
                Owner = Application.Current.MainWindow,
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            StatusLine = "检查更新失败：" + ex.Message;
        }
    }

    private void OnIgnoreUpdate(UpdateInfo info)
    {
        _settings.IgnoredUpdates[info.Mod.StateKey] = info.NewVersion;
        AppState.Save(_settings);
        StatusLine = $"已忽略「{info.Mod.Name}」的 v{info.NewVersion}";
    }

    private void EditApiKey()
    {
        var current = SecretStore.Load();
        var hint = current is null
            ? "粘贴 Nexus Personal API Key（本地 DPAPI 加密保存）："
            : "已保存 key，输入新 key 覆盖，留空则清除：";
        var text = InputDialog.Show(Application.Current.MainWindow, "Nexus API Key", hint);
        if (text is null) return;

        if (string.IsNullOrWhiteSpace(text))
        {
            SecretStore.Clear();
            StatusLine = "API Key 已清除";
            return;
        }

        SecretStore.Save(text);
        StatusLine = "API Key 已加密保存，开始拉取模组信息…";
        _ = FetchNexusAsync();
    }

    private void UpdateStatus()
    {
        var (layers, bytes) = LayerIndex.Summary(Path.Combine(_settings.GameDir, "data"));
        var on = Mods.Count(m => m.IsEnabled);
        var keyState = SecretStore.HasKey ? "API Key 已配置" : "未配置 API Key";
        StatusLine = $"库内 {Mods.Count} 个模组 · 启用 {on} · 当前 {layers} 层 / {LayerIndex.HumanSize(bytes)} · {keyState}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
