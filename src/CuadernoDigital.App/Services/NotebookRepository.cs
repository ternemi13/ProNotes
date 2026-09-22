using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class NotebookRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string LibraryDirectory { get; }

    public NotebookRepository(string? libraryDirectory = null)
    {
        LibraryDirectory = libraryDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProNotes");
        Directory.CreateDirectory(LibraryDirectory);
    }

    public IReadOnlyList<NotebookSummary> GetSummaries()
    {
        Directory.CreateDirectory(LibraryDirectory);
        return Directory
            .EnumerateFiles(LibraryDirectory, "*.cdgz", SearchOption.TopDirectoryOnly)
            .Select(TryReadSummary)
            .Where(summary => summary is not null)
            .Cast<NotebookSummary>()
            .OrderByDescending(summary => summary.UpdatedAt)
            .ToList();
    }

    public Notebook Create(string title)
    {
        var notebook = new Notebook
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Nuevo cuaderno" : title.Trim(),
            Cover =
            {
                Title = string.IsNullOrWhiteSpace(title) ? "Nuevo cuaderno" : title.Trim(),
                Subtitle = "ProNotes"
            },
            Pages =
            [
                new NotebookPage
                {
                    Title = "Pagina 1",
                    BackgroundType = PageBackgroundType.Grid
                }
            ]
        };

        Save(notebook, GetNotebookPath(notebook));
        return notebook;
    }

    public Notebook Load(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var metadataEntry = archive.GetEntry("metadata.json")
            ?? throw new InvalidDataException("El cuaderno no tiene metadata.json.");

        using var metadataStream = metadataEntry.Open();
        var package = JsonSerializer.Deserialize<NotebookPackage>(metadataStream, JsonOptions)
            ?? throw new InvalidDataException("No se pudo leer la metadata del cuaderno.");

        var notebook = package.ToNotebook();
        notebook.Pages.Clear();

        foreach (var pageFile in package.Pages)
        {
            var entry = archive.GetEntry($"pages/{pageFile}");
            if (entry is null)
            {
                continue;
            }

            using var stream = entry.Open();
            var page = JsonSerializer.Deserialize<NotebookPage>(stream, JsonOptions);
            if (page is not null)
            {
                notebook.Pages.Add(page);
            }
        }

        if (notebook.Pages.Count == 0)
        {
            notebook.Pages.Add(new NotebookPage { Title = "Pagina 1" });
        }

        return notebook;
    }

    public void Save(Notebook notebook, string? filePath = null)
    {
        notebook.UpdatedAt = DateTimeOffset.UtcNow;

        var targetPath = filePath ?? GetNotebookPath(notebook);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        var tempPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $"{Path.GetFileName(targetPath)}.tmp");

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
        {
            var package = NotebookPackage.FromNotebook(notebook);
            WriteJsonEntry(archive, "metadata.json", package);

            foreach (var page in notebook.Pages)
            {
                WriteJsonEntry(archive, $"pages/{GetPageFileName(page)}", page);
            }
        }

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        File.Move(tempPath, targetPath);
    }

    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    public string GetNotebookPath(Notebook notebook)
    {
        var safeTitle = string.Join("_", notebook.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle))
        {
            safeTitle = "Cuaderno";
        }

        return Path.Combine(LibraryDirectory, $"{safeTitle}-{notebook.Id[..8]}.cdgz");
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string path, T value)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private NotebookSummary? TryReadSummary(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var metadataEntry = archive.GetEntry("metadata.json");
            if (metadataEntry is null)
            {
                return null;
            }

            using var stream = metadataEntry.Open();
            var package = JsonSerializer.Deserialize<NotebookPackage>(stream, JsonOptions);
            return package is null ? null : NotebookSummary.FromNotebook(package.ToNotebook(), filePath);
        }
        catch
        {
            return null;
        }
    }

    private static string GetPageFileName(NotebookPage page) => $"page_{page.Id}.json";

    private sealed class NotebookPackage
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public NotebookCover Cover { get; set; } = new();
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public List<string> Pages { get; set; } = [];
        public List<AiChatMessage> AiChatHistory { get; set; } = [];

        public static NotebookPackage FromNotebook(Notebook notebook)
        {
            return new NotebookPackage
            {
                Id = notebook.Id,
                Title = notebook.Title,
                Cover = notebook.Cover,
                CreatedAt = notebook.CreatedAt,
                UpdatedAt = notebook.UpdatedAt,
                Pages = notebook.Pages.Select(GetPageFileName).ToList(),
                AiChatHistory = notebook.AiChatHistory.ToList()
            };
        }

        public Notebook ToNotebook()
        {
            return new Notebook
            {
                Id = Id,
                Title = Title,
                Cover = Cover,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                AiChatHistory = AiChatHistory
            };
        }
    }
}
