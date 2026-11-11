using System.Windows;

namespace HD2MM.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string body, string yesText, string noText)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        YesButton.Content = yesText;
        NoButton.Content = noText;
    }

    public static bool Show(Window? owner, string title, string body,
        string yesText = "当然", string noText = "还是算了")
    {
        var dlg = new ConfirmDialog(title, body, yesText, noText);
        if (owner is not null) dlg.Owner = owner;
        return dlg.ShowDialog() == true;
    }

    private void OnYes(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnNo(object sender, RoutedEventArgs e) => DialogResult = false;
}
