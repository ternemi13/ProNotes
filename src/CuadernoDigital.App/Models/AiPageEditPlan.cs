namespace CuadernoDigital.App.Models;

public sealed class AiPageEditPlan
{
    public string Summary { get; set; } = "Listo. Agregue el contenido a la pagina.";
    public List<AiPageTextBlock> TextBlocks { get; set; } = [];
    public List<AiPageInkShape> InkShapes { get; set; } = [];
}

public sealed class AiPageTextBlock
{
    public string Text { get; set; } = string.Empty;
    public double X { get; set; } = 120;
    public double Y { get; set; } = 120;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 160;
    public double FontSize { get; set; } = 20;
    public string FontFamily { get; set; } = "Segoe UI";
    public string Foreground { get; set; } = "#111827";
    public string HighlightColor { get; set; } = "Transparent";
    public bool IsBold { get; set; }
    public bool IsItalic { get; set; }
    public bool IsUnderline { get; set; }
    public string TextAlignment { get; set; } = "Left";
}

public sealed class AiPageInkShape
{
    public string Type { get; set; } = "line";
    public double X { get; set; } = 160;
    public double Y { get; set; } = 160;
    public double X2 { get; set; } = 320;
    public double Y2 { get; set; } = 220;
    public double Width { get; set; } = 180;
    public double Height { get; set; } = 120;
    public string Stroke { get; set; } = "#2563EB";
    public double StrokeWidth { get; set; } = 4;
    public List<AiPagePoint> Points { get; set; } = [];
}

public sealed class AiPagePoint
{
    public double X { get; set; }
    public double Y { get; set; }
}
