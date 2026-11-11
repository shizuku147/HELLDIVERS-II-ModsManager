using System.Windows;
using HD2MM.Models;

namespace HD2MM.Views;

public partial class OptionsDialog : Window
{
    private readonly List<OptionEntry> _entries;

    public OptionsDialog(string modName, List<OptionEntry> entries)
    {
        InitializeComponent();
        _entries = entries;
        ModNameText.Text = modName;
        EntryList.ItemsSource = entries;
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries) entry.IsChecked = true;
    }

    private void OnSelectNone(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _entries) entry.IsChecked = false;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
