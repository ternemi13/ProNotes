using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        RenderPreview();
        CoverChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderPreview()
    {
        if (Cover is null || PreviewBorder is null)
        {
            return;
        }

        EnsureCoverDefaults(Cover);
        var backgroundColor = ParseColor(Cover.BackgroundColor, Color.FromRgb(37, 99, 235));
        var accentColor = ParseColor(Cover.AccentColor, Color.FromRgb(147, 197, 253));
        var foreground = IsLight(backgroundColor) && Cover.TemplateName == "Minimal"
            ? new SolidColorBrush(Color.FromRgb(17, 24, 39))
            : Brushes.White;
        var secondary = IsLight(backgroundColor) && Cover.TemplateName == "Minimal"
            ? new SolidColorBrush(Color.FromRgb(71, 85, 105))
            : new SolidColorBrush(Color.FromRgb(226, 232, 240));

        PreviewBorder.Background = new SolidColorBrush(backgroundColor);
        PreviewOverlay.Background = new SolidColorBrush(backgroundColor);
        PreviewOverlay.Opacity = string.IsNullOrWhiteSpace(Cover.BackgroundImageBase64) ? 1 : 0.72;
        PreviewAccent.Background = new SolidColorBrush(accentColor);
        PreviewAccent.Visibility = Cover.TemplateName == "Minimal" ? Visibility.Collapsed : Visibility.Visible;
        PreviewTitle.Text = Cover.Title;
        PreviewSubtitle.Text = Cover.Subtitle;
        PreviewTitle.Foreground = foreground;
        PreviewSubtitle.Foreground = secondary;
        PreviewTitle.HorizontalAlignment = Cover.TemplateName == "Minimal" ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        PreviewSubtitle.HorizontalAlignment = Cover.TemplateName == "Minimal" ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        PreviewImageLayer.Background = CreateImageBrush(Cover.BackgroundImageBase64);
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

    private static Brush? CreateImageBrush(string? imageBase64)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(imageBase64);
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();

            return new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill
            };
        }
        catch
        {
            return null;
        }
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
