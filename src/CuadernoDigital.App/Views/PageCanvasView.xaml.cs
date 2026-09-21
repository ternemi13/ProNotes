using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;

namespace CuadernoDigital.App.Views;

public enum InkToolMode
{
    Pen,
    Highlighter,
    Eraser,
    Select
}

public partial class PageCanvasView : UserControl
{
    public static readonly DependencyProperty CurrentPageProperty =
        DependencyProperty.Register(
            nameof(CurrentPage),
            typeof(NotebookPage),
            typeof(PageCanvasView),
            new PropertyMetadata(null, OnCurrentPageChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(PageCanvasView),
            new PropertyMetadata(1d));

    private bool isLoading;
    private bool isChangingHistory;
    private readonly Stack<string> undoStack = new();
    private readonly Stack<string> redoStack = new();

    public PageCanvasView()
    {
        InitializeComponent();
        ConfigureTool(InkToolMode.Pen, Colors.Black, 3);
        InkSurface.Strokes.StrokesChanged += InkStrokes_StrokesChanged;
    }

    public event EventHandler? PageChanged;

    public NotebookPage? CurrentPage
    {
        get => (NotebookPage?)GetValue(CurrentPageProperty);
        set => SetValue(CurrentPageProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, Math.Clamp(value, 0.35, 3));
    }

    public void ConfigureTool(InkToolMode mode, Color color, double width)
    {
        switch (mode)
        {
            case InkToolMode.Highlighter:
                InkSurface.EditingMode = InkCanvasEditingMode.Ink;
                InkSurface.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = Color.FromArgb(110, color.R, color.G, color.B),
                    Width = Math.Max(width, 14),
                    Height = Math.Max(width, 14),
                    IsHighlighter = true,
                    FitToCurve = true,
                    StylusTip = StylusTip.Rectangle
                };
                break;
            case InkToolMode.Eraser:
                InkSurface.EditingMode = InkCanvasEditingMode.EraseByStroke;
                break;
            case InkToolMode.Select:
                InkSurface.EditingMode = InkCanvasEditingMode.Select;
                break;
            case InkToolMode.Pen:
            default:
                InkSurface.EditingMode = InkCanvasEditingMode.Ink;
                InkSurface.DefaultDrawingAttributes = new DrawingAttributes
                {
                    Color = color,
                    Width = width,
                    Height = width,
                    FitToCurve = true,
                    IgnorePressure = false,
                    StylusTip = StylusTip.Ellipse
                };
                break;
        }
    }

    public void Undo()
    {
        if (undoStack.Count == 0)
        {
            return;
        }

        redoStack.Push(PageSerializer.SerializeStrokes(InkSurface.Strokes));
        ApplyInkSnapshot(undoStack.Pop());
    }

    public void Redo()
    {
        if (redoStack.Count == 0)
        {
            return;
        }

        undoStack.Push(PageSerializer.SerializeStrokes(InkSurface.Strokes));
        ApplyInkSnapshot(redoStack.Pop());
    }

    public void CaptureHistory()
    {
        if (isLoading || isChangingHistory)
        {
            return;
        }

        undoStack.Push(CurrentPage?.InkBase64 ?? string.Empty);
        redoStack.Clear();
    }

    private static void OnCurrentPageChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((PageCanvasView)dependencyObject).LoadPage((NotebookPage?)args.NewValue);
    }

    private void LoadPage(NotebookPage? page)
    {
        isLoading = true;
        undoStack.Clear();
        redoStack.Clear();
        InkSurface.Strokes.StrokesChanged -= InkStrokes_StrokesChanged;
        InkSurface.Strokes = PageSerializer.DeserializeStrokes(page?.InkBase64);
        InkSurface.Strokes.StrokesChanged += InkStrokes_StrokesChanged;
        isLoading = false;
    }

    private void PersistCurrentInk()
    {
        if (isLoading || isChangingHistory || CurrentPage is null)
        {
            return;
        }

        CurrentPage.InkBase64 = PageSerializer.SerializeStrokes(InkSurface.Strokes);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyInkSnapshot(string inkBase64)
    {
        isChangingHistory = true;
        InkSurface.Strokes.StrokesChanged -= InkStrokes_StrokesChanged;
        InkSurface.Strokes = PageSerializer.DeserializeStrokes(inkBase64);
        InkSurface.Strokes.StrokesChanged += InkStrokes_StrokesChanged;
        isChangingHistory = false;
        PersistCurrentInk();
    }

    private void InkStrokes_StrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        PersistCurrentInk();
    }

    private void InkSurface_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
    {
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void InkSurface_PreviewInputStart(object sender, InputEventArgs e)
    {
        CaptureHistory();
    }

    private void InkSurface_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            return;
        }

        Zoom += e.Delta > 0 ? 0.1 : -0.1;
        e.Handled = true;
    }
}
