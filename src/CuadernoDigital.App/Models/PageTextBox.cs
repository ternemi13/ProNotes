namespace CuadernoDigital.App.Models;

public sealed class PageTextBox
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "Escribe aqui...";
    public double X { get; set; } = 120;
    public double Y { get; set; } = 120;
    public double Width { get; set; } = 260;
    public double Height { get; set; } = 120;
    public double FontSize { get; set; } = 18;
    public string FontFamily { get; set; } = "Segoe UI";
    public string Foreground { get; set; } = "#111827";
    public string HighlightColor { get; set; } = "Transparent";
    public double LineSpacing { get; set; } = 1.2;
    public int IndentLevel { get; set; }
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public string TextAlignment { get; set; } = "Left";
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
}
