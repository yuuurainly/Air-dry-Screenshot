using System.Windows;

namespace LiangTu.App.Windows;

public partial class TextInputWindow : Window
{
    public TextInputWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InputBox.Focus();
    }

    public string Text => InputBox.Text.Trim();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            return;
        }

        DialogResult = true;
    }
}
