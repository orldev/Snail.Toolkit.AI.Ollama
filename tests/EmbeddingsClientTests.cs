using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients;
using Snail.Toolkit.AI.Ollama.Configuration;

namespace Snail.Toolkit.AI.Ollama.Tests;

/// <summary>
/// Tests for <see cref="EmbeddingsClient"/> acting as a MEAI embedding generator.
/// </summary>
public class EmbeddingsClientTests
{
    private sealed class StubHandler(string responseBody) : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private static EmbeddingsClient CreateClient(StubHandler handler, string? defaultModel = "embed-model") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") },
            Options.Create(new OllamaOptions { DefaultModel = defaultModel }));

    [Fact]
    public async Task GenerateAsync_ValidResponse_MapsVectorsModelAndUsage()
    {
        var handler = new StubHandler(
            """{"model":"embed-model","embeddings":[[0.1,0.2],[0.3,0.4]],"prompt_eval_count":7}""");
        using var client = CreateClient(handler);

        var embeddings = await ((IEmbeddingGenerator<string, Embedding<float>>)client)
            .GenerateAsync(["one", "two"]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal([0.1f, 0.2f], embeddings[0].Vector.ToArray());
        Assert.Equal("embed-model", embeddings[0].ModelId);
        Assert.Equal(7, embeddings.Usage!.InputTokenCount);
        Assert.Contains("\"embed-model\"", handler.RequestBody);
    }

    [Fact]
    public async Task GenerateAsync_NoModelConfigured_ThrowsInvalidOperationException()
    {
        using var client = CreateClient(new StubHandler("{}"), defaultModel: null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ((IEmbeddingGenerator<string, Embedding<float>>)client).GenerateAsync(["one"]));

        Assert.Contains("model", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetService_MetadataRequested_ReturnsGeneratorMetadata()
    {
        using var client = CreateClient(new StubHandler("{}"));

        var metadata = Assert.IsType<EmbeddingGeneratorMetadata>(
            client.GetService(typeof(EmbeddingGeneratorMetadata)));
        Assert.Equal("ollama", metadata.ProviderName);
        Assert.Equal("embed-model", metadata.DefaultModelId);
    }
}
