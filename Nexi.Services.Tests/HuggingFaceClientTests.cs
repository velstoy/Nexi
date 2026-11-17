using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using Nexi.Services.Models;

namespace Nexi.Services.Tests;

public class HuggingFaceClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SearchModels_parses_catalog_entries_with_gguf_download()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.StartsWith("https://huggingface.co/api/models?search="))
            {
                return Json("""[{"id":"bartowski/Llama-3.2-3B-Instruct-GGUF","downloads":1000}]""");
            }

            if (url.Contains("/tree/main"))
            {
                return Json("""
                    [
                      {"type":"file","path":"Llama-3.2-3B-Instruct-Q8_0.gguf","size":3000000000},
                      {"type":"file","path":"Llama-3.2-3B-Instruct-Q4_K_M.gguf","size":2000000000},
                      {"type":"file","path":"README.md","size":5000}
                    ]
                    """);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var client = new HuggingFaceClient(new HttpClient(handler), NullLogger<HuggingFaceClient>.Instance);
        var results = await client.SearchModelsAsync("llama 3.2");

        var entry = Assert.Single(results);
        Assert.Equal("bartowski/Llama-3.2-3B-Instruct-GGUF", entry.Id);
        Assert.Equal("Q4_K_M", entry.Quantization);
        Assert.Equal(
            "https://huggingface.co/bartowski/Llama-3.2-3B-Instruct-GGUF/resolve/main/Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            entry.DownloadUrl);
        Assert.Equal("1.86 GB", entry.SizeText);
    }

    [Fact]
    public async Task SearchModels_skips_repos_without_gguf_files()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.StartsWith("https://huggingface.co/api/models?search="))
            {
                return Json("""[{"id":"some/text-model"},{"id":"some/onnx-model"}]""");
            }

            return Json("""[{"type":"file","path":"model.onnx","size":100}]""");
        });

        var client = new HuggingFaceClient(new HttpClient(handler), NullLogger<HuggingFaceClient>.Instance);
        var results = await client.SearchModelsAsync("anything");

        Assert.Empty(results);
    }

    [Fact]
    public async Task DownloadFileAsync_reports_progress_and_writes_file()
    {
        var payload = new byte[1000];
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new MemoryStream(payload))
            {
                Headers = { ContentLength = 1000 }
            }
        });

        var client = new HuggingFaceClient(new HttpClient(handler), NullLogger<HuggingFaceClient>.Instance);
        var dest = Path.Combine(Path.GetTempPath(), "nexi-hf-test-" + Guid.NewGuid().ToString("N"), "model.gguf");

        var progress = new List<ModelDownloadProgress>();
        await foreach (var p in client.DownloadFileAsync("https://example.com/model.gguf", dest, "m1"))
        {
            progress.Add(p);
        }

        Assert.True(File.Exists(dest));
        Assert.Equal(1000, new FileInfo(dest).Length);
        Assert.Equal(100, progress[^1].Percent);
        Assert.All(progress, p => Assert.InRange(p.Percent, 0, 100));
        Assert.DoesNotContain(dest + ".part", Directory.GetFiles(Path.GetDirectoryName(dest)!));
    }
}
