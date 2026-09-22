namespace CuadernoDigital.App.Models;

public sealed class AiRequestAttachment
{
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = "application/octet-stream";
    public byte[] Data { get; set; } = [];
}
