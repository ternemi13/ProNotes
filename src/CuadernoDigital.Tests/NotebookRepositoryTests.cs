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
        notebook.Cover.Subtitle = "Segundo semestre";
        notebook.Cover.BackgroundColor = "#111827";
        notebook.Cover.AccentColor = "#FACC15";
        notebook.Cover.TemplateName = "Band";
        notebook.Cover.BackgroundImageBase64 = Convert.ToBase64String([9, 8, 7]);
        notebook.Cover.BackgroundImageMimeType = "image/png";
        notebook.Cover.BackgroundImageOffsetX = 0.25;
        notebook.Cover.BackgroundImageOffsetY = -0.35;
        notebook.Cover.BackgroundImageScale = 1.4;
        notebook.AiChatHistory.Add(new AiChatMessage
        {
            Role = "user",
            Text = "Explicame esta pagina",
            CreatedAt = DateTimeOffset.Parse("2026-09-21T10:00:00Z")
        });
        notebook.AiChatHistory.Add(new AiChatMessage
        {
            Role = "assistant",
            Text = "Claro, revisemos tus apuntes.",
            CreatedAt = DateTimeOffset.Parse("2026-09-21T10:00:10Z")
        });
        notebook.Pages[0].InkBase64 = "sample";
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);

        Assert.Equal("Calculo II", loaded.Title);
        Assert.Equal("Calculo II", loaded.Cover.Title);
        Assert.Equal("Segundo semestre", loaded.Cover.Subtitle);
        Assert.Equal("#111827", loaded.Cover.BackgroundColor);
        Assert.Equal("#FACC15", loaded.Cover.AccentColor);
        Assert.Equal("Band", loaded.Cover.TemplateName);
        Assert.Equal(Convert.ToBase64String([9, 8, 7]), loaded.Cover.BackgroundImageBase64);
        Assert.Equal("image/png", loaded.Cover.BackgroundImageMimeType);
        Assert.Equal(0.25, loaded.Cover.BackgroundImageOffsetX);
        Assert.Equal(-0.35, loaded.Cover.BackgroundImageOffsetY);
        Assert.Equal(1.4, loaded.Cover.BackgroundImageScale);
        Assert.Equal(2, loaded.AiChatHistory.Count);
        Assert.Equal("user", loaded.AiChatHistory[0].Role);
        Assert.Equal("Explicame esta pagina", loaded.AiChatHistory[0].Text);
        Assert.Equal("assistant", loaded.AiChatHistory[1].Role);
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
            Height = 80,
            Rotation = 15,
            ZIndex = 4
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
        Assert.Equal(15, image.Rotation);
        Assert.Equal(4, image.ZIndex);
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
            FontSize = 20,
            IsBold = true,
            IsUnderline = true,
            Foreground = "#2563EB",
            TextAlignment = "Center",
            Rotation = 30,
            ZIndex = 5
        });
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);
        var textBox = Assert.Single(loaded.Pages[0].TextBoxes);

        Assert.Equal("Apunte editable", textBox.Text);
        Assert.Equal(33, textBox.X);
        Assert.Equal(300, textBox.Width);
        Assert.Equal(20, textBox.FontSize);
        Assert.True(textBox.IsBold);
        Assert.True(textBox.IsUnderline);
        Assert.Equal("#2563EB", textBox.Foreground);
        Assert.Equal("Center", textBox.TextAlignment);
        Assert.Equal(30, textBox.Rotation);
        Assert.Equal(5, textBox.ZIndex);
    }

    [Fact]
    public void SaveLoad_RoundTripsTables()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);

        var notebook = repository.Create("Tablas");
        var table = new PageTable
        {
            Rows = 2,
            Columns = 2,
            X = 42,
            Y = 70,
            Width = 320,
            Height = 160,
            Rotation = 45,
            ZIndex = 6,
            Cells = ["A", "B", "C", "D"]
        };
        notebook.Pages[0].Tables.Add(table);
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);
        var loadedTable = Assert.Single(loaded.Pages[0].Tables);

        Assert.Equal(2, loadedTable.Rows);
        Assert.Equal(2, loadedTable.Columns);
        Assert.Equal(42, loadedTable.X);
        Assert.Equal(320, loadedTable.Width);
        Assert.Equal(45, loadedTable.Rotation);
        Assert.Equal(6, loadedTable.ZIndex);
        Assert.Equal(["A", "B", "C", "D"], loadedTable.Cells);
    }

    [Fact]
    public void SaveLoad_RoundTripsCharts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);

        var notebook = repository.Create("Graficas");
        notebook.Pages[0].Charts.Add(new PageChart
        {
            Title = "Notas",
            Type = PageChartType.Line,
            X = 55,
            Y = 66,
            Width = 420,
            Height = 260,
            Rotation = 10,
            ZIndex = 7,
            DataPoints =
            [
                new ChartDataPoint { Label = "Parcial 1", Value = 4.2 },
                new ChartDataPoint { Label = "Parcial 2", Value = 4.7 }
            ]
        });
        repository.Save(notebook);

        var summary = Assert.Single(repository.GetSummaries());
        var loaded = repository.Load(summary.FilePath);
        var chart = Assert.Single(loaded.Pages[0].Charts);

        Assert.Equal("Notas", chart.Title);
        Assert.Equal(PageChartType.Line, chart.Type);
        Assert.Equal(55, chart.X);
        Assert.Equal(420, chart.Width);
        Assert.Equal(10, chart.Rotation);
        Assert.Equal(7, chart.ZIndex);
        Assert.Equal("Parcial 1", chart.DataPoints[0].Label);
        Assert.Equal(4.7, chart.DataPoints[1].Value);
    }

    [Fact]
    public void PageTable_ResizePreservesExistingCells()
    {
        var table = new PageTable
        {
            Rows = 2,
            Columns = 2,
            Cells = ["A", "B", "C", "D"]
        };

        table.Resize(3, 3);

        Assert.Equal(3, table.Rows);
        Assert.Equal(3, table.Columns);
        Assert.Equal("A", table.Cells[0]);
        Assert.Equal("B", table.Cells[1]);
        Assert.Equal("C", table.Cells[3]);
        Assert.Equal("D", table.Cells[4]);

        table.Resize(1, 2);

        Assert.Equal(1, table.Rows);
        Assert.Equal(2, table.Columns);
        Assert.Equal(["A", "B"], table.Cells);
    }

    [Fact]
    public void GeminiSettingsService_SaveLoadClear_ProtectsLocalKey()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var settings = new GeminiSettingsService(directory);

        settings.SaveApiKey("sample-secret-key");

        Assert.True(settings.HasApiKey);
        Assert.Equal("sample-secret-key", settings.LoadApiKey());
        Assert.Equal(-1, File.ReadAllBytes(Path.Combine(directory, "gemini.key")).AsSpan().IndexOf("sample-secret-key"u8));

        settings.ClearApiKey();

        Assert.False(settings.HasApiKey);
        Assert.Null(settings.LoadApiKey());
    }
}
