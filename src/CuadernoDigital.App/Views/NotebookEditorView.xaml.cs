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

    private void Select_Click(object sender, RoutedEventArgs e)
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

    private void PasteImage_Click(object sender, RoutedEventArgs e)
    {
        CanvasView.InsertImageFromClipboard();
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

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            PasteImage_Click(sender, e);
            e.Handled = true;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        autoSaveTimer.Stop();
        SaveIfNeeded();
    }
}
