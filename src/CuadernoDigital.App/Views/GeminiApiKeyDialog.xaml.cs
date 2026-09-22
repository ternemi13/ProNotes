using System.Windows;

namespace CuadernoDigital.App.Views;

public partial class GeminiApiKeyDialog : Window
{
    public GeminiApiKeyDialog(bool hasExistingKey)
    {
        InitializeComponent();
        HelpText.Text = hasExistingKey
            ? "Ya hay una clave guardada. Escribe una nueva para reemplazarla o usa Quitar. La clave se guarda cifrada localmente con Windows."
            : "Pega tu API key de Gemini. Se guarda cifrada localmente con Windows y nunca se escribe en el repositorio.";
        ClearButton.IsEnabled = hasExistingKey;
        Loaded += (_, _) => ApiKeyBox.Focus();
    }

    public string ApiKey => ApiKeyBox.Password.Trim();

    public bool ShouldClear { get; private set; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            MessageBox.Show(this, "Pega una API key valida o cancela.", "Clave requerida", MessageBoxButton.OK, MessageBoxImage.Information);
            ApiKeyBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ShouldClear = true;
        DialogResult = true;
    }
}
