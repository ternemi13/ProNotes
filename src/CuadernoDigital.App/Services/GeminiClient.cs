using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CuadernoDigital.App.Models;

namespace CuadernoDigital.App.Services;

public sealed class GeminiClient
{
    private const string EndpointFormat = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={0}";
    private readonly HttpClient httpClient;

    public GeminiClient(HttpClient? httpClient = null)
    {
        this.httpClient = httpClient ?? new HttpClient();
    }

    public Task<string> AskAsync(string apiKey, string prompt, IReadOnlyList<AiChatMessage>? history = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new InvalidOperationException("Escribe una pregunta para el asistente.");
        }

        var request = CreateRequest(history, [new JsonObject { ["text"] = prompt.Trim() }]);
        return SendAsync(apiKey, request, cancellationToken);
    }

    public Task<string> ReviewPageAsync(
        string apiKey,
        byte[] pagePng,
        IReadOnlyList<AiChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        if (pagePng.Length == 0)
        {
            throw new InvalidOperationException("No se pudo capturar la pagina actual.");
        }

        var prompt = "Lee esta pagina de apuntes. Resume lo importante, corrige errores si los ves y sugiere mejoras concretas para estudiar.";
        var request = CreateRequest(
            history,
            [
                new JsonObject { ["text"] = prompt },
                new JsonObject
                {
                    ["inline_data"] = new JsonObject
                    {
                        ["mime_type"] = "image/png",
                        ["data"] = Convert.ToBase64String(pagePng)
                    }
                }
            ]);

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
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0.4,
                ["maxOutputTokens"] = 1200
            }
        };
    }

    private async Task<string> SendAsync(string apiKey, JsonObject request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Configura primero tu API key de Gemini.");
        }

        var endpoint = string.Format(EndpointFormat, Uri.EscapeDataString(apiKey.Trim()));
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
        var prefix = statusCode switch
        {
            HttpStatusCode.TooManyRequests => "Gemini alcanzo el limite de cuota o velocidad del plan gratuito.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "La API key de Gemini no fue aceptada.",
            HttpStatusCode.BadRequest => "Gemini no pudo procesar la solicitud.",
            _ => $"Gemini respondio {(int)statusCode}."
        };

        return string.IsNullOrWhiteSpace(payload)
            ? prefix
            : $"{prefix}\n{payload}";
    }
}
