using System.Windows;
using HD2MM.Models;

namespace HD2MM.Views;

public partial class LayersDialog : Window
{
    public LayersDialog(List<LayerGroupView> groups)
    {
        InitializeComponent();
        GroupList.ItemsSource = groups;

        var layers = groups.Sum(g => g.Layers);
        var bytes = groups.Sum(g => g.Bytes);
        SummaryText.Text = $"共 {groups.Count} 个 bundle · {layers} 层 · {Services.LayerIndex.HumanSize(bytes)}";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
