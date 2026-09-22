namespace CuadernoDigital.App.Models;

public sealed class Notebook
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Nuevo cuaderno";
    public NotebookCover Cover { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<NotebookPage> Pages { get; set; } = [];
    public List<AiChatMessage> AiChatHistory { get; set; } = [];
}
