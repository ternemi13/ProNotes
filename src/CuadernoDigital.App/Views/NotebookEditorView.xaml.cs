using System.ComponentModel;
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

    private void CoverDesigner_CoverChanged(object sender, EventArgs e)
    {
        ViewModel.MarkCoverDirty();
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

    private void TextColor_Changed(object sender, RoutedEventArgs e)
    {
        if (CanvasView is null || TextColorBox?.SelectedItem is not ComboBoxItem item || item.Tag is not string color)
        {
            return;
        }

        CanvasView.SetSelectedTextColor(color);
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
        var color = Colors.Black;
        if (InkColorBox?.SelectedItem is ComboBoxItem item && item.Tag is string hex)
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
        }

        CanvasView.ConfigureTool(currentTool, color, InkWidthSlider?.Value ?? 3);
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.Redo();
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
}
