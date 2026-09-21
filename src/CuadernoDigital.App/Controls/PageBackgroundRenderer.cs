using System.Windows;
using System.Windows.Media;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Controls;

public sealed class PageBackgroundRenderer : FrameworkElement
{
    public static readonly DependencyProperty BackgroundTypeProperty =
        DependencyProperty.Register(
            nameof(BackgroundType),
            typeof(PageBackgroundType),
            typeof(PageBackgroundRenderer),
            new FrameworkPropertyMetadata(PageBackgroundType.Grid, FrameworkPropertyMetadataOptions.AffectsRender));

    public PageBackgroundType BackgroundType
    {
        get => (PageBackgroundType)GetValue(BackgroundTypeProperty);
        set => SetValue(BackgroundTypeProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        drawingContext.DrawRectangle(Brushes.White, null, new Rect(RenderSize));

        switch (BackgroundType)
        {
            case PageBackgroundType.Grid:
                DrawGrid(drawingContext);
                break;
            case PageBackgroundType.Lined:
                DrawLines(drawingContext);
                break;
            case PageBackgroundType.Dotted:
                DrawDots(drawingContext);
                break;
            case PageBackgroundType.Plain:
            default:
                break;
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 240)), 1);
        const double step = 28;
        for (var x = step; x < ActualWidth; x += step)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, ActualHeight));
        }

        for (var y = step; y < ActualHeight; y += step)
        {
            context.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
        }
    }

    private void DrawLines(DrawingContext context)
    {
        var pen = new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1);
        const double step = 34;
        for (var y = 72d; y < ActualHeight; y += step)
        {
            context.DrawLine(pen, new Point(72, y), new Point(ActualWidth - 72, y));
        }

        var marginPen = new Pen(new SolidColorBrush(Color.FromRgb(248, 113, 113)), 1);
        context.DrawLine(marginPen, new Point(86, 0), new Point(86, ActualHeight));
    }

    private void DrawDots(DrawingContext context)
    {
        var brush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
        const double step = 24;
        for (var x = step; x < ActualWidth; x += step)
        {
            for (var y = step; y < ActualHeight; y += step)
            {
                context.DrawEllipse(brush, null, new Point(x, y), 1.5, 1.5);
            }
        }
    }
}
