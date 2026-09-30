using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Snail.Toolkit.AI.Ollama.Clients.Extensions;
using Snail.Toolkit.AI.Ollama.Configuration;
using Snail.Toolkit.AI.Ollama.Contracts.Mapping;
using Snail.Toolkit.HttpBuilder.Extensions;

using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatResponse = Snail.Toolkit.AI.Ollama.Contracts.Responses.ChatResponse;
using IChatClient = Snail.Toolkit.AI.Ollama.Abstractions.IChatClient;

namespace Snail.Toolkit.AI.Ollama.Clients;

/// <summary>
/// MEAI chat client over Ollama's native /api/chat: NDJSON streaming, tool calling,
/// thinking and images.
/// </summary>
public class ChatClient(HttpClient httpClient, IOptions<OllamaOptions> options)
    : TypedHttpClientBase(httpClient), IChatClient
{
    private readonly OllamaOptions _options = options.Value;

    private readonly ChatClientMetadata _metadata = new(
        providerName: "ollama",
        providerUri: Uri.TryCreate(options.Value.BaseUrl, UriKind.Absolute, out var uri) ? uri : null,
        defaultModelId: options.Value.DefaultModel);

    private const string ChatEndpoint = "api/chat";

    /// <exception cref="InvalidOperationException">Thrown when no model is configured anywhere.</exception>
    private string ResolveModel(ChatOptions? options) =>
        options?.ModelId ?? _options.DefaultModel
        ?? throw new InvalidOperationException(
            "No model specified: set ChatOptions.ModelId or OllamaOptions.DefaultModel.");

    /// <inheritdoc />
    /// <exception cref="TimeoutException">Thrown when OllamaOptions.Timeout expires first.</exception>
    public async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = messages.ToInternalRequest(options, ResolveModel(options));
        request.Stream = false;

        var response = await _options
                           .WithinTimeoutAsync(
                               token => Post(ChatEndpoint).AsJson(request, Wire.Json).SendAsync<ChatResponse>(token),
                               cancellationToken)
                           .ConfigureAwait(false)
                       ?? throw new InvalidOperationException("Failed to deserialize the chat response.");

        return response.ToAiResponse();
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown when Ollama reports a failure inside the stream.</exception>
    /// <exception cref="IOException">Thrown when the stream ends before its final chunk.</exception>
    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = messages.ToInternalRequest(options, ResolveModel(options));

        var chunks = Post(ChatEndpoint)
            .AsJson(request, Wire.Json)
            .SendAsNdjsonAsync<ChatResponse>(cancellationToken)
            .UntilDoneAsync(cancellationToken);

        await foreach (var chunk in chunks.ConfigureAwait(false))
        {
            yield return chunk.ToAiUpdate();
        }
    }

    /// <summary>
    /// Serves <see cref="ChatClientMetadata"/> for MEAI telemetry wrappers, or the client itself.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is not null ? null
        : serviceType == typeof(ChatClientMetadata) ? _metadata
        : serviceType.IsInstanceOfType(this) ? this
        : null;

    /// <summary>
    /// Releases nothing: the transport belongs to <see cref="IHttpClientFactory"/>, and MEAI
    /// pipeline wrappers cascade Dispose — disposing the shared HttpClient would break other consumers.
    /// </summary>
    public void Dispose() => GC.SuppressFinalize(this);
}
