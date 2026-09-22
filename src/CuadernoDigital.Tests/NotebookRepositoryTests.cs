using CuadernoDigital.App.Services;
using CuadernoDigital.App.Models;
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

    [Fact]
    public void SaveLoad_RoundTripsEmbeddedImages()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);

        var notebook = repository.Create("Imagenes");
        notebook.Pages[0].Images.Add(new StickerImage
        {
            FileName = "sample.png",
            MimeType = "image/png",
            ImageBase64 = Convert.ToBase64String([1, 2, 3, 4]),
            X = 10,
            Y = 20,
            Width = 120,
            Height = 80
        });
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);
        var image = Assert.Single(loaded.Pages[0].Images);

        Assert.Equal("sample.png", image.FileName);
        Assert.Equal("image/png", image.MimeType);
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), image.ImageBase64);
        Assert.Equal(10, image.X);
        Assert.Equal(120, image.Width);
    }

    [Fact]
    public void SaveLoad_RoundTripsTextBoxes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);

        var notebook = repository.Create("Texto");
        notebook.Pages[0].TextBoxes.Add(new PageTextBox
        {
            Text = "Apunte editable",
            X = 33,
            Y = 44,
            Width = 300,
            Height = 140,
            FontSize = 20
        });
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);
        var textBox = Assert.Single(loaded.Pages[0].TextBoxes);

        Assert.Equal("Apunte editable", textBox.Text);
        Assert.Equal(33, textBox.X);
        Assert.Equal(300, textBox.Width);
        Assert.Equal(20, textBox.FontSize);
    }
}
