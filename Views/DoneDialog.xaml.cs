using System.Windows;

namespace HD2MM.Views;

public partial class DoneDialog : Window
{
    public DoneDialog(int mods, int layers, string size)
    {
        InitializeComponent();
        SummaryText.Text = $"已应用 {mods} 个模组 · {layers} 层 · {size}";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
