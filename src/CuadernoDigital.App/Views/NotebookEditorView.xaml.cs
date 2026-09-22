using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using CuadernoDigital.App.ViewModels;
using Microsoft.Win32;

namespace CuadernoDigital.App.Views;

public partial class NotebookEditorView : Window
{
    private readonly DispatcherTimer autoSaveTimer;
    private InkToolMode currentTool = InkToolMode.Pen;
    private string currentInkColor = "#111827";
    private string currentTextColor = "#111827";
    private string currentHighlightColor = "#FEF08A";

    public NotebookEditorView(NotebookRepository repository, Notebook notebook, string filePath)
    {
        InitializeComponent();
        DataContext = new NotebookEditorViewModel(repository, notebook, filePath);

        autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        autoSaveTimer.Tick += (_, _) => SaveIfNeeded();
        autoSaveTimer.Start();

        AiPanel.Configure(ViewModel.Notebook, CaptureCurrentPageForAi, InsertAiTextIntoPage);
        AiPanel.ChatChanged += (_, _) => ViewModel.MarkDirty();
        ApplyInkSettings();
    }

    private NotebookEditorViewModel ViewModel => (NotebookEditorViewModel)DataContext;

    private void SaveIfNeeded()
    {
        if (ViewModel.HasUnsavedChanges)
        {
            ViewModel.Save();
        }
    }

    private void CanvasView_PageChanged(object sender, EventArgs e)
    {
        ViewModel.MarkDirty();
    }

    private byte[]? CaptureCurrentPageForAi()
    {
        return ViewModel.SelectedPage is null
            ? null
            : CanvasView.RenderPageToPng(ViewModel.SelectedPage, 1.5);
    }

    private void InsertAiTextIntoPage(string text)
    {
        CanvasView.InsertTextBox(text);
        currentTool = InkToolMode.Select;
        ApplyInkSettings();
    }

