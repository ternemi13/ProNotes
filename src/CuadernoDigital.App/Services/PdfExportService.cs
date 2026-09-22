using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CuadernoDigital.App.Controls;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Views;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MediaColor = System.Windows.Media.Color;

namespace CuadernoDigital.App.Services;

public sealed class PdfExportService
{
    private const double PageWidth = 900;
    private const double PageHeight = 1200;
    private const double RenderScale = 2;

    static PdfExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task ExportNotebookAsync(Notebook notebook, string targetPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notebook);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        cancellationToken.ThrowIfCancellationRequested();

        var pages = new List<byte[]> { RenderCover(notebook.Cover) };
        pages.AddRange(notebook.Pages.Select(page =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return RenderPage(page);
        }));

        WritePdf(pages, targetPath);
        return Task.CompletedTask;
    }

    public Task ExportPageAsync(NotebookPage page, string targetPath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        cancellationToken.ThrowIfCancellationRequested();

        WritePdf([RenderPage(page)], targetPath);
        return Task.CompletedTask;
    }

    private static byte[] RenderCover(NotebookCover cover)
    {
        var backgroundColor = ParseColor(cover.BackgroundColor, MediaColor.FromRgb(37, 99, 235));
        var titleColor = IsLight(backgroundColor) && string.Equals(cover.TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? "#111827"
            : "White";
        var subtitleColor = IsLight(backgroundColor) && string.Equals(cover.TemplateName, "Minimal", StringComparison.OrdinalIgnoreCase)
            ? "#475569"
            : "#E2E8F0";

        var preview = new CoverPreviewControl
        {
            Width = PageWidth,
            Height = PageHeight,
            CoverTitle = cover.Title,
            Subtitle = cover.Subtitle,
            BackgroundColor = cover.BackgroundColor,
            AccentColor = cover.AccentColor,
            ImageBase64 = cover.BackgroundImageBase64,
            ImageOffsetX = cover.BackgroundImageOffsetX,
            ImageOffsetY = cover.BackgroundImageOffsetY,
            ImageScale = cover.BackgroundImageScale <= 0 ? 1 : cover.BackgroundImageScale,
            TemplateName = cover.TemplateName,
            TitleColor = titleColor,
            SubtitleColor = subtitleColor,
            CornerRadius = new CornerRadius(0),
            TitleFontSize = 54,
            SubtitleFontSize = 24
        };

        return RenderElementToPng(preview, PageWidth, PageHeight, RenderScale);
    }

    private static byte[] RenderPage(NotebookPage page)
    {
        var pageView = new PageCanvasView();
        return pageView.RenderPageToPng(page, RenderScale);
    }

    private static void WritePdf(IReadOnlyList<byte[]> pages, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath))!);

        Document.Create(document =>
        {
            foreach (var imageBytes in pages)
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.Content().Image(imageBytes).FitArea();
                });
            }
        }).GeneratePdf(targetPath);
    }

    private static byte[] RenderElementToPng(FrameworkElement element, double width, double height, double scale)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new System.Windows.Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();

        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(width * scale),
            (int)Math.Ceiling(height * scale),
            96 * scale,
            96 * scale,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static MediaColor ParseColor(string? color, MediaColor fallback)
    {
        try
        {
            return string.IsNullOrWhiteSpace(color)
                ? fallback
                : (MediaColor)ColorConverter.ConvertFromString(color);
        }
        catch
        {
            return fallback;
        }
    }

    private static bool IsLight(MediaColor color)
    {
        var luminance = 0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B;
        return luminance > 180;
    }
}
