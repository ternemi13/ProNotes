namespace CuadernoDigital.App.Models;

public sealed class AiChatMessage
{
    public string Role { get; set; } = "user";
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
