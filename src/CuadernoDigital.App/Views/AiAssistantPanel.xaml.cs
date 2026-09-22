using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
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
        var shouldAttachCurrentPage = shouldEditPage
            ? NeedsCurrentPageForPageEdit(prompt, requestAttachments)
            : ShouldIncludeCurrentPage(prompt);
        var pagePng = shouldAttachCurrentPage ? captureCurrentPage?.Invoke() : null;
        var shouldInsertIntoNotebook = !shouldEditPage && ShouldInsertAnswerIntoNotebook(prompt);

        AddMessage("user", BuildUserMessage(prompt, requestAttachments, pagePng is not null));
        PromptBox.Clear();
        attachments.Clear();

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
            await SendPageEditAsync(prompt, () => client.AskForPageEditAsync(apiKey, prompt, history.TakeLast(4).ToList(), requestAttachments, pagePng));
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
        var normalized = NormalizePrompt(prompt);
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

        return ContainsAny(normalized, keywords);
    }

    private static bool ShouldInsertAnswerIntoNotebook(string prompt)
    {
        var normalized = NormalizePrompt(prompt);
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

        return ContainsAny(normalized, keywords);
    }

    private static bool ShouldApplyPageEdit(string prompt)
    {
        var normalized = NormalizePrompt(prompt);
        string[] keywords =
        [
            "dibuja",
            "dibuje",
            "dibujame",
            "dibujar",
            "dibuj",
            "haz un dibujo",
            "hazme un dibujo",
            "draw",
            "traza",
            "plasm",
            "pinta",
            "escribe ",
            "escriba ",
            "escribeme",
            "escribe en la pagina",
            "escribe en mi cuaderno",
            "pon en la pagina",
            "pon ",
            "ponlo",
            "agrega a la pagina",
            "agrega ",
            "agregalo",
            "anade a la pagina",
            "anade ",
            "coloca ",
            "inserta ",
            "insert",
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

        return ContainsAny(normalized, keywords) || LooksLikeDrawableRequest(normalized);
    }

    private static bool NeedsCurrentPageForPageEdit(string prompt, IReadOnlyList<AiRequestAttachment> requestAttachments)
    {
        if (requestAttachments.Count > 0)
        {
            return true;
        }

        var normalized = NormalizePrompt(prompt);
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

        return ContainsAny(normalized, keywords);
    }

    private static AiPageEditPlan CreateLocalPageEditPlan(string prompt, string summary)
    {
        var normalized = NormalizePrompt(prompt);
        var title = BuildLocalTitle(prompt);
        if (LooksLikeDrawingRequest(normalized))
        {
            return CreateLocalDrawingPlan(normalized, title, summary);
        }

        if (LooksLikeDiagramRequest(normalized))
        {
            return CreateLocalDiagramPlan(title, summary);
        }

        var text = BuildLocalWrittenText(prompt);

        return new AiPageEditPlan
        {
            Summary = summary,
            TextBlocks =
            [
                new()
                {
                    Text = LooksLikeWritingRequest(normalized) ? "Apunte" : title,
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
                    Text = text,
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

    private static AiPageEditPlan CreateLocalDrawingPlan(string normalized, string title, string summary)
    {
        if (ContainsAny(normalized, "cara feliz") || ContainsAnyWord(normalized, "carita", "smiley", "sonrisa"))
        {
            return CreateLocalFacePlan(summary, isHappy: !ContainsAny(normalized, "triste", "sad"));
        }

        if (ContainsAnyWord(normalized, "corazon", "heart"))
        {
            return CreateLocalHeartPlan(summary);
        }

        if (ContainsAnyWord(normalized, "sol", "sun"))
        {
            return CreateLocalSunPlan(summary);
        }

        if (ContainsAnyWord(normalized, "casa", "house"))
        {
            return CreateLocalHousePlan(summary);
        }

        if (ContainsAnyWord(normalized, "arbol", "tree"))
        {
            return CreateLocalTreePlan(summary);
        }

        if (ContainsAnyWord(normalized, "estrella", "star"))
        {
            return CreateLocalStarPlan(summary);
        }

        if (ContainsAnyWord(normalized, "nube", "cloud"))
        {
            return CreateLocalCloudPlan(summary);
        }

        if (ContainsAnyWord(normalized, "flor", "flower"))
        {
            return CreateLocalFlowerPlan(summary);
        }

        return CreateLocalSketchPlan(title, summary);
    }

    private static AiPageEditPlan CreateLocalFacePlan(string summary, bool isHappy)
    {
        var mouthPoints = isHappy
            ? Points((350, 365), (385, 405), (450, 420), (515, 405), (550, 365))
            : Points((350, 410), (385, 370), (450, 355), (515, 370), (550, 410));

        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "ellipse", X = 300, Y = 170, Width = 300, Height = 300, Stroke = "#F59E0B", StrokeWidth = 8 },
                new() { Type = "ellipse", X = 370, Y = 275, Width = 34, Height = 44, Stroke = "#111827", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 496, Y = 275, Width = 34, Height = 44, Stroke = "#111827", StrokeWidth = 7 },
                new() { Type = "path", Stroke = "#111827", StrokeWidth = 7, Points = mouthPoints },
                new() { Type = "ellipse", X = 330, Y = 335, Width = 42, Height = 26, Stroke = "#F97316", StrokeWidth = 3 },
                new() { Type = "ellipse", X = 528, Y = 335, Width = 42, Height = 26, Stroke = "#F97316", StrokeWidth = 3 }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalHeartPlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new()
                {
                    Type = "path",
                    Stroke = "#E11D48",
                    StrokeWidth = 8,
                    Points = Points(
                        (450, 470), (330, 360), (285, 285), (310, 225), (370, 210), (425, 255),
                        (450, 305), (475, 255), (530, 210), (590, 225), (615, 285), (570, 360),
                        (450, 470))
                }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalSunPlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "ellipse", X = 355, Y = 250, Width = 190, Height = 190, Stroke = "#F59E0B", StrokeWidth = 8 },
                new() { Type = "line", X = 450, Y = 170, X2 = 450, Y2 = 220, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 450, Y = 470, X2 = 450, Y2 = 525, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 275, Y = 345, X2 = 330, Y2 = 345, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 570, Y = 345, X2 = 625, Y2 = 345, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 320, Y = 215, X2 = 355, Y2 = 250, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 580, Y = 215, X2 = 545, Y2 = 250, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 320, Y = 475, X2 = 355, Y2 = 440, Stroke = "#F59E0B", StrokeWidth = 6 },
                new() { Type = "line", X = 580, Y = 475, X2 = 545, Y2 = 440, Stroke = "#F59E0B", StrokeWidth = 6 }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalHousePlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "rectangle", X = 320, Y = 330, Width = 260, Height = 190, Stroke = "#2563EB", StrokeWidth = 6 },
                new() { Type = "path", Stroke = "#DC2626", StrokeWidth = 7, Points = Points((290, 330), (450, 205), (610, 330), (290, 330)) },
                new() { Type = "rectangle", X = 430, Y = 420, Width = 55, Height = 100, Stroke = "#92400E", StrokeWidth = 5 },
                new() { Type = "rectangle", X = 350, Y = 370, Width = 55, Height = 50, Stroke = "#0EA5E9", StrokeWidth = 4 },
                new() { Type = "rectangle", X = 505, Y = 370, Width = 55, Height = 50, Stroke = "#0EA5E9", StrokeWidth = 4 }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalTreePlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "rectangle", X = 425, Y = 380, Width = 55, Height = 145, Stroke = "#92400E", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 335, Y = 210, Width = 230, Height = 210, Stroke = "#16A34A", StrokeWidth = 8 },
                new() { Type = "ellipse", X = 270, Y = 285, Width = 190, Height = 175, Stroke = "#22C55E", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 445, Y = 285, Width = 190, Height = 175, Stroke = "#22C55E", StrokeWidth = 7 },
                new() { Type = "line", X = 285, Y = 525, X2 = 615, Y2 = 525, Stroke = "#16A34A", StrokeWidth = 5 }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalStarPlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new()
                {
                    Type = "path",
                    Stroke = "#F59E0B",
                    StrokeWidth = 8,
                    Points = Points(
                        (450, 185), (482, 295), (598, 295), (504, 362), (540, 475),
                        (450, 405), (360, 475), (396, 362), (302, 295), (418, 295), (450, 185))
                }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalCloudPlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "ellipse", X = 280, Y = 320, Width = 180, Height = 120, Stroke = "#0EA5E9", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 390, Y = 260, Width = 180, Height = 165, Stroke = "#0EA5E9", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 510, Y = 320, Width = 140, Height = 110, Stroke = "#0EA5E9", StrokeWidth = 7 },
                new() { Type = "path", Stroke = "#0EA5E9", StrokeWidth = 7, Points = Points((325, 430), (430, 455), (560, 445), (625, 410)) }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalFlowerPlan(string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            InkShapes =
            [
                new() { Type = "line", X = 450, Y = 390, X2 = 450, Y2 = 560, Stroke = "#16A34A", StrokeWidth = 7 },
                new() { Type = "ellipse", X = 410, Y = 230, Width = 80, Height = 115, Stroke = "#EC4899", StrokeWidth = 6 },
                new() { Type = "ellipse", X = 410, Y = 340, Width = 80, Height = 115, Stroke = "#EC4899", StrokeWidth = 6 },
                new() { Type = "ellipse", X = 350, Y = 295, Width = 115, Height = 80, Stroke = "#EC4899", StrokeWidth = 6 },
                new() { Type = "ellipse", X = 435, Y = 295, Width = 115, Height = 80, Stroke = "#EC4899", StrokeWidth = 6 },
                new() { Type = "ellipse", X = 420, Y = 315, Width = 60, Height = 60, Stroke = "#F59E0B", StrokeWidth = 7 },
                new() { Type = "path", Stroke = "#16A34A", StrokeWidth = 5, Points = Points((450, 470), (395, 430), (360, 455), (420, 495)) }
            ]
        };
    }

    private static AiPageEditPlan CreateLocalSketchPlan(string title, string summary)
    {
        return new AiPageEditPlan
        {
            Summary = summary,
            TextBlocks =
            [
                new() { Text = title, X = 250, Y = 145, Width = 400, Height = 55, FontSize = 26, Foreground = "#1D4ED8", IsBold = true, TextAlignment = "Center" }
            ],
            InkShapes =
            [
                new() { Type = "ellipse", X = 310, Y = 230, Width = 280, Height = 200, Stroke = "#2563EB", StrokeWidth = 6 },
                new() { Type = "path", Stroke = "#16A34A", StrokeWidth = 6, Points = Points((330, 455), (390, 405), (455, 455), (520, 405), (585, 455)) },
                new() { Type = "line", X = 315, Y = 505, X2 = 585, Y2 = 505, Stroke = "#64748B", StrokeWidth = 4 }
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
            .Replace("dibuje", "", StringComparison.OrdinalIgnoreCase)
            .Replace("hazme", "", StringComparison.OrdinalIgnoreCase)
            .Replace("haz", "", StringComparison.OrdinalIgnoreCase)
            .Replace("has", "", StringComparison.OrdinalIgnoreCase)
            .Replace("escribe en la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Replace("pon en la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Replace("agrega a la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Replace("anade a la pagina", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ':', ';', ',');

        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "Apunte visual";
        }

        return char.ToUpper(cleaned[0]) + cleaned[1..];
    }

    private static string BuildLocalWrittenText(string prompt)
    {
        var cleaned = prompt.Trim();
        string[] removable =
        [
            "escribe en la pagina",
            "escribe en mi cuaderno",
            "pon en la pagina",
            "agrega a la pagina",
            "anade a la pagina",
            "coloca en la pagina",
            "transcribe",
            "pasalo a la pagina",
            "pasa esto a la pagina",
            "escribe",
            "pon",
            "agrega",
            "anade",
            "coloca",
            "inserta"
        ];

        foreach (var fragment in removable)
        {
            cleaned = cleaned.Replace(fragment, "", StringComparison.OrdinalIgnoreCase);
        }

        cleaned = cleaned.Trim(' ', '.', ':', ';', ',');
        return string.IsNullOrWhiteSpace(cleaned)
            ? prompt.Trim()
            : cleaned;
    }

    private static bool LooksLikeDrawingRequest(string normalized)
    {
        return ContainsAny(normalized, "dibu", "draw", "traza", "pinta", "plasm") || LooksLikeDrawableRequest(normalized);
    }

    private static bool LooksLikeDrawableRequest(string normalized)
    {
        return ContainsAny(normalized, "cara feliz")
            || ContainsAnyWord(
            normalized,
            "carita",
            "cara",
            "sonrisa",
            "smiley",
            "corazon",
            "heart",
            "sol",
            "sun",
            "casa",
            "house",
            "arbol",
            "tree",
            "estrella",
            "star",
            "nube",
            "cloud",
            "flor",
            "flower");
    }

    private static bool LooksLikeDiagramRequest(string normalized)
    {
        return ContainsAny(normalized, "diagrama", "mapa", "esquema", "linea de tiempo", "organiza");
    }

    private static bool LooksLikeWritingRequest(string normalized)
    {
        return ContainsAny(normalized, "escribe", "pon ", "agrega", "anade", "coloca", "inserta", "transcribe");
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        return keywords.Any(text.Contains);
    }

    private static bool ContainsAnyWord(string text, params string[] words)
    {
        return words.Any(word => ContainsWord(text, word));
    }

    private static bool ContainsWord(string text, string word)
    {
        var index = text.IndexOf(word, StringComparison.Ordinal);
        while (index >= 0)
        {
            var before = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            var afterIndex = index + word.Length;
            var after = afterIndex >= text.Length || !char.IsLetterOrDigit(text[afterIndex]);
            if (before && after)
            {
                return true;
            }

            index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal);
        }

        return false;
    }

    private static string NormalizePrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return string.Empty;
        }

        var decomposed = prompt.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static List<AiPagePoint> Points(params (double X, double Y)[] points)
    {
        return points
            .Select(point => new AiPagePoint { X = point.X, Y = point.Y })
            .ToList();
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
