using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HD2MM.Models;

namespace HD2MM.Views;

public partial class OccupancyDialog : Window
{
    private readonly Action<WeaponUsage, string> _onTagChanged;
    private readonly Func<WeaponUsage, List<WeaponUsage>> _onDelete;
    private readonly Func<List<WeaponUsage>> _onRescan;

    public OccupancyDialog(
        List<WeaponUsage> items,
        Action<WeaponUsage, string> onTagChanged,
        Func<WeaponUsage, List<WeaponUsage>> onDelete,
        Func<List<WeaponUsage>> onRescan)
    {
        InitializeComponent();
        _onTagChanged = onTagChanged;
        _onDelete = onDelete;
        _onRescan = onRescan;

        Bind(items);
    }

    private void Bind(List<WeaponUsage> items)
    {
        UsageList.ItemsSource = items;

        var free = items.Count(x => !x.IsOccupied);
        SummaryText.Text = items.Count == 0
            ? "没有可显示的装备。给模组写备注（如 SR-24）或点「重新扫描」试试。"
            : $"共 {items.Count} 项 · 空闲 {free} · 已占用 {items.Count - free}";
    }

    private void OnTagClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null) return;

        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.Placement = PlacementMode.Bottom;
        button.ContextMenu.IsOpen = true;
    }

    private void OnTagPick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        if (item.Parent is not ContextMenu menu) return;
        if (menu.PlacementTarget is not Button { Tag: WeaponUsage usage }) return;

        var tag = item.Tag as string ?? "";
        _onTagChanged(usage, tag);
    }

    private void OnDeleteEntry(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: WeaponUsage usage }) return;

        var ok = ConfirmDialog.Show(this, "从列表移除？",
            $"把「{usage.Code}」从占用检查里隐藏。\n\n只是不显示，不会动任何模组文件。",
            "移除", "算了");

        if (!ok) return;

        Bind(_onDelete(usage));
    }

    private void OnRescan(object sender, RoutedEventArgs e) => Bind(_onRescan());

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
