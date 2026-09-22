using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using CuadernoDigital.App.ViewModels;
using Xunit;

namespace CuadernoDigital.Tests;

public sealed class NotebookEditorViewModelTests
{
    [Fact]
    public void Constructor_SelectsFirstPageAfterCommandsAreReady()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);
        var notebook = new Notebook
        {
            Title = "Fisica",
            Pages =
            [
                new NotebookPage { Title = "Pagina 1" }
            ]
        };

        var viewModel = new NotebookEditorViewModel(repository, notebook, Path.Combine(directory, "fisica.cdgz"));

        Assert.NotNull(viewModel.SelectedPage);
        Assert.False(viewModel.DeletePageCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_AddsPageWhenNotebookIsEmpty()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ProNotesTests", Guid.NewGuid().ToString("N"));
        var repository = new NotebookRepository(directory);
        var notebook = new Notebook { Title = "Vacio" };

        var viewModel = new NotebookEditorViewModel(repository, notebook, Path.Combine(directory, "vacio.cdgz"));

        Assert.Single(viewModel.Pages);
        Assert.NotNull(viewModel.SelectedPage);
    }
}
