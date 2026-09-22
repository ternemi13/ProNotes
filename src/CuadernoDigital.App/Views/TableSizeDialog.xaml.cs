using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CuadernoDigital.App.Views;

public partial class TableSizeDialog : Window
{
    public TableSizeDialog()
    {
        InitializeComponent();

        for (var value = 1; value <= 12; value++)
        {
            RowsBox.Items.Add(value);
            ColumnsBox.Items.Add(value);
        }

        RowsBox.SelectionChanged += (_, _) => RenderPreview();
        ColumnsBox.SelectionChanged += (_, _) => RenderPreview();
        RowsBox.SelectedItem = 3;
        ColumnsBox.SelectedItem = 3;
        RenderPreview();
    }

    public int Rows => RowsBox.SelectedItem is int value ? value : 3;

    public int Columns => ColumnsBox.SelectedItem is int value ? value : 3;

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void RenderPreview()
    {
        if (PreviewGrid is null)
        {
            return;
        }

        PreviewGrid.Children.Clear();
        PreviewGrid.RowDefinitions.Clear();
        PreviewGrid.ColumnDefinitions.Clear();

        for (var row = 0; row < Rows; row++)
        {
            PreviewGrid.RowDefinitions.Add(new RowDefinition());
        }

        for (var column = 0; column < Columns; column++)
        {
            PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var cell = new Border
                {
                    Background = row == 0 && column == 0
                        ? new SolidColorBrush(Color.FromRgb(238, 242, 255))
                        : new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    BorderThickness = new Thickness(0.5)
                };
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                PreviewGrid.Children.Add(cell);
            }
        }
    }
}
