using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using CuadernoDigital.App.Views;

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

    private void OpenEditor(Notebook notebook, string filePath)
    {
        var window = new NotebookEditorView(repository, notebook, filePath);
        window.Closed += (_, _) => LoadLibrary();
        window.Show();
    }
}
