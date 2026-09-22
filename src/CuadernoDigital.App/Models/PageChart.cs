namespace CuadernoDigital.App.Models;

public enum PageChartType
{
    Bar,
    Line,
    Pie
}

public sealed class PageChart
{
    public const int MinDataPoints = 1;
    public const int MaxDataPoints = 12;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Grafica";
    public PageChartType Type { get; set; } = PageChartType.Bar;
    public double X { get; set; } = 150;
    public double Y { get; set; } = 180;
    public double Width { get; set; } = 420;
    public double Height { get; set; } = 260;
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public List<ChartDataPoint> DataPoints { get; set; } =
    [
        new() { Label = "A", Value = 10 },
        new() { Label = "B", Value = 16 },
        new() { Label = "C", Value = 8 }
    ];

    public void NormalizeData()
    {
        DataPoints = DataPoints
            .Where(point => !string.IsNullOrWhiteSpace(point.Label))
            .Take(MaxDataPoints)
            .Select(point => new ChartDataPoint
            {
                Label = point.Label.Trim(),
                Value = point.Value
            })
            .ToList();

        if (DataPoints.Count == 0)
        {
            DataPoints.Add(new ChartDataPoint { Label = "Dato", Value = 1 });
        }
    }
}

public sealed class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}
