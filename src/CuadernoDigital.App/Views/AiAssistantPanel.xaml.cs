using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using Microsoft.Win32;

namespace CuadernoDigital.App.Views;

public partial class AiAssistantPanel : UserControl
{
    private const int MaxHistoryMessages = 80;
    private const long MaxAttachmentBytes = 20 * 1024 * 1024;
    private readonly ObservableCollection<AiChatMessage> messages = [];
    private readonly ObservableCollection<AiRequestAttachment> attachments = [];
    private readonly GeminiClient client = new();
    private readonly GeminiSettingsService settings = new();
    private Notebook? notebook;
    private Func<byte[]?>? captureCurrentPage;
    private Action<string>? insertTextIntoPage;
    private bool isBusy;

    public event EventHandler? ChatChanged;

    public AiAssistantPanel()
    {
        InitializeComponent();
        MessagesList.ItemsSource = messages;
        AttachmentsList.ItemsSource = attachments;
        UpdateStatus();
    }

    public void Configure(Notebook activeNotebook, Func<byte[]?> pageCapture, Action<string> insertText)
    {
        notebook = activeNotebook;
        captureCurrentPage = pageCapture;
        insertTextIntoPage = insertText;
        messages.Clear();

        foreach (var message in activeNotebook.AiChatHistory)
        {
            messages.Add(message);
        }

        if (messages.Count == 0)
        {
            StatusText.Text = settings.HasApiKey
                ? "Listo. Escribe tu pedido y envia."
                : "Configura la clave de Gemini para activar el asistente.";
        }
        else
        {
            ScrollToLastMessage();
            UpdateStatus();
        }
    }

    public void FocusPrompt()
    {
        PromptBox.Focus();
    }

    public void ConfigureApiKey()
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

    private async void Send_Click(object sender, RoutedEventArgs e)
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
        var requestAttachments = attachments.ToList();
        var pagePng = ShouldIncludeCurrentPage(prompt) ? captureCurrentPage?.Invoke() : null;
        var shouldInsertIntoNotebook = ShouldInsertAnswerIntoNotebook(prompt);

        AddMessage("user", BuildUserMessage(prompt, requestAttachments, pagePng is not null));
        PromptBox.Clear();
        attachments.Clear();
        await SendAsync(() => client.AskAsync(apiKey, prompt, history, requestAttachments, pagePng), shouldInsertIntoNotebook);
    }

    private void AttachFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Adjuntar a la IA",
            Multiselect = true,
            Filter = "Archivos compatibles|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.pdf;*.txt;*.md;*.csv|Imagenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|PDF|*.pdf|Texto|*.txt;*.md;*.csv|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        foreach (var fileName in dialog.FileNames)
        {
            AddAttachmentFromFile(fileName);
        }
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: AiRequestAttachment attachment })
        {
            attachments.Remove(attachment);
        }
    }

    private void PromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            Send_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.V && Clipboard.ContainsImage())
        {
            AddImageFromClipboard();
            e.Handled = true;
        }
    }

    private async Task SendAsync(Func<Task<string>> operation, bool insertAnswerIntoNotebook)
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var answer = await operation();
            var text = string.IsNullOrWhiteSpace(answer) ? "Gemini no devolvio texto." : answer.Trim();
            AddMessage("assistant", text);
            if (insertAnswerIntoNotebook && insertTextIntoPage is not null)
            {
                insertTextIntoPage(text);
                StatusText.Text = "Respuesta agregada a la pagina actual.";
            }
            else
            {
                UpdateStatus();
            }
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
        SendButton.IsEnabled = !busy;
        AttachButton.IsEnabled = !busy;
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
            ? "Listo. Escribe tu pedido y envia."
            : "Configura la clave de Gemini para activar el asistente.";
    }

    private void AddAttachmentFromFile(string fileName)
    {
        try
        {
            var fileInfo = new FileInfo(fileName);
            if (!fileInfo.Exists)
            {
                return;
            }

            if (fileInfo.Length > MaxAttachmentBytes)
            {
                StatusText.Text = $"El archivo {fileInfo.Name} es demasiado grande.";
                return;
            }

            var mimeType = GetSupportedMimeType(fileInfo.Extension);
            if (mimeType is null)
            {
                StatusText.Text = $"Tipo de archivo no compatible: {fileInfo.Extension}";
                return;
            }

            attachments.Add(new AiRequestAttachment
            {
                Name = fileInfo.Name,
                MimeType = mimeType,
                Data = File.ReadAllBytes(fileInfo.FullName)
            });

            StatusText.Text = "Archivo adjunto listo para enviar.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"No se pudo adjuntar el archivo. {ex.Message}";
        }
    }

    private void AddImageFromClipboard()
    {
        var bitmap = Clipboard.GetImage();
        if (bitmap is null)
        {
            return;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        attachments.Add(new AiRequestAttachment
        {
            Name = $"pegado_{DateTimeOffset.Now:HHmmss}.png",
            MimeType = "image/png",
            Data = stream.ToArray()
        });
        StatusText.Text = "Imagen pegada como adjunto.";
    }

    private static string BuildUserMessage(string prompt, IReadOnlyList<AiRequestAttachment> requestAttachments, bool includesPage)
    {
        var extras = new List<string>();
        if (includesPage)
        {
            extras.Add("pagina actual");
        }

        extras.AddRange(requestAttachments.Select(attachment => attachment.Name));
        return extras.Count == 0
            ? prompt
            : $"{prompt}\n\nAdjuntos: {string.Join(", ", extras)}";
    }

    private static bool ShouldIncludeCurrentPage(string prompt)
    {
        var normalized = prompt.ToLowerInvariant();
        string[] keywords =
        [
            "pagina",
            "apunte",
            "ejercicio",
            "escribi",
            "escrito",
            "ortografia",
            "tabla",
            "grafica",
            "dibujo",
            "esto",
            "corrige",
            "revisa",
            "lee"
        ];

        return keywords.Any(normalized.Contains);
    }

    private static bool ShouldInsertAnswerIntoNotebook(string prompt)
    {
        var normalized = prompt.ToLowerInvariant();
        string[] keywords =
        [
            "ponlo",
            "pon ",
            "agrega",
            "agregalo",
            "insert",
            "anade",
            "escribe en",
            "en los apuntes",
            "en mi cuaderno",
            "edita lo que escribi",
            "corrige la ortografia"
        ];

        return keywords.Any(normalized.Contains);
    }

    private static string? GetSupportedMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".txt" or ".md" => "text/plain",
            ".csv" => "text/csv",
            _ => null
        };
    }
}
