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
        OpenNotebookCommand = new RelayCommand(_ => OpenSelected(), _ => SelectedNotebook is not null);
        DeleteNotebookCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedNotebook is not null);
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

    private void OpenSelected()
    {
        if (SelectedNotebook is null)
        {
            return;
        }

        var notebook = repository.Load(SelectedNotebook.FilePath);
        OpenEditor(notebook, SelectedNotebook.FilePath);
        LoadLibrary();
    }

    private void DeleteSelected()
    {
        if (SelectedNotebook is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Eliminar \"{SelectedNotebook.Title}\"?",
            "Eliminar cuaderno",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        repository.Delete(SelectedNotebook.FilePath);
        LoadLibrary();
    }

    private void OpenEditor(Notebook notebook, string filePath)
    {
        var window = new NotebookEditorView(repository, notebook, filePath);
        window.Closed += (_, _) => LoadLibrary();
        window.Show();
    }
}
