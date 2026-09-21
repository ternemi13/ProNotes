using CuadernoDigital.App.Services;
using Xunit;

namespace CuadernoDigital.Tests;

public sealed class NotebookRepositoryTests
{
    [Fact]
    public void CreateSaveLoad_RoundTripsNotebook()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);

        var notebook = repository.Create("Calculo II");
        notebook.Pages[0].InkBase64 = "sample";
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);

        Assert.Equal("Calculo II", loaded.Title);
        Assert.Single(loaded.Pages);
        Assert.Equal("sample", loaded.Pages[0].InkBase64);
    }
}
