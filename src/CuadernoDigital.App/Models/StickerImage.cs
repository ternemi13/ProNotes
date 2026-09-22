namespace CuadernoDigital.App.Models;

public sealed class StickerImage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/png";
    public string ImageBase64 { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 240;
    public double Height { get; set; } = 160;
    public double Rotation { get; set; }
}
