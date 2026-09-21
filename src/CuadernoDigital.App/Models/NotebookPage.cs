using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CuadernoDigital.App.Models;

public sealed class NotebookPage : INotifyPropertyChanged
{
    private string title = "Pagina";
    private PageBackgroundType backgroundType = PageBackgroundType.Grid;
    private string inkBase64 = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public PageBackgroundType BackgroundType
    {
        get => backgroundType;
        set => SetProperty(ref backgroundType, value);
    }

    public string InkBase64
    {
        get => inkBase64;
        set => SetProperty(ref inkBase64, value);
    }

    public List<StickerImage> Images { get; set; } = [];

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
