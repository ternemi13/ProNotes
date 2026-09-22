namespace CuadernoDigital.App.Models;

public sealed class PageTable
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; } = 140;
    public double Y { get; set; } = 160;
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 180;
    public int Rows { get; set; } = 3;
    public int Columns { get; set; } = 3;
    public double FontSize { get; set; } = 15;
    public List<string> Cells { get; set; } = [];

    public void EnsureCellCount()
    {
        var expected = Math.Max(1, Rows) * Math.Max(1, Columns);
        while (Cells.Count < expected)
        {
            Cells.Add(string.Empty);
        }

        if (Cells.Count > expected)
        {
            Cells.RemoveRange(expected, Cells.Count - expected);
        }
    }
}
