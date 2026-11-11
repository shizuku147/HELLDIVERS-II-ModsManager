using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using HD2MM.Models;

namespace HD2MM.Views;

public partial class UpdatesDialog : Window
{
    private readonly ObservableCollection<UpdateInfo> _items;
    private readonly Action<UpdateInfo> _onIgnore;

    public UpdatesDialog(IEnumerable<UpdateInfo> items, Action<UpdateInfo> onIgnore)
    {
        InitializeComponent();
        _items = new ObservableCollection<UpdateInfo>(items);
        _onIgnore = onIgnore;
        UpdateList.ItemsSource = _items;
    }

    private void OnOpenPage(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string url } && !string.IsNullOrWhiteSpace(url))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void OnIgnore(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: UpdateInfo info }) return;

        _onIgnore(info);
        _items.Remove(info);
        if (_items.Count == 0) Close();
    }

    private void OnIgnoreAll(object sender, RoutedEventArgs e)
    {
        foreach (var info in _items.ToList()) _onIgnore(info);
        _items.Clear();
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
