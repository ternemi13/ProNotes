using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class GeminiClient
{
    private const string ModelName = "gemini-3.6-flash";
    private const string EndpointFormat = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private readonly HttpClient httpClient;

    public GeminiClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public Task<string> AskAsync(
        string apiKey,
        string prompt,
        IReadOnlyList<AiChatMessage>? history = null,
        IReadOnlyList<AiRequestAttachment>? attachments = null,
        byte[]? currentPagePng = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Escribe una pregunta para el asistente.");
        }

        var currentParts = new List<JsonObject>
        {
            new() { ["text"] = prompt.Trim() }
        };

        if (currentPagePng is { Length: > 0 })
        {
            currentParts.Add(new JsonObject { ["text"] = "Imagen de la pagina actual del cuaderno:" });
            currentParts.Add(CreateInlineDataPart("image/png", currentPagePng));
        }

        foreach (var attachment in attachments ?? [])
        {
            if (attachment.Data.Length == 0)
            {
                continue;
            }

            currentParts.Add(new JsonObject { ["text"] = $"Archivo adjunto: {attachment.Name}" });
            if (attachment.MimeType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                currentParts.Add(new JsonObject { ["text"] = System.Text.Encoding.UTF8.GetString(attachment.Data) });
            }
            else
            {
                currentParts.Add(CreateInlineDataPart(attachment.MimeType, attachment.Data));
            }
        }

        var request = CreateRequest(history, currentParts);
        return SendAsync(apiKey, request, cancellationToken);
    }

    private static JsonObject CreateRequest(IReadOnlyList<AiChatMessage>? history, IReadOnlyList<JsonObject> currentParts)
    {
        var contents = new JsonArray();
        foreach (var message in history?.TakeLast(12) ?? [])
        {
            if (string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            contents.Add(new JsonObject
            {
                ["role"] = message.Role == "assistant" ? "model" : "user",
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = message.Text }
                }
            });
        }

        var parts = new JsonArray();
        foreach (var part in currentParts)
        {
            parts.Add(part);
        }

        contents.Add(new JsonObject
        {
            ["role"] = "user",
            ["parts"] = parts
        });

        return new JsonObject
        {
            ["contents"] = contents,
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["text"] = "Eres el asistente integrado de ProNotes. Ayudas a estudiar, corregir apuntes, revisar ejercicios, resumir temas y redactar contenido listo para insertar en el cuaderno. Cuando el usuario pida poner, agregar, insertar, editar o corregir contenido en los apuntes, responde con una version final clara y util para pegar en la pagina, sin disculpas ni relleno."
                    }
                }
            },
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.4,
                ["maxOutputTokens"] = 1800
            }
        };
    }

    private async Task<string> SendAsync(string apiKey, JsonObject request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Configura primero tu API key de Gemini.");
        }

        var endpoint = string.Format(EndpointFormat, ModelName, Uri.EscapeDataString(apiKey.Trim()));
        using var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildErrorMessage(response.StatusCode, payload));
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string payload)
    {
        var apiMessage = TryExtractApiErrorMessage(payload);
        var prefix = statusCode switch
        {
            HttpStatusCode.TooManyRequests => "Gemini alcanzo el limite de cuota o velocidad del plan gratuito.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "La API key de Gemini no fue aceptada.",
            HttpStatusCode.BadRequest => "Gemini no pudo procesar la solicitud.",
            HttpStatusCode.NotFound => "El modelo de Gemini configurado no esta disponible para esta API key.",
            _ => $"Gemini respondio {(int)statusCode}."
        };

        return string.IsNullOrWhiteSpace(apiMessage)
            ? prefix
            : $"{prefix} {apiMessage}";
    }

    private static JsonObject CreateInlineDataPart(string mimeType, byte[] data)
    {
        return new JsonObject
        {
            ["inline_data"] = new JsonObject
            {
                ["mime_type"] = mimeType,
                ["data"] = Convert.ToBase64String(data)
            }
        };
    }

    private static string? TryExtractApiErrorMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString();
        }
        catch
        {
            return null;
        }
    }
}
