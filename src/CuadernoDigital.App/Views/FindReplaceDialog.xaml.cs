using System.Windows;

namespace CuadernoDigital.App.Views;

public partial class FindReplaceDialog : Window
{
    public FindReplaceDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => FindBox.Focus();
    }

    public string FindText => FindBox.Text.Trim();

    public string ReplacementText => ReplaceBox.Text;

    public bool MatchCase => MatchCaseBox.IsChecked == true;

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FindText))
        {
            MessageBox.Show(this, "Escribe el texto que quieres buscar.", "Buscar y reemplazar", MessageBoxButton.OK, MessageBoxImage.Information);
            FindBox.Focus();
            return;
        }

        DialogResult = true;
    }
}
