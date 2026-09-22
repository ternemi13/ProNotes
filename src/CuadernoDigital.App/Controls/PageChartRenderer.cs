using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Controls;

public static class PageChartRenderer
{
    private static readonly Color[] Palette =
    [
        Color.FromRgb(37, 99, 235),
        Color.FromRgb(22, 163, 74),
        Color.FromRgb(234, 179, 8),
        Color.FromRgb(220, 38, 38),
        Color.FromRgb(124, 58, 237),
        Color.FromRgb(8, 145, 178),
        Color.FromRgb(244, 114, 182),
        Color.FromRgb(71, 85, 105)
    ];

    public static FrameworkElement Create(PageChart chart)
    {
        chart.NormalizeData();

        var surface = new Canvas
        {
            Width = 420,
            Height = 260,
            Background = Brushes.White
        };

        AddTitle(surface, chart.Title);

        switch (chart.Type)
        {
            case PageChartType.Line:
                DrawLineChart(surface, chart.DataPoints);
                break;
            case PageChartType.Pie:
                DrawPieChart(surface, chart.DataPoints);
                break;
            case PageChartType.Bar:
            default:
                DrawBarChart(surface, chart.DataPoints);
                break;
        }

        return new Viewbox
        {
            Stretch = Stretch.Fill,
            Child = surface
        };
    }

    private static void AddTitle(Canvas surface, string title)
    {
        var text = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Grafica" : title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
            Width = 390,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(text, 15);
        Canvas.SetTop(text, 10);
        surface.Children.Add(text);
    }

    private static void DrawBarChart(Canvas surface, IReadOnlyList<ChartDataPoint> points)
    {
        const double plotX = 46;
        const double plotY = 48;
        const double plotWidth = 330;
        const double plotHeight = 150;
        var max = Math.Max(1, points.Max(point => Math.Abs(point.Value)));

        DrawAxes(surface, plotX, plotY, plotWidth, plotHeight);

        var slot = plotWidth / points.Count;
        var barWidth = Math.Min(34, slot * 0.58);
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var height = Math.Abs(point.Value) / max * (plotHeight - 8);
            var x = plotX + slot * index + (slot - barWidth) / 2;
            var y = plotY + plotHeight - height;

            var bar = new Rectangle
            {
                Width = barWidth,
                Height = height,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(Palette[index % Palette.Length])
            };
            Canvas.SetLeft(bar, x);
            Canvas.SetTop(bar, y);
            surface.Children.Add(bar);

            AddSmallText(surface, FormatNumber(point.Value), x - 12, y - 20, 58, TextAlignment.Center);
            AddSmallText(surface, point.Label, plotX + slot * index, plotY + plotHeight + 8, slot, TextAlignment.Center);
        }
    }

    private static void DrawLineChart(Canvas surface, IReadOnlyList<ChartDataPoint> points)
    {
        const double plotX = 46;
        const double plotY = 48;
        const double plotWidth = 330;
        const double plotHeight = 150;
        DrawAxes(surface, plotX, plotY, plotWidth, plotHeight);

        var min = points.Min(point => point.Value);
        var max = points.Max(point => point.Value);
        var range = Math.Abs(max - min) < 0.0001 ? 1 : max - min;
        var slot = points.Count == 1 ? plotWidth : plotWidth / (points.Count - 1);
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Palette[0]),
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round
        };

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = plotX + slot * index;
            var y = plotY + plotHeight - ((point.Value - min) / range * (plotHeight - 10)) - 5;
            polyline.Points.Add(new Point(x, y));

            var marker = new Ellipse
            {
                Width = 9,
                Height = 9,
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Palette[0]),
                StrokeThickness = 3
            };
            Canvas.SetLeft(marker, x - 4.5);
            Canvas.SetTop(marker, y - 4.5);
            surface.Children.Add(marker);

            AddSmallText(surface, point.Label, x - 35, plotY + plotHeight + 8, 70, TextAlignment.Center);
        }

        surface.Children.Insert(surface.Children.Count - points.Count, polyline);
    }

    private static void DrawPieChart(Canvas surface, IReadOnlyList<ChartDataPoint> points)
    {
        var total = points.Sum(point => Math.Abs(point.Value));
        if (total <= 0)
        {
            total = 1;
        }

        const double centerX = 155;
        const double centerY = 132;
        const double radius = 72;
        var startAngle = -90d;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var sweep = Math.Abs(point.Value) / total * 360;
            var path = new Path
            {
                Fill = new SolidColorBrush(Palette[index % Palette.Length]),
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            path.Data = CreatePieSlice(centerX, centerY, radius, startAngle, sweep);
            surface.Children.Add(path);
            startAngle += sweep;
        }

        for (var index = 0; index < points.Count; index++)
        {
            var color = new Border
            {
                Width = 10,
                Height = 10,
                Background = new SolidColorBrush(Palette[index % Palette.Length])
            };
            Canvas.SetLeft(color, 255);
            Canvas.SetTop(color, 68 + index * 21);
            surface.Children.Add(color);

            AddSmallText(surface, $"{points[index].Label}: {FormatNumber(points[index].Value)}", 272, 62 + index * 21, 128, TextAlignment.Left);
        }
    }

    private static void DrawAxes(Canvas surface, double x, double y, double width, double height)
    {
        var axisBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139));
        surface.Children.Add(new Line { X1 = x, Y1 = y + height, X2 = x + width, Y2 = y + height, Stroke = axisBrush, StrokeThickness = 1 });
        surface.Children.Add(new Line { X1 = x, Y1 = y, X2 = x, Y2 = y + height, Stroke = axisBrush, StrokeThickness = 1 });

        for (var line = 1; line <= 3; line++)
        {
            var top = y + height / 4 * line;
            surface.Children.Add(new Line
            {
                X1 = x,
                Y1 = top,
                X2 = x + width,
                Y2 = top,
                Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                StrokeThickness = 1
            });
        }
    }

    private static Geometry CreatePieSlice(double centerX, double centerY, double radius, double startAngle, double sweepAngle)
    {
        var start = PointOnCircle(centerX, centerY, radius, startAngle);
        var end = PointOnCircle(centerX, centerY, radius, startAngle + sweepAngle);
        var largeArc = sweepAngle > 180;

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(centerX, centerY), true, true);
        context.LineTo(start, true, false);
        context.ArcTo(end, new Size(radius, radius), 0, largeArc, SweepDirection.Clockwise, true, false);
        context.LineTo(new Point(centerX, centerY), true, false);
        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(double centerX, double centerY, double radius, double angle)
    {
        var radians = angle * Math.PI / 180;
        return new Point(centerX + Math.Cos(radians) * radius, centerY + Math.Sin(radians) * radius);
    }

    private static void AddSmallText(Canvas surface, string text, double x, double y, double width, TextAlignment alignment)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
            Width = width,
            TextAlignment = alignment,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        surface.Children.Add(block);
    }

    private static string FormatNumber(double value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
