namespace CuadernoDigital.App.Models;

public sealed class NotebookCover
{
    public string BackgroundColor { get; set; } = "#2563EB";
    public string AccentColor { get; set; } = "#93C5FD";
    public string? BackgroundImageBase64 { get; set; }
    public string? BackgroundImageMimeType { get; set; }
    public double BackgroundImageOffsetX { get; set; }
    public double BackgroundImageOffsetY { get; set; }
    public double BackgroundImageScale { get; set; } = 1;
    public string TemplateName { get; set; } = "Classic";
    public string Title { get; set; } = "Nuevo cuaderno";
    public string Subtitle { get; set; } = "ProNotes";
}
