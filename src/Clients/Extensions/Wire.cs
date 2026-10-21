using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

using AIJsonUtilities = Microsoft.Extensions.AI.AIJsonUtilities;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// The JSON every client writes requests and reads responses with.
/// </summary>
internal static class Wire
{
    /// <summary>
    /// Web defaults, unset properties left off the wire, and a type discriminator read from any position;
    /// metadata from <see cref="OllamaJson"/> first and from MEAI's resolver for anything else.
    /// </summary>
    /// <remarks>
    /// Ollama documents absent fields, not null ones, and treats some nulls differently from absence, so a
    /// request carries only what the caller set. Dictionary values are not properties and keep their
    /// nulls — a System One choice criterion without a description must still arrive as null.
    /// <para>
    /// System.Text.Json reads a polymorphic "type" only as the first property by default; nothing in the
    /// Ollama contract promises that order, and a reordered field would fail the whole response.
    /// </para>
    /// <para>
    /// The members typed object — a System One state and instructions, a format, think, embedding options —
    /// resolve through MEAI's resolver, which adds reflection only where the application allows it: any
    /// value serializes under the JIT as before; under Native AOT it has to be a JsonElement, a JsonNode, a
    /// primitive or a dictionary of those.
    /// </para>
    /// </remarks>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(OllamaJson.Default, AIJsonUtilities.DefaultOptions.TypeInfoResolver)
    };

    /// <summary>The /api/chat request.</summary>
    public static readonly JsonTypeInfo<ChatRequest> ChatRequest = Info<ChatRequest>();

    /// <summary>A /api/chat answer or one of its streamed chunks.</summary>
    public static readonly JsonTypeInfo<ChatResponse> ChatResponse = Info<ChatResponse>();

    /// <summary>The /api/generate request.</summary>
    public static readonly JsonTypeInfo<GenerateRequest> GenerateRequest = Info<GenerateRequest>();

    /// <summary>One streamed /api/generate chunk.</summary>
    public static readonly JsonTypeInfo<GenerateResponse> GenerateResponse = Info<GenerateResponse>();

    /// <summary>The /api/embed request.</summary>
    public static readonly JsonTypeInfo<EmbeddingsRequest> EmbeddingsRequest = Info<EmbeddingsRequest>();

    /// <summary>The /api/embed answer.</summary>
    public static readonly JsonTypeInfo<EmbeddingsResponse> EmbeddingsResponse = Info<EmbeddingsResponse>();

    /// <summary>The /v1/systemone request.</summary>
    public static readonly JsonTypeInfo<SystemOneRequest> SystemOneRequest = Info<SystemOneRequest>();

    /// <summary>The /v1/systemone answer.</summary>
    public static readonly JsonTypeInfo<SystemOneResponse> SystemOneResponse = Info<SystemOneResponse>();

    /// <summary>The /api/show answer.</summary>
    public static readonly JsonTypeInfo<ModelDescription> ModelDescription = Info<ModelDescription>();

    /// <summary>
    /// The metadata of <typeparamref name="T"/> under <see cref="Json"/>.
    /// </summary>
    public static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Json.GetTypeInfo(typeof(T));
}
