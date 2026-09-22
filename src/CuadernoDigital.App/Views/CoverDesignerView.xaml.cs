using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CuadernoDigital.App.Models;
using Microsoft.Win32;

namespace CuadernoDigital.App.Views;

public partial class CoverDesignerView : UserControl
{
    public static readonly DependencyProperty CoverProperty =
        DependencyProperty.Register(
            nameof(Cover),
            typeof(NotebookCover),
            typeof(CoverDesignerView),
            new PropertyMetadata(null, OnCoverChanged));

    private bool isLoading;
    private bool isDraggingPreview;
    private Point dragStart;
    private double dragStartOffsetX;
    private double dragStartOffsetY;

    public CoverDesignerView()
    {
        InitializeComponent();
    }

    public event EventHandler? CoverChanged;

    public NotebookCover? Cover
    {
        get => (NotebookCover?)GetValue(CoverProperty);
        set => SetValue(CoverProperty, value);
    }

    private static void OnCoverChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((CoverDesignerView)dependencyObject).LoadCover((NotebookCover?)args.NewValue);
    }

    private void LoadCover(NotebookCover? cover)
    {
        isLoading = true;
        EnsureCoverDefaults(cover);

        TitleBox.Text = cover?.Title ?? string.Empty;
        SubtitleBox.Text = cover?.Subtitle ?? string.Empty;
        SelectColorItem(cover?.BackgroundColor, cover?.AccentColor);
        ImageScaleSlider.Value = cover?.BackgroundImageScale ?? 1;
        ImageOffsetXSlider.Value = cover?.BackgroundImageOffsetX ?? 0;
        ImageOffsetYSlider.Value = cover?.BackgroundImageOffsetY ?? 0;

        isLoading = false;
        RenderPreview();
    }

    private void CoverInput_Changed(object sender, RoutedEventArgs e)
    {
        if (isLoading || Cover is null)
        {
            return;
        }

        Cover.Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "Nuevo cuaderno" : TitleBox.Text.Trim();
        Cover.Subtitle = string.IsNullOrWhiteSpace(SubtitleBox.Text) ? "ProNotes" : SubtitleBox.Text.Trim();
        ApplySelectedColors(Cover);
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ImageFrame_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isLoading || Cover is null)
        {
            return;
        }

        Cover.BackgroundImageScale = Math.Clamp(ImageScaleSlider.Value, 0.7, 2.4);
        Cover.BackgroundImageOffsetX = Math.Clamp(ImageOffsetXSlider.Value, -1, 1);
        Cover.BackgroundImageOffsetY = Math.Clamp(ImageOffsetYSlider.Value, -1, 1);
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Template_Click(object sender, RoutedEventArgs e)
    {
        if (Cover is null || sender is not Button button || button.Tag is not string template)
        {
            return;
        }

        Cover.TemplateName = template;
        switch (template)
        {
            case "Band":
                Cover.BackgroundColor = "#111827";
                Cover.AccentColor = "#FACC15";
                break;
            case "Minimal":
                Cover.BackgroundColor = "#F8FAFC";
                Cover.AccentColor = "#2563EB";
                break;
            case "Photo":
                Cover.BackgroundColor = "#0F172A";
                Cover.AccentColor = "#38BDF8";
                break;
            case "Classic":
            default:
                Cover.BackgroundColor = "#2563EB";
                Cover.AccentColor = "#93C5FD";
                break;
        }

        isLoading = true;
        SelectColorItem(Cover.BackgroundColor, Cover.AccentColor);
        isLoading = false;
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ChooseImage_Click(object sender, RoutedEventArgs e)
    {
        if (Cover is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Imagen de portada",
            Filter = "Imagenes|*.png;*.jpg;*.jpeg;*.bmp|Todos los archivos|*.*"
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        var bytes = File.ReadAllBytes(dialog.FileName);
        Cover.BackgroundImageBase64 = Convert.ToBase64String(bytes);
        Cover.BackgroundImageMimeType = GetMimeType(dialog.FileName);
        Cover.TemplateName = "Photo";
        Cover.BackgroundImageScale = Math.Max(1, Cover.BackgroundImageScale);
        isLoading = true;
        ImageScaleSlider.Value = Cover.BackgroundImageScale;
        isLoading = false;
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ClearImage_Click(object sender, RoutedEventArgs e)
    {
        if (Cover is null)
        {
            return;
        }

        Cover.BackgroundImageBase64 = null;
        Cover.BackgroundImageMimeType = null;
        Cover.BackgroundImageOffsetX = 0;
        Cover.BackgroundImageOffsetY = 0;
        Cover.BackgroundImageScale = 1;
        isLoading = true;
        ImageScaleSlider.Value = 1;
        ImageOffsetXSlider.Value = 0;
        ImageOffsetYSlider.Value = 0;
        isLoading = false;
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CenterImage_Click(object sender, RoutedEventArgs e)
    {
        if (Cover is null)
        {
            return;
        }

        Cover.BackgroundImageOffsetX = 0;
        Cover.BackgroundImageOffsetY = 0;
        isLoading = true;
        ImageOffsetXSlider.Value = 0;
        ImageOffsetYSlider.Value = 0;
        isLoading = false;
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Cover is null || string.IsNullOrWhiteSpace(Cover.BackgroundImageBase64))
        {
            return;
        }

        isDraggingPreview = true;
        dragStart = e.GetPosition(Preview);
        dragStartOffsetX = Cover.BackgroundImageOffsetX;
        dragStartOffsetY = Cover.BackgroundImageOffsetY;
        Preview.CaptureMouse();
        e.Handled = true;
    }

    private void Preview_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDraggingPreview || Cover is null)
        {
            return;
        }

        var position = e.GetPosition(Preview);
        var delta = position - dragStart;
        Cover.BackgroundImageOffsetX = Math.Clamp(dragStartOffsetX + delta.X / Math.Max(1, Preview.ActualWidth * 0.5), -1, 1);
        Cover.BackgroundImageOffsetY = Math.Clamp(dragStartOffsetY + delta.Y / Math.Max(1, Preview.ActualHeight * 0.5), -1, 1);

        isLoading = true;
        ImageOffsetXSlider.Value = Cover.BackgroundImageOffsetX;
        ImageOffsetYSlider.Value = Cover.BackgroundImageOffsetY;
        isLoading = false;
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Preview_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (isDraggingPreview)
        {
            Preview.ReleaseMouseCapture();
        }

        isDraggingPreview = false;
    }

    private void RenderPreview()
    {
        if (Cover is null || Preview is null)
        {
            return;
        }

        EnsureCoverDefaults(Cover);
        var backgroundColor = ParseColor(Cover.BackgroundColor, Color.FromRgb(37, 99, 235));
        var titleColor = IsLight(backgroundColor) && string.Equals(Cover.TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? "#111827"
            : "White";
        var subtitleColor = IsLight(backgroundColor) && string.Equals(Cover.TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? "#475569"
            : "#E2E8F0";

        Preview.CoverTitle = Cover.Title;
        Preview.Subtitle = Cover.Subtitle;
        Preview.BackgroundColor = Cover.BackgroundColor;
        Preview.AccentColor = Cover.AccentColor;
        Preview.ImageBase64 = Cover.BackgroundImageBase64;
        Preview.ImageScale = Cover.BackgroundImageScale;
        Preview.ImageOffsetX = Cover.BackgroundImageOffsetX;
        Preview.ImageOffsetY = Cover.BackgroundImageOffsetY;
        Preview.TemplateName = Cover.TemplateName;
        Preview.TitleColor = titleColor;
        Preview.SubtitleColor = subtitleColor;
    }

    private static void EnsureCoverDefaults(NotebookCover? cover)
    {
        if (cover is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cover.BackgroundColor))
        {
            cover.BackgroundColor = "#2563EB";
        }

        if (string.IsNullOrWhiteSpace(cover.AccentColor))
        {
            cover.AccentColor = "#93C5FD";
        }

        if (string.IsNullOrWhiteSpace(cover.TemplateName))
        {
            cover.TemplateName = "Classic";
        }

        if (string.IsNullOrWhiteSpace(cover.Title))
        {
            cover.Title = "Nuevo cuaderno";
        }

        if (string.IsNullOrWhiteSpace(cover.Subtitle))
        {
            cover.Subtitle = "ProNotes";
        }

        if (cover.BackgroundImageScale <= 0)
        {
            cover.BackgroundImageScale = 1;
        }
    }

    private void ApplySelectedColors(NotebookCover cover)
    {
        if (ColorBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var colors = tag.Split('|', StringSplitOptions.TrimEntries);
            if (colors.Length == 2)
            {
                cover.BackgroundColor = colors[0];
                cover.AccentColor = colors[1];
            }
        }
    }

    private void SelectColorItem(string? backgroundColor, string? accentColor)
    {
        foreach (var item in ColorBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is not string tag)
            {
                continue;
            }

            var colors = tag.Split('|', StringSplitOptions.TrimEntries);
            if (colors.Length == 2
                && string.Equals(colors[0], backgroundColor, StringComparison.OrdinalIgnoreCase)
                && string.Equals(colors[1], accentColor, StringComparison.OrdinalIgnoreCase))
            {
                ColorBox.SelectedItem = item;
                return;
            }
        }

        ColorBox.SelectedIndex = -1;
    }

    private static Color ParseColor(string? color, Color fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(color)
                ? fallback
                : (Color)ColorConverter.ConvertFromString(color);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool IsLight(Color color)
    {
        var luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        return luminance > 180;
    }

    private static string GetMimeType(string filePath)
    {
        return Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };
    }
}
