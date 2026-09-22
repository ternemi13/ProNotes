using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace CuadernoDigital.App.Controls;

public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownViewer),
            new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        BorderThickness = new Thickness(0);
        Background = Brushes.Transparent;
        Padding = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        ((MarkdownViewer)dependencyObject).Document = BuildDocument(args.NewValue?.ToString() ?? string.Empty);
    }

    private static FlowDocument BuildDocument(string markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI Variable"),
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        };

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var inCodeBlock = false;
        var codeLines = new List<string>();

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                if (inCodeBlock)
                {
                    document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
                    codeLines.Clear();
                }

                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            document.Blocks.Add(CreateParagraph(line));
        }

        if (codeLines.Count > 0)
        {
            document.Blocks.Add(CreateCodeBlock(string.Join(Environment.NewLine, codeLines)));
        }

        return document;
    }

    private static Paragraph CreateParagraph(string line)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 6),
            LineHeight = 18
        };

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal))
        {
            paragraph.Inlines.Add(new Run("• ") { FontWeight = FontWeights.SemiBold });
            AddInlineMarkdown(paragraph, trimmed[2..]);
            return paragraph;
        }

        var numbered = Regex.Match(trimmed, @"^\d+\.\s+(.*)$");
        if (numbered.Success)
        {
            paragraph.Inlines.Add(new Run(trimmed[..(trimmed.IndexOf('.') + 2)]) { FontWeight = FontWeights.SemiBold });
            AddInlineMarkdown(paragraph, numbered.Groups[1].Value);
            return paragraph;
        }

        AddInlineMarkdown(paragraph, line);
        return paragraph;
    }

    private static void AddInlineMarkdown(Paragraph paragraph, string text)
    {
        var parts = Regex.Split(text, @"(\*\*.*?\*\*)");
        foreach (var part in parts)
        {
            if (part.StartsWith("**", StringComparison.Ordinal) && part.EndsWith("**", StringComparison.Ordinal) && part.Length > 4)
            {
                paragraph.Inlines.Add(new Run(part[2..^2]) { FontWeight = FontWeights.SemiBold });
            }
            else if (!string.IsNullOrEmpty(part))
            {
                paragraph.Inlines.Add(new Run(part));
            }
        }
    }

    private static Block CreateCodeBlock(string code)
    {
        return new Section(new Paragraph(new Run(code))
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Margin = new Thickness(0),
            Padding = new Thickness(10),
            Background = new SolidColorBrush(Color.FromRgb(241, 245, 249))
        })
        {
            Margin = new Thickness(0, 4, 0, 8),
            BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
            BorderThickness = new Thickness(1)
        };
    }
}
