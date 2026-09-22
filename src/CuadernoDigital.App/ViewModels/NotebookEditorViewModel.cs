using System.Collections.ObjectModel;
using System.Windows.Input;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;

namespace CuadernoDigital.App.ViewModels;

public sealed class NotebookEditorViewModel : ObservableObject
{
    private readonly NotebookRepository repository;
    private NotebookPage? selectedPage;
    private bool hasUnsavedChanges;

    public NotebookEditorViewModel(NotebookRepository repository, Notebook notebook, string filePath)
    {
        this.repository = repository;
        Notebook = notebook;
        FilePath = filePath;
        if (notebook.Pages.Count == 0)
        {
            notebook.Pages.Add(new NotebookPage { Title = "Pagina 1" });
        }

        Pages = new ObservableCollection<NotebookPage>(notebook.Pages);

        AddPageCommand = new RelayCommand(_ => AddPage());
        DeletePageCommand = new RelayCommand(_ => DeleteSelectedPage(), _ => SelectedPage is not null && Pages.Count > 1);
        SaveCommand = new RelayCommand(_ => Save());
        RenameCommand = new RelayCommand(_ => RenameNotebook());

        SelectedPage = Pages.FirstOrDefault();
    }

    public Notebook Notebook { get; }
    public string FilePath { get; }
    public ObservableCollection<NotebookPage> Pages { get; }

    public IReadOnlyList<PageBackgroundType> BackgroundTypes { get; } =
    [
        PageBackgroundType.Plain,
        PageBackgroundType.Grid,
        PageBackgroundType.Lined,
        PageBackgroundType.Dotted
    ];

    public NotebookPage? SelectedPage
    {
        get => selectedPage;
        set
        {
            if (SetProperty(ref selectedPage, value))
            {
                (DeletePageCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasUnsavedChanges
    {
        get => hasUnsavedChanges;
        private set => SetProperty(ref hasUnsavedChanges, value);
    }

    public ICommand AddPageCommand { get; }
    public ICommand DeletePageCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RenameCommand { get; }

    public void MarkDirty()
    {
        HasUnsavedChanges = true;
    }

    public void MarkCoverDirty()
    {
        if (!string.IsNullOrWhiteSpace(Notebook.Cover.Title))
        {
            Notebook.Title = Notebook.Cover.Title.Trim();
            OnPropertyChanged(nameof(Notebook));
        }

        MarkDirty();
    }

    public void Save()
    {
        Notebook.Pages = Pages.ToList();
        repository.Save(Notebook, FilePath);
        HasUnsavedChanges = false;
    }

    private void AddPage()
    {
        var page = new NotebookPage
        {
            Title = $"Pagina {Pages.Count + 1}",
            BackgroundType = PageBackgroundType.Grid
        };
        Pages.Add(page);
        SelectedPage = page;
        MarkDirty();
    }

    private void DeleteSelectedPage()
    {
        if (SelectedPage is null || Pages.Count <= 1)
        {
            return;
        }

        var index = Pages.IndexOf(SelectedPage);
        Pages.Remove(SelectedPage);
        SelectedPage = Pages[Math.Clamp(index - 1, 0, Pages.Count - 1)];
        MarkDirty();
    }

    private void RenameNotebook()
    {
        var title = Views.PromptDialog.Ask("Renombrar", "Nombre del cuaderno:", Notebook.Title);
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        Notebook.Title = title.Trim();
        Notebook.Cover.Title = Notebook.Title;
        OnPropertyChanged(nameof(Notebook));
        MarkDirty();
    }
}
