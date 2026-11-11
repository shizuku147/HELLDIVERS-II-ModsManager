using System.Windows;

namespace HD2MM.Views;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string initial = "")
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        PromptText.Text = prompt;
        ValueBox.Text = initial;
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    public string Value => ValueBox.Text;

    public static string? Show(Window? owner, string title, string prompt, string initial = "")
    {
        var dlg = new InputDialog(title, prompt, initial);
        if (owner is not null) dlg.Owner = owner;
        return dlg.ShowDialog() == true ? dlg.Value.Trim() : null;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
