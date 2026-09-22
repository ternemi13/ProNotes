using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using CuadernoDigital.App.Views;
using Microsoft.Win32;

namespace CuadernoDigital.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly NotebookRepository repository;
    private NotebookSummary? selectedNotebook;

    public MainWindowViewModel(NotebookRepository repository)
    {
        this.repository = repository;
        NewNotebookCommand = new RelayCommand(_ => CreateNotebook());
        OpenNotebookCommand = new RelayCommand(parameter => OpenNotebook(parameter as NotebookSummary ?? SelectedNotebook), parameter => parameter is NotebookSummary || SelectedNotebook is not null);
        DeleteNotebookCommand = new RelayCommand(parameter => DeleteNotebook(parameter as NotebookSummary ?? SelectedNotebook), parameter => parameter is NotebookSummary || SelectedNotebook is not null);
        RefreshCommand = new RelayCommand(_ => LoadLibrary());
        LoadLibrary();
    }

    public ObservableCollection<NotebookSummary> Notebooks { get; } = [];

    public NotebookSummary? SelectedNotebook
    {
        get => selectedNotebook;
        set
        {
            if (SetProperty(ref selectedNotebook, value))
            {
                ((RelayCommand)OpenNotebookCommand).RaiseCanExecuteChanged();
                ((RelayCommand)DeleteNotebookCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand NewNotebookCommand { get; }
    public ICommand OpenNotebookCommand { get; }
    public ICommand DeleteNotebookCommand { get; }
    public ICommand RefreshCommand { get; }

    public void LoadLibrary()
    {
        Notebooks.Clear();
        foreach (var notebook in repository.GetSummaries())
        {
            Notebooks.Add(notebook);
        }
    }

    private void CreateNotebook()
    {
        var title = PromptDialog.Ask("Nuevo cuaderno", "Nombre del cuaderno:", "Mi cuaderno");
        if (title is null)
        {
            return;
        }

        var notebook = repository.Create(title);
        var path = repository.GetNotebookPath(notebook);
        OpenEditor(notebook, path);
        LoadLibrary();
    }

    public void OpenNotebook(NotebookSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var notebook = repository.Load(summary.FilePath);
        OpenEditor(notebook, summary.FilePath);
        LoadLibrary();
    }

    public void EditCover(NotebookSummary? summary, Window? owner = null)
    {
        if (summary is null)
        {
            return;
        }

        var notebook = repository.Load(summary.FilePath);
        var dialog = new CoverEditorWindow(notebook.Cover)
        {
            Owner = owner
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        CoverEditorWindow.CopyCover(dialog.EditedCover, notebook.Cover);
        if (!string.IsNullOrWhiteSpace(notebook.Cover.Title))
        {
            notebook.Title = notebook.Cover.Title.Trim();
        }

        repository.Save(notebook, summary.FilePath);
        LoadLibrary();
    }

    public void DeleteNotebook(NotebookSummary? summary)
    {
        if (summary is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Eliminar \"{summary.Title}\"?",
            "Eliminar cuaderno",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        repository.Delete(summary.FilePath);
        LoadLibrary();
    }

    public async Task ExportNotebookPdfAsync(NotebookSummary? summary, Window? owner = null)
    {
        if (summary is null)
        {
            return;
        }

        var notebook = repository.Load(summary.FilePath);
        var dialog = new SaveFileDialog
        {
            Title = "Exportar cuaderno a PDF",
            Filter = "PDF|*.pdf",
            FileName = $"{MakeSafeFileName(notebook.Title)}.pdf"
        };

        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await new PdfExportService().ExportNotebookAsync(notebook, dialog.FileName);
            MessageBox.Show(owner, $"PDF exportado:\n{dialog.FileName}", "Exportacion completada", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, $"No se pudo exportar el PDF.\n\n{ex.Message}", "Error al exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void OpenEditor(Notebook notebook, string filePath)
    {
        var window = new NotebookEditorView(repository, notebook, filePath);
        window.Closed += (_, _) => LoadLibrary();
        window.Show();
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "ProNotes" : safe;
    }
}
