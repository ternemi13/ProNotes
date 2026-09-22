using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CuadernoDigital.App.Services;
using CuadernoDigital.App.ViewModels;

namespace CuadernoDigital.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel(new NotebookRepository());
    }

    private void NotebookList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.OpenNotebookCommand.CanExecute(null))
        {
            viewModel.OpenNotebookCommand.Execute(null);
        }
    }

    private void NotebookMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.DataContext = button.Tag;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OpenNotebookMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.OpenNotebook(GetNotebookSummary(sender));
        }
    }

    private void EditCoverMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.EditCover(GetNotebookSummary(sender), this);
        }
    }

    private void DeleteNotebookMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DeleteNotebook(GetNotebookSummary(sender));
        }
    }

    private static NotebookSummary? GetNotebookSummary(object sender)
    {
        return sender is FrameworkElement element
            ? element.DataContext as NotebookSummary
            : null;
    }
}
