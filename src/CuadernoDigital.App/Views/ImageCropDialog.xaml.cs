using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CuadernoDigital.App.Views;

public partial class ImageCropDialog : Window
{
    private readonly BitmapSource source;
    private bool isUpdating;

    public ImageCropDialog(BitmapSource source)
    {
        InitializeComponent();
        this.source = source;
        OriginalImage.Source = source;
        ImageSizeText.Text = $"{source.PixelWidth} x {source.PixelHeight}px";
        ConfigureSliders();
        UpdatePreview();
    }

    public byte[] CroppedPngBytes { get; private set; } = [];

    public static BitmapSource DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ConfigureSliders()
    {
        isUpdating = true;
        CropXSlider.Maximum = Math.Max(0, source.PixelWidth - 1);
        CropYSlider.Maximum = Math.Max(0, source.PixelHeight - 1);
        CropWidthSlider.Maximum = source.PixelWidth;
        CropHeightSlider.Maximum = source.PixelHeight;
        CropXSlider.Value = 0;
        CropYSlider.Value = 0;
        CropWidthSlider.Value = source.PixelWidth;
        CropHeightSlider.Value = source.PixelHeight;
        isUpdating = false;
    }

    private void CropSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (isUpdating || CropXSlider is null)
        {
            return;
        }

        NormalizeSliderBounds();
        UpdatePreview();
    }

    private void NormalizeSliderBounds()
    {
        isUpdating = true;
        CropWidthSlider.Maximum = Math.Max(1, source.PixelWidth - CropXSlider.Value);
        CropHeightSlider.Maximum = Math.Max(1, source.PixelHeight - CropYSlider.Value);
        CropWidthSlider.Value = Math.Clamp(CropWidthSlider.Value, 1, CropWidthSlider.Maximum);
        CropHeightSlider.Value = Math.Clamp(CropHeightSlider.Value, 1, CropHeightSlider.Maximum);
        isUpdating = false;
    }

    private void UpdatePreview()
    {
        var rect = GetCropRect();
        var cropped = new CroppedBitmap(source, rect);
        cropped.Freeze();
        PreviewImage.Source = cropped;
        CropInfoText.Text = $"Recorte: {rect.X}, {rect.Y}, {rect.Width} x {rect.Height}px";
    }

    private Int32Rect GetCropRect()
    {
        var x = (int)Math.Round(CropXSlider.Value);
        var y = (int)Math.Round(CropYSlider.Value);
        var width = (int)Math.Round(CropWidthSlider.Value);
        var height = (int)Math.Round(CropHeightSlider.Value);

        x = Math.Clamp(x, 0, Math.Max(0, source.PixelWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, source.PixelHeight - 1));
        width = Math.Clamp(width, 1, source.PixelWidth - x);
        height = Math.Clamp(height, 1, source.PixelHeight - y);
        return new Int32Rect(x, y, width, height);
    }

    private void UseFullImage_Click(object sender, RoutedEventArgs e)
    {
        ConfigureSliders();
        UpdatePreview();
        CroppedPngBytes = EncodePng(source);
        DialogResult = true;
    }

    private void Insert_Click(object sender, RoutedEventArgs e)
    {
        var cropped = new CroppedBitmap(source, GetCropRect());
        CroppedPngBytes = EncodePng(cropped);
        DialogResult = true;
    }

    private static byte[] EncodePng(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
