using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
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
    private Action<AiPageEditPlan>? applyPageEditToPage;
    private bool isBusy;

    public event EventHandler? ChatChanged;

    public AiAssistantPanel()
    {
        InitializeComponent();
        MessagesList.ItemsSource = messages;
        AttachmentsList.ItemsSource = attachments;
        UpdateStatus();
    }

    public void Configure(Notebook activeNotebook, Func<byte[]?> pageCapture, Action<string> insertText, Action<AiPageEditPlan> applyPageEdit)
    {
        notebook = activeNotebook;
        captureCurrentPage = pageCapture;
        insertTextIntoPage = insertText;
        applyPageEditToPage = applyPageEdit;
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

        var history = messages.ToList();
        var requestAttachments = attachments.ToList();
        var shouldEditPage = ShouldApplyPageEdit(prompt);
        var needsGeminiForPageEdit = shouldEditPage && NeedsGeminiForPageEdit(prompt, requestAttachments);
        var pagePng = (needsGeminiForPageEdit || ShouldIncludeCurrentPage(prompt)) ? captureCurrentPage?.Invoke() : null;
        var shouldInsertIntoNotebook = ShouldInsertAnswerIntoNotebook(prompt);

        AddMessage("user", BuildUserMessage(prompt, requestAttachments, pagePng is not null));
        PromptBox.Clear();
        attachments.Clear();

        if (shouldEditPage && !needsGeminiForPageEdit)
        {
            ApplyLocalPageEdit(prompt, "Listo. Hice un borrador visual en la pagina.");
            return;
        }

        if (!TryLoadApiKey(out var apiKey))
        {
            if (shouldEditPage)
            {
                ApplyLocalPageEdit(prompt, "No hay clave de Gemini; hice un borrador local en la pagina.");
            }

            return;
        }

        if (shouldEditPage)
        {
            await SendPageEditAsync(prompt, () => client.AskForPageEditAsync(apiKey, prompt, history, requestAttachments, pagePng));
            return;
        }

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

    private async Task SendPageEditAsync(string prompt, Func<Task<string>> operation)
    {
        if (isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            var answer = await operation().WaitAsync(TimeSpan.FromSeconds(35));
            var plan = ParsePageEditPlan(answer);
            if (plan is null || (plan.TextBlocks.Count == 0 && plan.InkShapes.Count == 0))
            {
                ApplyLocalPageEdit(prompt, "Gemini no devolvio un plan visual aplicable; hice un borrador local.");
                return;
            }

            applyPageEditToPage?.Invoke(plan);
            AddMessage("assistant", string.IsNullOrWhiteSpace(plan.Summary) ? "Listo. Agregue el contenido a la pagina." : plan.Summary.Trim());
            StatusText.Text = "Gemini edito la pagina actual.";
        }
        catch (Exception ex)
        {
            ApplyLocalPageEdit(prompt, $"Gemini tardo o fallo ({ex.Message}). Hice un borrador local en la pagina.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ApplyLocalPageEdit(string prompt, string summary)
    {
        var plan = CreateLocalPageEditPlan(prompt, summary);
        applyPageEditToPage?.Invoke(plan);
        AddMessage("assistant", summary);
        StatusText.Text = summary;
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
        var bytes = ImageClipboardService.TryGetPngBytesFromClipboard();
        if (bytes is null)
        {
            return;
        }

        attachments.Add(new AiRequestAttachment
        {
            Name = $"pegado_{DateTimeOffset.Now:HHmmss}.png",
            MimeType = "image/png",
            Data = bytes
        });
        StatusText.Text = "Imagen pegada como adjunto.";
    }

    private static AiPageEditPlan? ParsePageEditPlan(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AiPageEditPlan>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start
            ? raw[start..(end + 1)]
            : null;
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

    private static bool ShouldApplyPageEdit(string prompt)
    {
        var normalized = prompt.ToLowerInvariant();
        string[] keywords =
        [
            "dibuja",
            "dibuje",
            "dibujame",
            "haz un dibujo",
            "hazme un dibujo",
            "draw",
            "escribe en la pagina",
            "escribe en mi cuaderno",
            "pon en la pagina",
            "agrega a la pagina",
            "anade a la pagina",
            "transcribe",
            "transcribir",
            "pasalo a la pagina",
            "pasa esto a la pagina",
            "haz un diagrama",
            "hazme un diagrama",
            "mapa conceptual",
            "esquema visual",
            "linea de tiempo",
            "organiza en la pagina"
        ];

        return keywords.Any(normalized.Contains);
    }

    private static bool NeedsGeminiForPageEdit(string prompt, IReadOnlyList<AiRequestAttachment> requestAttachments)
    {
        if (requestAttachments.Count > 0)
        {
            return true;
        }

        var normalized = prompt.ToLowerInvariant();
        string[] keywords =
        [
            "transcribe",
            "transcribir",
            "lee",
            "revisa",
            "corrige",
            "esto",
            "lo que aparece",
            "la imagen",
            "adjunto",
            "archivo"
        ];

        return keywords.Any(normalized.Contains);
    }

    private static AiPageEditPlan CreateLocalPageEditPlan(string prompt, string summary)
    {
        var normalized = prompt.ToLowerInvariant();
        var title = BuildLocalTitle(prompt);
        if (normalized.Contains("diagrama") || normalized.Contains("mapa") || normalized.Contains("esquema") || normalized.Contains("dibuja") || normalized.Contains("dibuj"))
        {
            return CreateLocalDiagramPlan(title, summary);
        }

        return new AiPageEditPlan
        {
            Summary = summary,
            TextBlocks =
            [
                new()
                {
                    Text = title,
                    X = 110,
                    Y = 120,
                    Width = 620,
                    Height = 70,
                    FontSize = 28,
                    Foreground = "#1D4ED8",
                    IsBold = true
                },
                new()
                {
                    Text = $"Apunte: {prompt.Trim()}",
                    X = 110,
                    Y = 205,
                    Width = 650,
                    Height = 190,
                    FontSize = 21,
                    Foreground = "#111827",
                    HighlightColor = "#FEF3C7"
                }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalDiagramPlan(string title, string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            TextBlocks =
            [
                new() { Text = title, X = 110, Y = 95, Width = 650, Height = 60, FontSize = 28, Foreground = "#1D4ED8", IsBold = true, TextAlignment = "Center" },
                new() { Text = "Idea principal", X = 335, Y = 215, Width = 230, Height = 58, FontSize = 20, Foreground = "#111827", HighlightColor = "#DBEAFE", IsBold = true, TextAlignment = "Center" },
                new() { Text = "Concepto 1", X = 105, Y = 390, Width = 190, Height = 55, FontSize = 18, Foreground = "#111827", HighlightColor = "#DCFCE7", TextAlignment = "Center" },
                new() { Text = "Concepto 2", X = 355, Y = 390, Width = 190, Height = 55, FontSize = 18, Foreground = "#111827", HighlightColor = "#FEF3C7", TextAlignment = "Center" },
                new() { Text = "Concepto 3", X = 605, Y = 390, Width = 190, Height = 55, FontSize = 18, Foreground = "#111827", HighlightColor = "#FCE7F3", TextAlignment = "Center" }
            ],
            InkShapes =
            [
                new() { Type = "rectangle", X = 320, Y = 200, Width = 260, Height = 90, Stroke = "#2563EB", StrokeWidth = 4 },
                new() { Type = "rectangle", X = 95, Y = 375, Width = 210, Height = 85, Stroke = "#16A34A", StrokeWidth = 4 },
                new() { Type = "rectangle", X = 345, Y = 375, Width = 210, Height = 85, Stroke = "#D97706", StrokeWidth = 4 },
                new() { Type = "rectangle", X = 595, Y = 375, Width = 210, Height = 85, Stroke = "#DB2777", StrokeWidth = 4 },
                new() { Type = "arrow", X = 390, Y = 290, X2 = 200, Y2 = 375, Stroke = "#64748B", StrokeWidth = 3 },
                new() { Type = "arrow", X = 450, Y = 290, X2 = 450, Y2 = 375, Stroke = "#64748B", StrokeWidth = 3 },
                new() { Type = "arrow", X = 510, Y = 290, X2 = 700, Y2 = 375, Stroke = "#64748B", StrokeWidth = 3 }
            ]
        };
    }

    private static string BuildLocalTitle(string prompt)
    {
        var cleaned = prompt
            .Replace("dibujame", "", StringComparison.OrdinalIgnoreCase)
            .Replace("dibuja", "", StringComparison.OrdinalIgnoreCase)
            .Replace("hazme", "", StringComparison.OrdinalIgnoreCase)
            .Replace("haz", "", StringComparison.OrdinalIgnoreCase)
            .Replace("escribe en la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Replace("pon en la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ':', ';', ',');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Apunte visual";
        }

        return char.ToUpper(cleaned[0]) + cleaned[1..];
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
