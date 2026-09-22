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

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "ok"
                          }
                        ]
                      }
                    }
                  ]
                }
                """)
            };
        }
    }
}
