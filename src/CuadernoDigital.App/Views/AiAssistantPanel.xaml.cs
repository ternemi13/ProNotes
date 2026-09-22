using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;

namespace CuadernoDigital.App.Views;

public partial class AiAssistantPanel : UserControl
{
    private const int MaxHistoryMessages = 80;
    private readonly ObservableCollection<AiChatMessage> messages = [];
    private readonly GeminiClient client = new();
    private readonly GeminiSettingsService settings = new();
    private Notebook? notebook;
    private Func<byte[]?>? captureCurrentPage;
    private bool isBusy;

    public event EventHandler? ChatChanged;

    public AiAssistantPanel()
    {
        InitializeComponent();
        MessagesList.ItemsSource = messages;
        UpdateStatus();
    }

    public void Configure(Notebook activeNotebook, Func<byte[]?> pageCapture)
    {
        notebook = activeNotebook;
        captureCurrentPage = pageCapture;
        messages.Clear();

        foreach (var message in activeNotebook.AiChatHistory)
        {
            messages.Add(message);
        }

        if (messages.Count == 0)
        {
            StatusText.Text = settings.HasApiKey
                ? "Listo. Puedes preguntar o pedir revision de la pagina actual."
                : "Configura la clave de Gemini para activar el asistente.";
        }
        else
        {
            ScrollToLastMessage();
            UpdateStatus();
        }
    }

    private async void Ask_Click(object sender, RoutedEventArgs e)
    {
        var prompt = PromptBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            StatusText.Text = "Escribe una pregunta primero.";
            PromptBox.Focus();
            return;
        }

        if (!TryLoadApiKey(out var apiKey))
        {
            return;
        }

        var history = messages.ToList();
        AddMessage("user", prompt);
        PromptBox.Clear();
        await SendAsync(() => client.AskAsync(apiKey, prompt, history));
    }

    private async void ReviewPage_Click(object sender, RoutedEventArgs e)
    {
        if (!TryLoadApiKey(out var apiKey))
        {
            return;
        }

        if (captureCurrentPage is null)
        {
            StatusText.Text = "Abre una pagina para revisarla.";
            return;
        }

        var pagePng = captureCurrentPage();
        if (pagePng is null || pagePng.Length == 0)
        {
            StatusText.Text = "No se pudo capturar la pagina actual.";
            return;
        }

        var history = messages.ToList();
        AddMessage("user", "Revisa la pagina actual y dime como mejorarla.");
        await SendAsync(() => client.ReviewPageAsync(apiKey, pagePng, history));
    }

    private void ConfigureKey_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dialog = new GeminiApiKeyDialog(settings.HasApiKey)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.ShouldClear)
        {
            settings.ClearApiKey();
            StatusText.Text = "Clave quitada. El asistente queda pausado hasta configurar una nueva.";
            return;
        }

        settings.SaveApiKey(dialog.ApiKey);
        StatusText.Text = "Clave guardada localmente y cifrada para este usuario de Windows.";
    }

    private async Task SendAsync(Func<Task<string>> operation)
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var answer = await operation();
            AddMessage("assistant", string.IsNullOrWhiteSpace(answer) ? "Gemini no devolvio texto." : answer.Trim());
            UpdateStatus();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private bool TryLoadApiKey(out string apiKey)
    {
        apiKey = settings.LoadApiKey() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            return true;
        }

        StatusText.Text = "Configura primero tu API key de Gemini.";
        return false;
    }

    private void AddMessage(string role, string text)
    {
        if (notebook is null)
        {
            return;
        }

        var message = new AiChatMessage
        {
            Role = role,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };

        messages.Add(message);
        notebook.AiChatHistory.Add(message);

        while (notebook.AiChatHistory.Count > MaxHistoryMessages)
        {
            notebook.AiChatHistory.RemoveAt(0);
        }

        while (messages.Count > MaxHistoryMessages)
        {
            messages.RemoveAt(0);
        }

        ChatChanged?.Invoke(this, EventArgs.Empty);
        ScrollToLastMessage();
    }

    private void SetBusy(bool busy)
    {
        isBusy = busy;
        AskButton.IsEnabled = !busy;
        ReviewButton.IsEnabled = !busy;
        ConfigureButton.IsEnabled = !busy;
        PromptBox.IsEnabled = !busy;
        if (busy)
        {
            StatusText.Text = "Consultando Gemini...";
        }
    }

    private void ScrollToLastMessage()
    {
        if (messages.Count > 0)
        {
            MessagesList.ScrollIntoView(messages[^1]);
        }
    }

    private void UpdateStatus()
    {
        StatusText.Text = settings.HasApiKey
            ? "Listo. Pregunta algo o revisa la pagina actual."
            : "Configura la clave de Gemini para activar el asistente.";
    }
}
