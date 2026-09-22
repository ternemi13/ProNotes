using System.Net;
using CuadernoDigital.App.Models;
using CuadernoDigital.App.Services;
using Xunit;

namespace CuadernoDigital.Tests;

public sealed class GeminiClientTests
{
    [Fact]
    public async Task AskAsync_UsesCurrentGeminiModelAndSendsInlineData()
    {
        var handler = new CaptureHandler();
        var client = new GeminiClient(new HttpClient(handler));

        var answer = await client.AskAsync(
            "sample-key",
            "Mira la pagina y este PDF",
            [],
            [
                new AiRequestAttachment
                {
                    Name = "guia.pdf",
                    MimeType = "application/pdf",
                    Data = [1, 2, 3]
                }
            ],
            [4, 5, 6]);

        Assert.Equal("ok", answer);
        Assert.Contains("models/gemini-3.6-flash:generateContent", handler.RequestUri?.ToString());
        Assert.Contains("inline_data", handler.RequestBody);
        Assert.Contains("application/pdf", handler.RequestBody);
        Assert.Contains("image/png", handler.RequestBody);
    }

    [Fact]
    public async Task AskAsync_FallsBackToNextFlashModelWhenPrimaryIsUnavailable()
    {
        var handler = new CaptureHandler(
            Error(HttpStatusCode.NotFound, "models/gemini-3.6-flash is not found"),
            Ok("fallback ok"));
        var client = new GeminiClient(new HttpClient(handler));

        var answer = await client.AskAsync("sample-key", "Hola");

        Assert.Equal("fallback ok", answer);
        Assert.Equal(2, handler.RequestUris.Count);
        Assert.Contains("models/gemini-3.6-flash:generateContent", handler.RequestUris[0].ToString());
        Assert.Contains("models/gemini-3.5-flash:generateContent", handler.RequestUris[1].ToString());
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public CaptureHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses.Length == 0 ? [Ok("ok")] : responses);
        }

        public Uri? RequestUri { get; private set; }
        public List<Uri> RequestUris { get; } = [];
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            if (request.RequestUri is not null)
            {
                RequestUris.Add(request.RequestUri);
            }

            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responses.Count > 0 ? responses.Dequeue() : Ok("ok");
        }
    }

    private static HttpResponseMessage Ok(string text)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent($$"""
            {
              "candidates": [
                {
                  "content": {
                    "parts": [
                      {
                        "text": "{{text}}"
                      }
                    ]
                  }
                }
              ]
            }
            """)
        };
    }

    private static HttpResponseMessage Error(HttpStatusCode statusCode, string message)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent($$"""
            {
              "error": {
                "message": "{{message}}"
              }
            }
            """)
        };
    }
}
