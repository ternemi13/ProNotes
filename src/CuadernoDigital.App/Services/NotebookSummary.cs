using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class NotebookSummary
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public string CoverColor { get; init; } = "#2563EB";
    public DateTimeOffset UpdatedAt { get; init; }

    public static NotebookSummary FromNotebook(Notebook notebook, string filePath)
    {
        return new NotebookSummary
        {
            Id = notebook.Id,
            Title = notebook.Title,
            FilePath = filePath,
            CoverColor = notebook.Cover.BackgroundColor,
            UpdatedAt = notebook.UpdatedAt
        };
    }
}
