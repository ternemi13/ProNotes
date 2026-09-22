using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using CuadernoDigital.App.Controls;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Views;

public partial class ChartDataDialog : Window
{
    public ChartDataDialog(PageChart? chart = null)
    {
        InitializeComponent();

        Chart = chart is null ? CreateDefaultChart() : CloneChart(chart);
        TitleBox.Text = Chart.Title;
        SelectChartType(Chart.Type);
        DataBox.Text = string.Join(Environment.NewLine, Chart.DataPoints.Select(point => $"{point.Label}, {point.Value.ToString("0.##", CultureInfo.CurrentCulture)}"));
        RenderPreview();
    }

    public PageChart Chart { get; private set; } = new();

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBuildChart(out var chart, out var error))
        {
            MessageBox.Show(this, error, "Datos invalidos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Chart = chart;
        DialogResult = true;
    }

    private void Input_Changed(object sender, RoutedEventArgs e)
    {
        RenderPreview();
    }

    private void RenderPreview()
    {
        if (PreviewHost is null)
        {
            return;
        }

        PreviewHost.Children.Clear();
        if (!TryBuildChart(out var chart, out _))
        {
            return;
        }

        PreviewHost.Children.Add(PageChartRenderer.Create(chart));
    }

    private bool TryBuildChart(out PageChart chart, out string error)
    {
        chart = CloneChart(Chart);
        chart.Title = string.IsNullOrWhiteSpace(TitleBox?.Text) ? "Grafica" : TitleBox.Text.Trim();
        chart.Type = GetSelectedChartType();
        chart.DataPoints = [];

        var lines = (DataBox?.Text ?? string.Empty)
            .Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separator = line.Contains(',') ? ',' : line.Contains(';') ? ';' : ':';
            var parts = line.Split(separator, 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                error = "Cada linea debe tener etiqueta y valor, por ejemplo: Enero, 12";
                return false;
            }

            if (!TryParseNumber(parts[1], out var value))
            {
                error = $"No pude leer el valor de \"{parts[0]}\".";
                return false;
            }

            chart.DataPoints.Add(new ChartDataPoint
            {
                Label = parts[0],
                Value = value
            });
        }

        if (chart.DataPoints.Count == 0)
        {
            error = "Agrega al menos un dato.";
            return false;
        }

        if (chart.DataPoints.Count > PageChart.MaxDataPoints)
        {
            error = $"Usa maximo {PageChart.MaxDataPoints} datos para que la grafica siga siendo legible.";
            return false;
        }

        chart.NormalizeData();
        error = string.Empty;
        return true;
    }

    private PageChartType GetSelectedChartType()
    {
        return TypeBox?.SelectedItem is ComboBoxItem item
            && item.Tag is string tag
            && Enum.TryParse<PageChartType>(tag, out var type)
            ? type
            : PageChartType.Bar;
    }

    private void SelectChartType(PageChartType type)
    {
        foreach (var item in TypeBox.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag && Enum.TryParse<PageChartType>(tag, out var itemType) && itemType == type)
            {
                TypeBox.SelectedItem = item;
                return;
            }
        }
    }

    private static bool TryParseNumber(string text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static PageChart CreateDefaultChart() => new();

    private static PageChart CloneChart(PageChart source)
    {
        return new PageChart
        {
            Id = source.Id,
            Title = source.Title,
            Type = source.Type,
            X = source.X,
            Y = source.Y,
            Width = source.Width,
            Height = source.Height,
            Rotation = source.Rotation,
            ZIndex = source.ZIndex,
            DataPoints = source.DataPoints
                .Select(point => new ChartDataPoint { Label = point.Label, Value = point.Value })
                .ToList()
        };
    }
}
