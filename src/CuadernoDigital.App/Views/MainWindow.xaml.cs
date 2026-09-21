using System.Windows;
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
}
