using System.IO;
using System.Windows.Ink;

namespace CuadernoDigital.App.Services;

public static class PageSerializer
{
    public static string SerializeStrokes(StrokeCollection strokes)
    {
        if (strokes.Count == 0)
        {
            return string.Empty;
        }

        using var stream = new MemoryStream();
        strokes.Save(stream);
        return Convert.ToBase64String(stream.ToArray());
    }

    public static StrokeCollection DeserializeStrokes(string? inkBase64)
    {
        if (string.IsNullOrWhiteSpace(inkBase64))
        {
            return new StrokeCollection();
        }

        try
        {
            var bytes = Convert.FromBase64String(inkBase64);
            using var stream = new MemoryStream(bytes);
            return new StrokeCollection(stream);
        }
        catch
        {
            return new StrokeCollection();
        }
    }
}