    private void Background_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.MarkDirty();
    }

    private void Pen_Click(object sender, RoutedEventArgs e)
    {
        currentTool = InkToolMode.Pen;
        ApplyInkSettings();
    }

    private void Highlighter_Click(object sender, RoutedEventArgs e)
    {
        currentTool = InkToolMode.Highlighter;
        ApplyInkSettings();
    }

    private void Eraser_Click(object sender, RoutedEventArgs e)
    {
        currentTool = InkToolMode.Eraser;
        ApplyInkSettings();
    }

    private void Cursor_Click(object sender, RoutedEventArgs e)
    {
        currentTool = InkToolMode.Select;
        ApplyInkSettings();
    }

    private void InsertText_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.InsertTextBox();
        currentTool = InkToolMode.Select;
        ApplyInkSettings();
    }

    private void InsertTable_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TableSizeDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            CanvasView.InsertTable(dialog.Rows, dialog.Columns);
            currentTool = InkToolMode.Select;
            ApplyInkSettings();
        }
    }

    private void InsertImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Insertar imagen",
            Filter = "Imagenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            CanvasView.InsertImageFromFile(dialog.FileName);
            currentTool = InkToolMode.Select;
            ApplyInkSettings();
        }
    }

    private void InsertChart_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ChartDataDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            CanvasView.InsertChart(dialog.Chart);
            currentTool = InkToolMode.Select;
            ApplyInkSettings();
        }
    }

    private void PasteObjectOrImage()
    {
        CanvasView.PasteObjectOrImage();
        currentTool = InkToolMode.Select;
        ApplyInkSettings();
    }

    private void InkSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (CanvasView is not null)
        {
            ApplyInkSettings();
        }
    }

    private void Bold_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.ToggleSelectedTextBold();
    }

    private void Italic_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.ToggleSelectedTextItalic();
    }

    private void Underline_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.ToggleSelectedTextUnderline();
    }

    private void TextSize_Changed(object sender, RoutedEventArgs e)
    {
        if (CanvasView is null || TextSizeBox?.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (double.TryParse(item.Content?.ToString(), out var fontSize))
        {
            CanvasView.SetSelectedTextFontSize(fontSize);
        }
    }

    private void FontFamily_Changed(object sender, RoutedEventArgs e)
    {
        if (CanvasView is null || FontFamilyBox?.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        CanvasView.SetSelectedTextFontFamily(item.Content?.ToString() ?? "Segoe UI");
    }

    private void LineSpacing_Changed(object sender, RoutedEventArgs e)
    {
        if (CanvasView is null || LineSpacingBox?.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        if (double.TryParse(item.Content?.ToString(), out var lineSpacing))
        {
            CanvasView.SetSelectedTextLineSpacing(lineSpacing);
        }
    }

    private void AlignLeft_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.SetSelectedTextAlignment("Left");
    }

    private void AlignCenter_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.SetSelectedTextAlignment("Center");
    }

    private void AlignRight_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.SetSelectedTextAlignment("Right");
    }

    private void Bullets_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.ApplyBulletsToSelectedText();
    }

    private void Numbering_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.ApplyNumberingToSelectedText();
    }

    private void IncreaseIndent_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.IncreaseSelectedTextIndent();
    }

    private void DecreaseIndent_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.DecreaseSelectedTextIndent();
    }

    private void FindReplace_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FindReplaceDialog
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var count = CanvasView.ReplaceTextOnPage(dialog.FindText, dialog.ReplacementText, dialog.MatchCase);
        MessageBox.Show(this, $"Reemplazos realizados: {count}", "Buscar y reemplazar", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void AddTableRow_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.AddRowToSelectedTable();
    }

    private void RemoveTableRow_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.RemoveRowFromSelectedTable();
    }

    private void AddTableColumn_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.AddColumnToSelectedTable();
    }

    private void RemoveTableColumn_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.RemoveColumnFromSelectedTable();
    }

    private void ApplyInkSettings()
    {
        var color = (Color)ColorConverter.ConvertFromString(currentInkColor);
        CanvasView.ConfigureTool(currentTool, color, InkWidthSlider?.Value ?? 3);
    }

    private void InkColorButton_Click(object sender, RoutedEventArgs e)
    {
        InkColorPopup.IsOpen = true;
    }

    private void TextColorButton_Click(object sender, RoutedEventArgs e)
    {
        TextColorPopup.IsOpen = true;
    }

    private void HighlightColorButton_Click(object sender, RoutedEventArgs e)
    {
        HighlightColorPopup.IsOpen = true;
    }

    private void InkColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string color)
        {
            return;
        }

        currentInkColor = color;
        SetSwatch(InkColorPreview, color);
        InkColorPopup.IsOpen = false;
        ApplyInkSettings();
    }

    private void TextColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string color)
        {
            return;
        }

        currentTextColor = color;
        SetSwatch(TextColorPreview, color);
        TextColorPopup.IsOpen = false;
        CanvasView.SetSelectedTextColor(currentTextColor);
    }

    private void HighlightColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string color)
        {
            return;
        }

        currentHighlightColor = color;
        SetSwatch(HighlightColorPreview, color);
        HighlightColorPopup.IsOpen = false;
        CanvasView.SetSelectedTextHighlightColor(currentHighlightColor);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.Redo();
    }

    private async void ExportPage_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPage is null)
        {
            return;
        }

        SaveIfNeeded();
        var dialog = new SaveFileDialog
        {
            Title = "Exportar pagina a PDF",
            Filter = "PDF|*.pdf",
            FileName = $"{MakeSafeFileName(ViewModel.Notebook.Title)}-{MakeSafeFileName(ViewModel.SelectedPage.Title)}.pdf"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await ExportAsync(() => new PdfExportService().ExportPageAsync(ViewModel.SelectedPage, dialog.FileName), dialog.FileName);
    }

    private async void ExportNotebook_Click(object sender, RoutedEventArgs e)
    {
        SaveIfNeeded();
        var dialog = new SaveFileDialog
        {
            Title = "Exportar cuaderno a PDF",
            Filter = "PDF|*.pdf",
            FileName = $"{MakeSafeFileName(ViewModel.Notebook.Title)}.pdf"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        await ExportAsync(() => new PdfExportService().ExportNotebookAsync(ViewModel.Notebook, dialog.FileName), dialog.FileName);
    }

    private async Task ExportAsync(Func<Task> export, string filePath)
    {
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await export();
            MessageBox.Show(this, $"PDF exportado:\n{filePath}", "Exportacion completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"No se pudo exportar el PDF.\n\n{ex.Message}", "Error al exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.B)
        {
            CanvasView.ToggleSelectedTextBold();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.I)
        {
            CanvasView.ToggleSelectedTextItalic();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.U)
        {
            CanvasView.ToggleSelectedTextUnderline();
            e.Handled = true;
            return;
        }

        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
        {
            CanvasView.CopySelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.X)
        {
            CanvasView.CutSelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.D)
        {
            CanvasView.DuplicateSelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            PasteObjectOrImage();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            CanvasView.DeleteSelectedObject();
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        autoSaveTimer.Stop();
        SaveIfNeeded();
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "ProNotes" : safe;
    }

    private static void SetSwatch(System.Windows.Shapes.Rectangle swatch, string color)
    {
        swatch.Fill = string.Equals(color, "Transparent", StringComparison.OrdinalIgnoreCase)
            ? Brushes.Transparent
            : (Brush)new BrushConverter().ConvertFromString(color)!;
    }
}
