using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Abstractions;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;
using Snail.Toolkit.HttpBuilder.Extensions;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// Vector embeddings over /api/embed, doubling as a MEAI embedding generator.
/// </summary>
public class EmbeddingsClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), IEmbeddingsClient
{
    private readonly OllamaOptions _options = options.Value;

    private readonly EmbeddingGeneratorMetadata _metadata = new(
        providerName: "ollama",
        providerUri: Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) ? uri : null,
        defaultModelId: options.Value.DefaultModel);

    private const string EmbedEndpoint = "api/embed";

    /// <exception cref="HttpBuilderException">Thrown when the API call returns a non-success status code.</exception>
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<EmbeddingsResponse> GenerateAsync(
        EmbeddingsRequest request,
        CancellationToken cancellationToken = default) =>
        await _options
            .WithinTimeoutAsync(
                token => Post(EmbedEndpoint).AsJson(request, Wire.Json).SendAsync<EmbeddingsResponse>(token),
                cancellationToken)
            .ConfigureAwait(false)
        ?? throw new InvalidOperationException("Failed to deserialize the embeddings response.");

    /// <summary>
    /// MEAI path: the model falls back to the configured default, usage comes from
    /// prompt_eval_count.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no model is configured anywhere.</exception>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = options?.ModelId ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "No model specified: set EmbeddingGenerationOptions.ModelId or OllamaOptions.DefaultModel.");

        var request = new EmbeddingsRequest(model, values)
        {
            Dimensions = options?.Dimensions,
            Truncate = options?.AdditionalProperties?.TryGetValue("truncate", out bool truncate) is true ? truncate : null,
            KeepAlive = options?.AdditionalProperties?.TryGetValue("keep_alive", out string? keepAlive) is true ? keepAlive : null
        };

        var response = await GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        var embeddings = new GeneratedEmbeddings<Embedding<float>>(
            response.Embeddings.Select(vector => new Embedding<float>(vector) { ModelId = response.Model }));

        if (response.PromptEvalCount is { } tokens)
        {
            embeddings.Usage = new UsageDetails { InputTokenCount = tokens, TotalTokenCount = tokens };
        }

        return embeddings;
    }

    /// <summary>
    /// Serves <see cref="EmbeddingGeneratorMetadata"/> for MEAI wrappers, or the client itself.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(EmbeddingGeneratorMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    /// <summary>
    /// Releases nothing: the transport belongs to <see cref="IHttpClientFactory"/>.
    /// </summary>
    public void Dispose() => GC.SuppressFinalize(this);
}
