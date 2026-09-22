using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuadernoDigital.App.Controls;
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
    private PageTable? selectedTable;
    private Border? selectedTableControl;
    private PageChart? selectedChart;
    private Border? selectedChartControl;
    private object? objectClipboard;
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

    public byte[] RenderPageToPng(NotebookPage page, double scale = 2)
    {
        var originalPage = CurrentPage;
        var originalZoom = Zoom;
        var changedPageForRender = !ReferenceEquals(originalPage, page);

        try
        {
            if (changedPageForRender)
            {
                SetCurrentValue(CurrentPageProperty, page);
            }

            Zoom = 1;
            ClearSelection();

            PageHost.Measure(new Size(PageHost.Width, PageHost.Height));
            PageHost.Arrange(new Rect(0, 0, PageHost.Width, PageHost.Height));
            PageHost.UpdateLayout();

            return RenderElementToPng(PageHost, PageHost.Width, PageHost.Height, scale);
        }
        finally
        {
            if (changedPageForRender)
            {
                SetCurrentValue(CurrentPageProperty, originalPage);
            }

            Zoom = originalZoom;
        }
    }

    public void InsertImageFromFile(string filePath)
    {
        if (CurrentPage is null)
        {
            return;
        }

        var bytes = File.ReadAllBytes(filePath);
        var croppedBytes = CropImageBeforeInsert(bytes);
        if (croppedBytes is null)
        {
            return;
        }

        InsertImageBytes(croppedBytes, MakeCroppedFileName(Path.GetFileNameWithoutExtension(filePath)), "image/png");
    }

    public void InsertTable(int rows = 3, int columns = 3)
    {
        if (CurrentPage is null)
        {
            return;
        }

        var table = new PageTable
        {
            Rows = rows,
            Columns = columns,
            Width = Math.Max(220, columns * 120),
            Height = Math.Max(90, rows * 44),
            ZIndex = GetNextZIndex()
        };
        table.EnsureCellCount();
        CurrentPage.Tables.Add(table);
        RenderObjects();
        SelectTable(table);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void InsertChart(PageChart chart)
    {
        if (CurrentPage is null)
        {
            return;
        }

        chart.NormalizeData();
        chart.ZIndex = GetNextZIndex();
        CurrentPage.Charts.Add(chart);
        RenderObjects();
        SelectChart(chart);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void EditSelectedChart()
    {
        if (selectedChart is null)
        {
            return;
        }

        var chart = selectedChart;
        var dialog = new ChartDataDialog(chart)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        chart.Title = dialog.Chart.Title;
        chart.Type = dialog.Chart.Type;
        chart.DataPoints = dialog.Chart.DataPoints;
        chart.NormalizeData();
        RenderObjects();
        SelectChart(chart);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddRowToSelectedTable()
    {
        ModifySelectedTable(table => table.AddRow());
    }

    public void RemoveRowFromSelectedTable()
    {
        ModifySelectedTable(table => table.RemoveRow());
    }

    public void AddColumnToSelectedTable()
    {
        ModifySelectedTable(table => table.AddColumn());
    }

    public void RemoveColumnFromSelectedTable()
    {
        ModifySelectedTable(table => table.RemoveColumn());
    }

    public void ResizeSelectedTable(int rows, int columns)
    {
        ModifySelectedTable(table => table.Resize(rows, columns));
    }

    public void SetSelectedTextFontSize(double fontSize)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.FontSize = Math.Clamp(fontSize, 8, 72));
    }

    public void SetSelectedTextColor(string color)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.Foreground = color);
    }

    public void SetSelectedTextFontFamily(string fontFamily)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily);
    }

    public void SetSelectedTextHighlightColor(string color)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.HighlightColor = color);
    }

    public void SetSelectedTextLineSpacing(double lineSpacing)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.LineSpacing = Math.Clamp(lineSpacing, 1, 2.5));
    }

    public void ToggleSelectedTextBold()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.IsBold = !textBox.IsBold);
    }

    public void ToggleSelectedTextItalic()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.IsItalic = !textBox.IsItalic);
    }

    public void ToggleSelectedTextUnderline()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.IsUnderline = !textBox.IsUnderline);
    }

    public void SetSelectedTextAlignment(string alignment)
    {
        ApplySelectedTextBoxStyle(textBox => textBox.TextAlignment = alignment);
    }

    public void ApplyBulletsToSelectedText()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.Text = PrefixTextLines(textBox.Text, index => "• "));
    }

    public void ApplyNumberingToSelectedText()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.Text = PrefixTextLines(textBox.Text, index => $"{index + 1}. "));
    }

    public void IncreaseSelectedTextIndent()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.IndentLevel = Math.Min(8, textBox.IndentLevel + 1));
    }

    public void DecreaseSelectedTextIndent()
    {
        ApplySelectedTextBoxStyle(textBox => textBox.IndentLevel = Math.Max(0, textBox.IndentLevel - 1));
    }

    public int ReplaceTextOnPage(string searchText, string replacement, bool matchCase)
    {
        if (CurrentPage is null || string.IsNullOrEmpty(searchText))
        {
            return 0;
        }

        var comparison = matchCase ? StringComparison.CurrentCulture : StringComparison.CurrentCultureIgnoreCase;
        var replacements = 0;
        foreach (var textBox in CurrentPage.TextBoxes)
        {
            var text = textBox.Text;
            replacements += ReplaceAll(ref text, searchText, replacement, comparison);
            textBox.Text = text;
        }

        foreach (var table in CurrentPage.Tables)
        {
            for (var index = 0; index < table.Cells.Count; index++)
            {
                var text = table.Cells[index];
                replacements += ReplaceAll(ref text, searchText, replacement, comparison);
                table.Cells[index] = text;
            }
        }

        if (replacements > 0)
        {
            RenderObjects();
            PageChanged?.Invoke(this, EventArgs.Empty);
        }

        return replacements;
    }

    private static int ReplaceAll(ref string text, string searchText, string replacement, StringComparison comparison)
    {
        var replacements = 0;
        var index = text.IndexOf(searchText, comparison);
        while (index >= 0)
        {
            text = string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + searchText.Length));
            replacements++;
            index = text.IndexOf(searchText, index + replacement.Length, comparison);
        }

        return replacements;
    }

    public void InsertImageFromClipboard()
    {
        if (CurrentPage is null)
        {
            return;
        }

        var imageBytes = ImageClipboardService.TryGetPngBytesFromClipboard();
        if (imageBytes is null)
        {
            MessageBox.Show(Window.GetWindow(this), "No se encontro una imagen compatible en el portapapeles.", "Pegar imagen", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var croppedBytes = CropImageBeforeInsert(imageBytes);
        if (croppedBytes is null)
        {
            return;
        }

        InsertImageBytes(croppedBytes, $"clipboard_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.png", "image/png");
    }

    private void InsertImageBytes(byte[] bytes, string fileName, string mimeType)
    {
        if (CurrentPage is null)
        {
            return;
        }

        var sticker = new StickerImage
        {
            FileName = fileName,
            MimeType = mimeType,
            ImageBase64 = Convert.ToBase64String(bytes),
            X = 110,
            Y = 130,
            ZIndex = GetNextZIndex()
        };

        SetInitialImageSize(sticker, bytes);
        CurrentPage.Images.Add(sticker);
        RenderObjects();
        SelectSticker(sticker);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private byte[]? CropImageBeforeInsert(byte[] bytes)
    {
        try
        {
            var bitmap = ImageCropDialog.DecodeImage(bytes);
            var dialog = new ImageCropDialog(bitmap)
            {
                Owner = Window.GetWindow(this)
            };

            return dialog.ShowDialog() == true ? dialog.CroppedPngBytes : null;
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), $"No se pudo leer la imagen seleccionada.\n\nDetalle: {ex.Message}", "Imagen no valida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    public void InsertTextBox()
    {
        InsertTextBox("Escribe aqui...");
    }

    public void InsertTextBox(string text)
    {
        if (CurrentPage is null)
        {
            return;
        }

        var textBox = new PageTextBox
        {
            Text = string.IsNullOrWhiteSpace(text) ? "Escribe aqui..." : text.Trim(),
            X = 140,
            Y = 140,
            Width = 420,
            Height = 180,
            ZIndex = GetNextZIndex()
        };
        CurrentPage.TextBoxes.Add(textBox);
        RenderObjects();
        SelectTextBox(textBox);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CopySelectedObject()
    {
        objectClipboard = CloneSelectedObject();
    }

    public void CutSelectedObject()
    {
        CopySelectedObject();
        DeleteSelectedObject();
    }

    public void PasteObjectOrImage()
    {
        if (objectClipboard is not null)
        {
            PasteInternalObject();
            return;
        }

        InsertImageFromClipboard();
    }

    public void DuplicateSelectedObject()
    {
        var clone = CloneSelectedObject();
        if (clone is null)
        {
            return;
        }

        objectClipboard = clone;
        PasteInternalObject();
    }

    public void BringSelectedObjectToFront()
    {
        ApplyToSelectedObject(
            sticker => sticker.ZIndex = GetNextZIndex(),
            textBox => textBox.ZIndex = GetNextZIndex(),
            table => table.ZIndex = GetNextZIndex(),
            chart => chart.ZIndex = GetNextZIndex());
    }

    public void SendSelectedObjectToBack()
    {
        var zIndex = GetMinZIndex() - 1;
        ApplyToSelectedObject(
            sticker => sticker.ZIndex = zIndex,
            textBox => textBox.ZIndex = zIndex,
            table => table.ZIndex = zIndex,
            chart => chart.ZIndex = zIndex);
    }

    public void RotateSelectedObject(double degrees)
    {
        ApplyToSelectedObject(
            sticker => sticker.Rotation = NormalizeRotation(sticker.Rotation + degrees),
            textBox => textBox.Rotation = NormalizeRotation(textBox.Rotation + degrees),
            table => table.Rotation = NormalizeRotation(table.Rotation + degrees),
            chart => chart.Rotation = NormalizeRotation(chart.Rotation + degrees));
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
        ClearSelection();

        if (CurrentPage is null)
        {
            return;
        }

        var objects = new List<(int ZIndex, int Sequence, object Item)>();
        var sequence = 0;
        objects.AddRange(CurrentPage.Images.Select(image => (image.ZIndex, sequence++, (object)image)));
        objects.AddRange(CurrentPage.TextBoxes.Select(textBox => (textBox.ZIndex, sequence++, (object)textBox)));
        objects.AddRange(CurrentPage.Tables.Select(table => (table.ZIndex, sequence++, (object)table)));
        objects.AddRange(CurrentPage.Charts.Select(chart => (chart.ZIndex, sequence++, (object)chart)));

        foreach (var item in objects.OrderBy(item => item.ZIndex).ThenBy(item => item.Sequence).Select(item => item.Item))
        {
            switch (item)
            {
                case StickerImage sticker:
                    AddObjectControl(CreateStickerControl(sticker), sticker.X, sticker.Y);
                    break;
                case PageTextBox textBox:
                    AddObjectControl(CreateTextBoxControl(textBox), textBox.X, textBox.Y);
                    break;
                case PageTable table:
                    AddObjectControl(CreateTableControl(table), table.X, table.Y);
                    break;
                case PageChart chart:
                    AddObjectControl(CreateChartControl(chart), chart.X, chart.Y);
                    break;
            }
        }
    }

    private void AddObjectControl(UIElement control, double x, double y)
    {
        Canvas.SetLeft(control, x);
        Canvas.SetTop(control, y);
        ObjectLayer.Children.Add(control);
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
        selectedTable = null;
        selectedTableControl = null;
        selectedChart = null;
        selectedChartControl = null;
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
            FontFamily = new FontFamily(pageTextBox.FontFamily),
            FontWeight = pageTextBox.IsBold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = pageTextBox.IsItalic ? FontStyles.Italic : FontStyles.Normal,
            TextDecorations = pageTextBox.IsUnderline ? TextDecorations.Underline : null,
            TextAlignment = ParseTextAlignment(pageTextBox.TextAlignment),
            Padding = new Thickness(4 + pageTextBox.IndentLevel * 22, 4, 4, 4),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        editor.SetValue(TextBlock.LineHeightProperty, pageTextBox.FontSize * pageTextBox.LineSpacing);
        editor.SetValue(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight);
        if (!string.Equals(pageTextBox.HighlightColor, "Transparent", StringComparison.OrdinalIgnoreCase))
        {
            editor.Background = (Brush)new BrushConverter().ConvertFromString(pageTextBox.HighlightColor)!;
        }
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
            Tag = pageTextBox,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(pageTextBox.Rotation)
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
        selectedTable = null;
        selectedTableControl = null;
        selectedChart = null;
        selectedChartControl = null;
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

    private Border CreateTableControl(PageTable table)
    {
        table.EnsureCellCount();
        var grid = new Grid
        {
            Background = Brushes.White
        };

        for (var row = 0; row < table.Rows; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition());
        }

        for (var column = 0; column < table.Columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < table.Rows; row++)
        {
            for (var column = 0; column < table.Columns; column++)
            {
                var index = row * table.Columns + column;
                var cell = new TextBox
                {
                    Text = table.Cells[index],
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    FontSize = table.FontSize,
                    Padding = new Thickness(6, 4, 6, 4),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                cell.TextChanged += (_, _) =>
                {
                    table.Cells[index] = cell.Text;
                    PageChanged?.Invoke(this, EventArgs.Empty);
                };
                cell.GotKeyboardFocus += (_, _) => SelectTable(table);
                cell.PreviewMouseRightButtonDown += (_, e) =>
                {
                    SelectTable(table);
                    e.Handled = false;
                };

                var cellBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    BorderThickness = new Thickness(0.5),
                    Child = cell
                };
                Grid.SetRow(cellBorder, row);
                Grid.SetColumn(cellBorder, column);
                grid.Children.Add(cellBorder);
            }
        }

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
            SelectTable(table);
        };
        moveThumb.DragDelta += (_, e) =>
        {
            table.X += e.HorizontalChange;
            table.Y += e.VerticalChange;
            if (selectedTableControl is not null)
            {
                Canvas.SetLeft(selectedTableControl, table.X);
                Canvas.SetTop(selectedTableControl, table.Y);
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
            table.Width = Math.Max(160, table.Width + e.HorizontalChange);
            table.Height = Math.Max(90, table.Height + e.VerticalChange);
            if (selectedTableControl is not null)
            {
                selectedTableControl.Width = table.Width;
                selectedTableControl.Height = table.Height;
            }

            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        var content = new Grid();
        content.Children.Add(grid);
        content.Children.Add(moveThumb);
        content.Children.Add(resizeThumb);

        var border = new Border
        {
            Width = table.Width,
            Height = table.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Child = content,
            Tag = table,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(table.Rotation)
        };

        border.MouseLeftButtonDown += (_, _) => SelectTable(table, border);
        border.MouseRightButtonDown += (_, e) =>
        {
            SelectTable(table, border);
            e.Handled = false;
        };
        return border;
    }

    private void SelectTable(PageTable table, Border? control = null)
    {
        ClearSelectionChrome();
        selectedSticker = null;
        selectedStickerControl = null;
        selectedTextBox = null;
        selectedTextBoxControl = null;
        selectedChart = null;
        selectedChartControl = null;
        selectedTable = table;
        selectedTableControl = control ?? ObjectLayer.Children
            .OfType<Border>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, table));

        if (selectedTableControl is not null)
        {
            selectedTableControl.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SetThumbsVisibility(selectedTableControl, Visibility.Visible);
        }
    }

    private Border CreateChartControl(PageChart chart)
    {
        chart.NormalizeData();

        var chartVisual = PageChartRenderer.Create(chart);
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
            SelectChart(chart);
        };
        moveThumb.DragDelta += (_, e) =>
        {
            chart.X += e.HorizontalChange;
            chart.Y += e.VerticalChange;
            if (selectedChartControl is not null)
            {
                Canvas.SetLeft(selectedChartControl, chart.X);
                Canvas.SetTop(selectedChartControl, chart.Y);
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
            chart.Width = Math.Max(220, chart.Width + e.HorizontalChange);
            chart.Height = Math.Max(150, chart.Height + e.VerticalChange);
            if (selectedChartControl is not null)
            {
                selectedChartControl.Width = chart.Width;
                selectedChartControl.Height = chart.Height;
            }

            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        var content = new Grid();
        content.Children.Add(chartVisual);
        content.Children.Add(moveThumb);
        content.Children.Add(resizeThumb);

        var border = new Border
        {
            Width = chart.Width,
            Height = chart.Height,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.White,
            Child = content,
            Tag = chart,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(chart.Rotation)
        };

        border.MouseLeftButtonDown += (_, e) =>
        {
            SelectChart(chart, border);
            if (e.ClickCount == 2)
            {
                EditSelectedChart();
            }
        };
        border.MouseRightButtonDown += (_, e) =>
        {
            SelectChart(chart, border);
            e.Handled = false;
        };
        return border;
    }

    private void SelectChart(PageChart chart, Border? control = null)
    {
        ClearSelectionChrome();
        selectedSticker = null;
        selectedStickerControl = null;
        selectedTextBox = null;
        selectedTextBoxControl = null;
        selectedTable = null;
        selectedTableControl = null;
        selectedChart = chart;
        selectedChartControl = control ?? ObjectLayer.Children
            .OfType<Border>()
            .FirstOrDefault(child => ReferenceEquals(child.Tag, chart));

        if (selectedChartControl is not null)
        {
            selectedChartControl.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            SetThumbsVisibility(selectedChartControl, Visibility.Visible);
        }
    }

    private void ApplySelectedTextBoxStyle(Action<PageTextBox> apply)
    {
        if (selectedTextBox is null)
        {
            return;
        }

        var textBox = selectedTextBox;
        apply(textBox);
        RenderObjects();
        SelectTextBox(textBox);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ModifySelectedTable(Action<PageTable> modify)
    {
        if (selectedTable is null)
        {
            return;
        }

        var table = selectedTable;
        modify(table);
        RenderObjects();
        SelectTable(table);
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private object? CloneSelectedObject()
    {
        if (selectedSticker is not null)
        {
            return CloneSticker(selectedSticker);
        }

        if (selectedTextBox is not null)
        {
            return CloneTextBox(selectedTextBox);
        }

        if (selectedTable is not null)
        {
            return CloneTable(selectedTable);
        }

        if (selectedChart is not null)
        {
            return CloneChart(selectedChart);
        }

        return null;
    }

    private void PasteInternalObject()
    {
        if (CurrentPage is null || objectClipboard is null)
        {
            return;
        }

        var clone = CloneObject(objectClipboard, 24);
        switch (clone)
        {
            case StickerImage sticker:
                sticker.ZIndex = GetNextZIndex();
                CurrentPage.Images.Add(sticker);
                RenderObjects();
                SelectSticker(sticker);
                break;
            case PageTextBox textBox:
                textBox.ZIndex = GetNextZIndex();
                CurrentPage.TextBoxes.Add(textBox);
                RenderObjects();
                SelectTextBox(textBox);
                break;
            case PageTable table:
                table.ZIndex = GetNextZIndex();
                CurrentPage.Tables.Add(table);
                RenderObjects();
                SelectTable(table);
                break;
            case PageChart chart:
                chart.ZIndex = GetNextZIndex();
                CurrentPage.Charts.Add(chart);
                RenderObjects();
                SelectChart(chart);
                break;
        }

        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static object CloneObject(object source, double offset)
    {
        return source switch
        {
            StickerImage sticker => CloneSticker(sticker, offset),
            PageTextBox textBox => CloneTextBox(textBox, offset),
            PageTable table => CloneTable(table, offset),
            PageChart chart => CloneChart(chart, offset),
            _ => throw new InvalidOperationException("Tipo de objeto no soportado.")
        };
    }

    private static StickerImage CloneSticker(StickerImage source, double offset = 0)
    {
        return new StickerImage
        {
            FileName = source.FileName,
            MimeType = source.MimeType,
            ImageBase64 = source.ImageBase64,
            X = source.X + offset,
            Y = source.Y + offset,
            Width = source.Width,
            Height = source.Height,
            Rotation = source.Rotation,
            ZIndex = source.ZIndex
        };
    }

    private static PageTextBox CloneTextBox(PageTextBox source, double offset = 0)
    {
        return new PageTextBox
        {
            Text = source.Text,
            X = source.X + offset,
            Y = source.Y + offset,
            Width = source.Width,
            Height = source.Height,
            FontSize = source.FontSize,
            FontFamily = source.FontFamily,
            Foreground = source.Foreground,
            HighlightColor = source.HighlightColor,
            LineSpacing = source.LineSpacing,
            IndentLevel = source.IndentLevel,
            IsBold = source.IsBold,
            IsItalic = source.IsItalic,
            IsUnderline = source.IsUnderline,
            TextAlignment = source.TextAlignment,
            Rotation = source.Rotation,
            ZIndex = source.ZIndex
        };
    }

    private static PageTable CloneTable(PageTable source, double offset = 0)
    {
        return new PageTable
        {
            X = source.X + offset,
            Y = source.Y + offset,
            Width = source.Width,
            Height = source.Height,
            Rows = source.Rows,
            Columns = source.Columns,
            FontSize = source.FontSize,
            Rotation = source.Rotation,
            ZIndex = source.ZIndex,
            Cells = source.Cells.ToList()
        };
    }

    private static PageChart CloneChart(PageChart source, double offset = 0)
    {
        return new PageChart
        {
            Title = source.Title,
            Type = source.Type,
            X = source.X + offset,
            Y = source.Y + offset,
            Width = source.Width,
            Height = source.Height,
            Rotation = source.Rotation,
            ZIndex = source.ZIndex,
            DataPoints = source.DataPoints
                .Select(point => new ChartDataPoint { Label = point.Label, Value = point.Value })
                .ToList()
        };
    }

    private void ApplyToSelectedObject(
        Action<StickerImage> stickerAction,
        Action<PageTextBox> textBoxAction,
        Action<PageTable> tableAction,
        Action<PageChart> chartAction)
    {
        switch (selectedSticker, selectedTextBox, selectedTable, selectedChart)
        {
            case ({ } sticker, null, null, null):
                stickerAction(sticker);
                RenderObjects();
                SelectSticker(sticker);
                break;
            case (null, { } textBox, null, null):
                textBoxAction(textBox);
                RenderObjects();
                SelectTextBox(textBox);
                break;
            case (null, null, { } table, null):
                tableAction(table);
                RenderObjects();
                SelectTable(table);
                break;
            case (null, null, null, { } chart):
                chartAction(chart);
                RenderObjects();
                SelectChart(chart);
                break;
            default:
                return;
        }

        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private int GetNextZIndex()
    {
        if (CurrentPage is null)
        {
            return 1;
        }

        return EnumerateZIndexes(CurrentPage).DefaultIfEmpty(0).Max() + 1;
    }

    private int GetMinZIndex()
    {
        if (CurrentPage is null)
        {
            return 0;
        }

        return EnumerateZIndexes(CurrentPage).DefaultIfEmpty(0).Min();
    }

    private static IEnumerable<int> EnumerateZIndexes(NotebookPage page)
    {
        foreach (var image in page.Images)
        {
            yield return image.ZIndex;
        }

        foreach (var textBox in page.TextBoxes)
        {
            yield return textBox.ZIndex;
        }

        foreach (var table in page.Tables)
        {
            yield return table.ZIndex;
        }

        foreach (var chart in page.Charts)
        {
            yield return chart.ZIndex;
        }
    }

    private static double NormalizeRotation(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
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

        if (selectedTableControl is not null)
        {
            selectedTableControl.BorderBrush = Brushes.Transparent;
            SetThumbsVisibility(selectedTableControl, Visibility.Collapsed);
        }

        if (selectedChartControl is not null)
        {
            selectedChartControl.BorderBrush = Brushes.Transparent;
            SetThumbsVisibility(selectedChartControl, Visibility.Collapsed);
        }
    }

    private void ClearSelection()
    {
        ClearSelectionChrome();
        selectedSticker = null;
        selectedStickerControl = null;
        selectedTextBox = null;
        selectedTextBoxControl = null;
        selectedTable = null;
        selectedTableControl = null;
        selectedChart = null;
        selectedChartControl = null;
    }

    private void PageHost_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsObjectInteraction(e.OriginalSource as DependencyObject))
        {
            return;
        }

        ClearSelection();
        Focus();
    }

    private static bool IsObjectInteraction(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: StickerImage or PageTextBox or PageTable or PageChart })
            {
                return true;
            }

            if (source is TextBox or Thumb)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
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

    private static byte[] RenderElementToPng(UIElement element, double width, double height, double scale)
    {
        scale = Math.Clamp(scale, 1, 4);
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * scale),
            (int)Math.Ceiling(height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
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

    private static string MakeCroppedFileName(string fileNameWithoutExtension)
    {
        var safeName = string.IsNullOrWhiteSpace(fileNameWithoutExtension)
            ? "imagen"
            : fileNameWithoutExtension.Trim();
        return $"{safeName}_recorte.png";
    }

    private static TextAlignment ParseTextAlignment(string alignment)
    {
        return alignment switch
        {
            "Center" => TextAlignment.Center,
            "Right" => TextAlignment.Right,
            "Justify" => TextAlignment.Justify,
            _ => TextAlignment.Left
        };
    }

    private static string PrefixTextLines(string text, Func<int, string> prefixFactory)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var contentIndex = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripListPrefix(lines[index]);
            if (string.IsNullOrWhiteSpace(line))
            {
                lines[index] = line;
                continue;
            }

            lines[index] = prefixFactory(contentIndex) + line.TrimStart();
            contentIndex++;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string StripListPrefix(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("• ", StringComparison.Ordinal))
        {
            return trimmed[2..];
        }

        var dotIndex = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (dotIndex > 0 && trimmed[..dotIndex].All(char.IsDigit))
        {
            return trimmed[(dotIndex + 2)..];
        }

        return line;
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
        if (Keyboard.FocusedElement is TextBox && !IsTextFormattingShortcut(e))
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
        {
            CopySelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.X)
        {
            CutSelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.D)
        {
            DuplicateSelectedObject();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.B)
        {
            ToggleSelectedTextBold();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.I)
        {
            ToggleSelectedTextItalic();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.U)
        {
            ToggleSelectedTextUnderline();
            e.Handled = true;
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
        {
            PasteObjectOrImage();
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

    private static bool IsTextFormattingShortcut(KeyEventArgs e)
    {
        return (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            && (e.Key == Key.B || e.Key == Key.I || e.Key == Key.U);
    }

    private void InsertTextMenuItem_Click(object sender, RoutedEventArgs e)
    {
        InsertTextBox();
    }

    private void InsertTableMenuItem_Click(object sender, RoutedEventArgs e)
    {
        InsertTable();
    }

    private void InsertChartMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ChartDataDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            InsertChart(dialog.Chart);
        }
    }

    private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        PasteObjectOrImage();
    }

    private void CutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CutSelectedObject();
    }

    private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CopySelectedObject();
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DuplicateSelectedObject();
    }

    private void EditChartMenuItem_Click(object sender, RoutedEventArgs e)
    {
        EditSelectedChart();
    }

    private void DeleteSelectedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedObject();
    }

    private void BringToFrontMenuItem_Click(object sender, RoutedEventArgs e)
    {
        BringSelectedObjectToFront();
    }

    private void SendToBackMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SendSelectedObjectToBack();
    }

    private void RotateRightMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RotateSelectedObject(15);
    }

    private void RotateLeftMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RotateSelectedObject(-15);
    }

    public void DeleteSelectedObject()
    {
        if (CurrentPage is null || (selectedSticker is null && selectedTextBox is null && selectedTable is null && selectedChart is null))
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
        else if (selectedTable is not null)
        {
            CurrentPage.Tables.Remove(selectedTable);
        }
        else if (selectedChart is not null)
        {
            CurrentPage.Charts.Remove(selectedChart);
        }

        RenderObjects();
        PageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AddTableRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddRowToSelectedTable();
    }

    private void AddTableColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddColumnToSelectedTable();
    }

    private void RemoveTableRowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveRowFromSelectedTable();
    }

    private void RemoveTableColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        RemoveColumnFromSelectedTable();
    }
}
