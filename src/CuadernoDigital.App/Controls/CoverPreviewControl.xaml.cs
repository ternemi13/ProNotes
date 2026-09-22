using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CuadernoDigital.App.Controls;

public partial class CoverPreviewControl : UserControl
{
    public static readonly DependencyProperty CoverTitleProperty =
        DependencyProperty.Register(nameof(CoverTitle), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("Nuevo cuaderno", OnCoverChanged));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("ProNotes", OnCoverChanged));

    public static readonly DependencyProperty BackgroundColorProperty =
        DependencyProperty.Register(nameof(BackgroundColor), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("#2563EB", OnCoverChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("#93C5FD", OnCoverChanged));

    public static readonly DependencyProperty ImageBase64Property =
        DependencyProperty.Register(nameof(ImageBase64), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata(null, OnCoverChanged));

    public static readonly DependencyProperty ImageOffsetXProperty =
        DependencyProperty.Register(nameof(ImageOffsetX), typeof(double), typeof(CoverPreviewControl), new PropertyMetadata(0d, OnCoverChanged));

    public static readonly DependencyProperty ImageOffsetYProperty =
        DependencyProperty.Register(nameof(ImageOffsetY), typeof(double), typeof(CoverPreviewControl), new PropertyMetadata(0d, OnCoverChanged));

    public static readonly DependencyProperty ImageScaleProperty =
        DependencyProperty.Register(nameof(ImageScale), typeof(double), typeof(CoverPreviewControl), new PropertyMetadata(1d, OnCoverChanged));

    public static readonly DependencyProperty TemplateNameProperty =
        DependencyProperty.Register(nameof(TemplateName), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("Classic", OnCoverChanged));

    public static readonly DependencyProperty TitleColorProperty =
        DependencyProperty.Register(nameof(TitleColor), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("White", OnCoverChanged));

    public static readonly DependencyProperty SubtitleColorProperty =
        DependencyProperty.Register(nameof(SubtitleColor), typeof(string), typeof(CoverPreviewControl), new PropertyMetadata("#DDEBFF", OnCoverChanged));

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(CoverPreviewControl), new PropertyMetadata(new CornerRadius(6)));

    public static readonly DependencyProperty TitleFontSizeProperty =
        DependencyProperty.Register(nameof(TitleFontSize), typeof(double), typeof(CoverPreviewControl), new PropertyMetadata(23d));

    public static readonly DependencyProperty SubtitleFontSizeProperty =
        DependencyProperty.Register(nameof(SubtitleFontSize), typeof(double), typeof(CoverPreviewControl), new PropertyMetadata(13d));

    public CoverPreviewControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    public string CoverTitle
    {
        get => (string)GetValue(CoverTitleProperty);
        set => SetValue(CoverTitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string BackgroundColor
    {
        get => (string)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public string AccentColor
    {
        get => (string)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public string? ImageBase64
    {
        get => (string?)GetValue(ImageBase64Property);
        set => SetValue(ImageBase64Property, value);
    }

    public double ImageOffsetX
    {
        get => (double)GetValue(ImageOffsetXProperty);
        set => SetValue(ImageOffsetXProperty, value);
    }

    public double ImageOffsetY
    {
        get => (double)GetValue(ImageOffsetYProperty);
        set => SetValue(ImageOffsetYProperty, value);
    }

    public double ImageScale
    {
        get => (double)GetValue(ImageScaleProperty);
        set => SetValue(ImageScaleProperty, value);
    }

    public string TemplateName
    {
        get => (string)GetValue(TemplateNameProperty);
        set => SetValue(TemplateNameProperty, value);
    }

    public string TitleColor
    {
        get => (string)GetValue(TitleColorProperty);
        set => SetValue(TitleColorProperty, value);
    }

    public string SubtitleColor
    {
        get => (string)GetValue(SubtitleColorProperty);
        set => SetValue(SubtitleColorProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public double TitleFontSize
    {
        get => (double)GetValue(TitleFontSizeProperty);
        set => SetValue(TitleFontSizeProperty, value);
    }

    public double SubtitleFontSize
    {
        get => (double)GetValue(SubtitleFontSizeProperty);
        set => SetValue(SubtitleFontSizeProperty, value);
    }

    private static void OnCoverChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((CoverPreviewControl)dependencyObject).Render();
    }

    private void Render()
    {
        if (RootBorder is null)
        {
            return;
        }

        var background = ParseColor(BackgroundColor, Color.FromRgb(37, 99, 235));
        var accent = ParseColor(AccentColor, Color.FromRgb(147, 197, 253));
        var hasImage = !string.IsNullOrWhiteSpace(ImageBase64);

        RootBorder.Background = new SolidColorBrush(background);
        ColorOverlay.Background = new SolidColorBrush(background);
        ColorOverlay.Opacity = hasImage ? 0.68 : 1;
        AccentBand.Background = new SolidColorBrush(accent);
        AccentBand.Visibility = string.Equals(TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Collapsed
            : Visibility.Visible;

        CoverImage.Source = hasImage ? CreateBitmap(ImageBase64) : null;
        CoverImage.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(Math.Clamp(ImageScale, 0.4, 3), Math.Clamp(ImageScale, 0.4, 3)),
                new TranslateTransform(ImageOffsetX * Math.Max(1, ActualWidth) * 0.5, ImageOffsetY * Math.Max(1, ActualHeight) * 0.5)
            }
        };

        TitleText.Text = string.IsNullOrWhiteSpace(CoverTitle) ? "Nuevo cuaderno" : CoverTitle;
        SubtitleText.Text = string.IsNullOrWhiteSpace(Subtitle) ? "ProNotes" : Subtitle;
        TitleText.Foreground = new SolidColorBrush(ParseColor(TitleColor, Colors.White));
        SubtitleText.Foreground = new SolidColorBrush(ParseColor(SubtitleColor, Color.FromRgb(221, 235, 255)));
        TextPanel.HorizontalAlignment = string.Equals(TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Stretch;
        TitleText.TextAlignment = string.Equals(TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? TextAlignment.Center
            : TextAlignment.Left;
        SubtitleText.TextAlignment = TitleText.TextAlignment;
    }

    private static BitmapImage? CreateBitmap(string? imageBase64)
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
            return bitmap;
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
}
