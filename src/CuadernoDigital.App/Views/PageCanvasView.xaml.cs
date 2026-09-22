using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
    private bool isDraggingSticker;
    private Point stickerDragStart;
    private StickerImage? selectedSticker;
    private Border? selectedStickerControl;
    private PageTextBox? selectedTextBox;
    private Border? selectedTextBoxControl;
    private readonly Stack<string> undoStack = new();
    private readonly Stack<string> redoStack = new();

    public PageCanvasView()
    {
        InitializeComponent();
        Focusable = true;
        ConfigureTool(InkToolMode.Pen, Colors.Black, 3);
        InkSurface.Strokes.StrokesChanged += InkStrokes_StrokesChanged;
        KeyDown += PageCanvasView_KeyDown;
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
        ObjectLayer.IsHitTestVisible = mode == InkToolMode.Select;
        InkSurface.IsHitTestVisible = mode != InkToolMode.Select;

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

    public void InsertImageFromFile(string filePath)
    {
        if (CurrentPage is null)
        {
            return;
        }

        var bytes = File.ReadAllBytes(filePath);
        var sticker = new StickerImage
        {
            FileName = Path.GetFileName(filePath),
            MimeType = GetMimeType(filePath),
            ImageBase64 = Convert.ToBase64String(bytes),
            X = 110,
            Y = 130
        };

        SetInitialImageSize(sticker, bytes);
        CurrentPage.Images.Add(sticker);
        RenderObjects();
        SelectSticker(sticker);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InsertImageFromClipboard()
    {
        if (CurrentPage is null || !Clipboard.ContainsImage())
        {
            return;
        }

        var bitmap = Clipboard.GetImage();
        if (bitmap is null)
        {
            return;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);

        var sticker = new StickerImage
        {
            FileName = $"clipboard_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png",
            MimeType = "image/png",
            ImageBase64 = Convert.ToBase64String(stream.ToArray()),
            X = 110,
            Y = 130,
            Width = Math.Clamp(bitmap.Width, 120, 520),
            Height = Math.Clamp(bitmap.Height, 90, 420)
        };

        CurrentPage.Images.Add(sticker);
        RenderObjects();
        SelectSticker(sticker);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InsertTextBox()
    {
        if (CurrentPage is null)
        {
            return;
        }

        var textBox = new PageTextBox();
        CurrentPage.TextBoxes.Add(textBox);
        RenderObjects();
        SelectTextBox(textBox);
        PageChanged?.Invoke(this, EventArgs.Empty);
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
        RenderObjects();
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

    private void RenderObjects()
    {
        ObjectLayer.Children.Clear();
        selectedSticker = null;
        selectedStickerControl = null;
        selectedTextBox = null;
        selectedTextBoxControl = null;

        if (CurrentPage is null)
        {
            return;
        }

        foreach (var sticker in CurrentPage.Images)
        {
            var control = CreateStickerControl(sticker);
            Canvas.SetLeft(control, sticker.X);
            Canvas.SetTop(control, sticker.Y);
            ObjectLayer.Children.Add(control);
        }

        foreach (var textBox in CurrentPage.TextBoxes)
        {
            var control = CreateTextBoxControl(textBox);
            Canvas.SetLeft(control, textBox.X);
            Canvas.SetTop(control, textBox.Y);
            ObjectLayer.Children.Add(control);
        }
    }

    private Border CreateStickerControl(StickerImage sticker)
    {
        var image = new Image
        {
            Source = CreateBitmap(sticker.ImageBase64),
            Stretch = Stretch.Uniform,
            IsHitTestVisible = false
        };

        var resizeThumb = new Thumb
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE,
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Opacity = 0.9,
            Visibility = Visibility.Collapsed
        };
        resizeThumb.DragDelta += (_, e) =>
        {
            sticker.Width = Math.Max(48, sticker.Width + e.HorizontalChange);
            sticker.Height = Math.Max(48, sticker.Height + e.VerticalChange);
            if (selectedStickerControl is not null)
            {
                selectedStickerControl.Width = sticker.Width;
                selectedStickerControl.Height = sticker.Height;
            }

            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        var grid = new Grid();
        grid.Children.Add(image);
        grid.Children.Add(resizeThumb);

        var border = new Border
        {
            Width = sticker.Width,
            Height = sticker.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Child = grid,
            Tag = sticker,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(sticker.Rotation)
        };

        border.MouseLeftButtonDown += Sticker_MouseLeftButtonDown;
        border.MouseRightButtonDown += Sticker_MouseRightButtonDown;
        border.MouseMove += Sticker_MouseMove;
        border.MouseLeftButtonUp += Sticker_MouseLeftButtonUp;
        return border;
    }

    private void Sticker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not StickerImage sticker)
        {
            return;
        }

        Focus();
        SelectSticker(sticker, border);
        isDraggingSticker = true;
        stickerDragStart = e.GetPosition(ObjectLayer);
        border.CaptureMouse();
        e.Handled = true;
    }

    private void Sticker_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDraggingSticker || selectedSticker is null || selectedStickerControl is null)
        {
            return;
        }

        var position = e.GetPosition(ObjectLayer);
        var delta = position - stickerDragStart;
        selectedSticker.X += delta.X;
        selectedSticker.Y += delta.Y;
        Canvas.SetLeft(selectedStickerControl, selectedSticker.X);
        Canvas.SetTop(selectedStickerControl, selectedSticker.Y);
        stickerDragStart = position;
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Sticker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border)
        {
            border.ReleaseMouseCapture();
        }

        isDraggingSticker = false;
        e.Handled = true;
    }

    private void Sticker_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is StickerImage sticker)
        {
            Focus();
            SelectSticker(sticker, border);
            e.Handled = false;
        }
    }

    private void SelectSticker(StickerImage sticker, Border? control = null)
    {
        ClearSelectionChrome();
        selectedTextBox = null;
        selectedTextBoxControl = null;
        selectedSticker = sticker;
        selectedStickerControl = control ?? ObjectLayer.Children
            .OfType<Border>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, sticker));

        if (selectedStickerControl is not null)
        {
            selectedStickerControl.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SetThumbsVisibility(selectedStickerControl, Visibility.Visible);
        }
    }

    private Border CreateTextBoxControl(PageTextBox pageTextBox)
    {
        var editor = new TextBox
        {
            Text = pageTextBox.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = (Brush)new BrushConverter().ConvertFromString(pageTextBox.Foreground)!,
            FontSize = pageTextBox.FontSize,
            Padding = new Thickness(4),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        editor.TextChanged += (_, _) =>
        {
            pageTextBox.Text = editor.Text;
            PageChanged?.Invoke(this, EventArgs.Empty);
        };
        editor.GotKeyboardFocus += (_, _) => SelectTextBox(pageTextBox);
        editor.PreviewMouseRightButtonDown += (_, e) =>
        {
            SelectTextBox(pageTextBox);
            e.Handled = false;
        };

        var moveThumb = new Thumb
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Cursor = Cursors.SizeAll,
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Opacity = 0.9,
            Visibility = Visibility.Collapsed
        };
        moveThumb.DragStarted += (_, _) =>
        {
            Focus();
            SelectTextBox(pageTextBox);
        };
        moveThumb.DragDelta += (_, e) =>
        {
            pageTextBox.X += e.HorizontalChange;
            pageTextBox.Y += e.VerticalChange;
            if (selectedTextBoxControl is not null)
            {
                Canvas.SetLeft(selectedTextBoxControl, pageTextBox.X);
                Canvas.SetTop(selectedTextBoxControl, pageTextBox.Y);
            }

            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        var resizeThumb = new Thumb
        {
            Width = 16,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Cursor = Cursors.SizeNWSE,
            Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
            Opacity = 0.9,
            Visibility = Visibility.Collapsed
        };
        resizeThumb.DragDelta += (_, e) =>
        {
            pageTextBox.Width = Math.Max(120, pageTextBox.Width + e.HorizontalChange);
            pageTextBox.Height = Math.Max(70, pageTextBox.Height + e.VerticalChange);
            if (selectedTextBoxControl is not null)
            {
                selectedTextBoxControl.Width = pageTextBox.Width;
                selectedTextBoxControl.Height = pageTextBox.Height;
            }

            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        var contentGrid = new Grid();
        contentGrid.Children.Add(editor);
        contentGrid.Children.Add(moveThumb);
        contentGrid.Children.Add(resizeThumb);

        var border = new Border
        {
            Width = pageTextBox.Width,
            Height = pageTextBox.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Child = contentGrid,
            Tag = pageTextBox
        };

        border.MouseLeftButtonDown += (_, _) => SelectTextBox(pageTextBox, border);
        border.MouseRightButtonDown += (_, e) =>
        {
            SelectTextBox(pageTextBox, border);
            e.Handled = false;
        };
        return border;
    }

    private void SelectTextBox(PageTextBox textBox, Border? control = null)
    {
        ClearSelectionChrome();
        selectedSticker = null;
        selectedStickerControl = null;
        selectedTextBox = textBox;
        selectedTextBoxControl = control ?? ObjectLayer.Children
            .OfType<Border>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, textBox));

        if (selectedTextBoxControl is not null)
        {
            selectedTextBoxControl.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SetThumbsVisibility(selectedTextBoxControl, Visibility.Visible);
        }
    }

    private void ClearSelectionChrome()
    {
        if (selectedStickerControl is not null)
        {
            selectedStickerControl.BorderBrush = Brushes.Transparent;
            SetThumbsVisibility(selectedStickerControl, Visibility.Collapsed);
        }

        if (selectedTextBoxControl is not null)
        {
            selectedTextBoxControl.BorderBrush = Brushes.Transparent;
            SetThumbsVisibility(selectedTextBoxControl, Visibility.Collapsed);
        }
    }

    private static void SetThumbsVisibility(DependencyObject root, Visibility visibility)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Thumb thumb)
            {
                thumb.Visibility = visibility;
            }

            SetThumbsVisibility(child, visibility);
        }
    }

    private static BitmapImage CreateBitmap(string imageBase64)
    {
        var bytes = Convert.FromBase64String(imageBase64);
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static void SetInitialImageSize(StickerImage sticker, byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var maxWidth = 520d;
        var maxHeight = 420d;
        var scale = Math.Min(maxWidth / frame.Width, maxHeight / frame.Height);
        scale = Math.Min(scale, 1d);
        sticker.Width = Math.Max(80, frame.Width * scale);
        sticker.Height = Math.Max(60, frame.Height * scale);
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            _ => "image/png"
        };
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

    private void PageCanvasView_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            InsertImageFromClipboard();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Delete)
        {
            return;
        }

        DeleteSelectedObject();
        e.Handled = true;
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        InsertImageFromClipboard();
    }

    private void DeleteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedObject();
    }

    private void DeleteSelectedObject()
    {
        if (CurrentPage is null || (selectedSticker is null && selectedTextBox is null))
        {
            return;
        }

        if (selectedSticker is not null)
        {
            CurrentPage.Images.Remove(selectedSticker);
        }
        else if (selectedTextBox is not null)
        {
            CurrentPage.TextBoxes.Remove(selectedTextBox);
        }

        RenderObjects();
        PageChanged?.Invoke(this, EventArgs.Empty);
    }
}
