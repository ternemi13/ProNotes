using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class NotebookSummary
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = "ProNotes";
    public string FilePath { get; init; } = string.Empty;
    public string CoverColor { get; init; } = "#2563EB";
    public string AccentColor { get; init; } = "#93C5FD";
    public string? CoverImageBase64 { get; init; }
    public double CoverImageOffsetX { get; init; }
    public double CoverImageOffsetY { get; init; }
    public double CoverImageScale { get; init; } = 1;
    public string TemplateName { get; init; } = "Classic";
    public string TitleColor { get; init; } = "White";
    public string SubtitleColor { get; init; } = "#DDEBFF";
    public DateTimeOffset UpdatedAt { get; init; }

    public static NotebookSummary FromNotebook(Notebook notebook, string filePath)
    {
        return new NotebookSummary
        {
            Id = notebook.Id,
            Title = string.IsNullOrWhiteSpace(notebook.Cover.Title) ? notebook.Title : notebook.Cover.Title,
            Subtitle = string.IsNullOrWhiteSpace(notebook.Cover.Subtitle) ? "ProNotes" : notebook.Cover.Subtitle,
            FilePath = filePath,
            CoverColor = notebook.Cover.BackgroundColor,
            AccentColor = notebook.Cover.AccentColor,
            CoverImageBase64 = notebook.Cover.BackgroundImageBase64,
            CoverImageOffsetX = notebook.Cover.BackgroundImageOffsetX,
            CoverImageOffsetY = notebook.Cover.BackgroundImageOffsetY,
            CoverImageScale = notebook.Cover.BackgroundImageScale <= 0 ? 1 : notebook.Cover.BackgroundImageScale,
            TemplateName = notebook.Cover.TemplateName,
            TitleColor = IsLight(notebook.Cover.BackgroundColor) ? "#111827" : "White",
            SubtitleColor = IsLight(notebook.Cover.BackgroundColor) ? "#475569" : "#DDEBFF",
            UpdatedAt = notebook.UpdatedAt
        };
    }

    private static bool IsLight(string color)
    {
        try
        {
            var parsed = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);
            var luminance = 0.2126 * parsed.R + 0.7152 * parsed.G + 0.0722 * parsed.B;
            return luminance > 180;
        }
        catch
        {
            return false;
        }
    }
}
