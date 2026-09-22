using System.Windows;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Views;

public partial class CoverEditorWindow : Window
{
    public CoverEditorWindow(NotebookCover cover)
    {
        InitializeComponent();
        EditedCover = CloneCover(cover);
        Designer.Cover = EditedCover;
    }

    public NotebookCover EditedCover { get; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    public static void CopyCover(NotebookCover source, NotebookCover target)
    {
        target.BackgroundColor = source.BackgroundColor;
        target.AccentColor = source.AccentColor;
        target.BackgroundImageBase64 = source.BackgroundImageBase64;
        target.BackgroundImageMimeType = source.BackgroundImageMimeType;
        target.BackgroundImageOffsetX = source.BackgroundImageOffsetX;
        target.BackgroundImageOffsetY = source.BackgroundImageOffsetY;
        target.BackgroundImageScale = source.BackgroundImageScale;
        target.TemplateName = source.TemplateName;
        target.Title = source.Title;
        target.Subtitle = source.Subtitle;
    }

    private static NotebookCover CloneCover(NotebookCover source)
    {
        var clone = new NotebookCover();
        CopyCover(source, clone);
        return clone;
    }
}
