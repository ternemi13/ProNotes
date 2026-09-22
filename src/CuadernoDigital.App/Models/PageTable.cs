namespace CuadernoDigital.App.Models;

public sealed class PageTable
{
    public const int MinRows = 1;
    public const int MaxRows = 20;
    public const int MinColumns = 1;
    public const int MaxColumns = 12;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; } = 140;
    public double Y { get; set; } = 160;
    public double Width { get; set; } = 360;
    public double Height { get; set; } = 180;
    public int Rows { get; set; } = 3;
    public int Columns { get; set; } = 3;
    public double FontSize { get; set; } = 15;
    public double Rotation { get; set; }
    public int ZIndex { get; set; }
    public List<string> Cells { get; set; } = [];

    public void EnsureCellCount()
    {
        Rows = Math.Clamp(Rows, MinRows, MaxRows);
        Columns = Math.Clamp(Columns, MinColumns, MaxColumns);
        var expected = Rows * Columns;
        while (Cells.Count < expected)
        {
            Cells.Add(string.Empty);
        }

        if (Cells.Count > expected)
        {
            Cells.RemoveRange(expected, Cells.Count - expected);
        }
    }

    public void Resize(int rows, int columns)
    {
        rows = Math.Clamp(rows, MinRows, MaxRows);
        columns = Math.Clamp(columns, MinColumns, MaxColumns);
        EnsureCellCount();

        var oldRows = Rows;
        var oldColumns = Columns;
        var oldCells = Cells.ToList();

        Rows = rows;
        Columns = columns;
        Cells.Clear();

        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                Cells.Add(row < oldRows && column < oldColumns
                    ? oldCells[row * oldColumns + column]
                    : string.Empty);
            }
        }
    }

    public void AddRow() => Resize(Rows + 1, Columns);

    public void RemoveRow() => Resize(Rows - 1, Columns);

    public void AddColumn() => Resize(Rows, Columns + 1);

    public void RemoveColumn() => Resize(Rows, Columns - 1);
}
