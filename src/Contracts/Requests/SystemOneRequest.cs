using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// Named questions a System One model scores against one state over /v1/systemone.
/// </summary>
/// <param name="Model">A local model trained for System One, such as "nimble".</param>
/// <param name="State">The context being judged: a string, or any object or array, sent as JSON.</param>
/// <param name="Questions">One to 64 questions, each scored on its own and answered under the same name.</param>
/// <remarks>
/// Deliberately not a <see cref="RequestBase"/>: the endpoint refuses streaming, output formats, sampling
/// options and thinking, so the shared request surface would send fields it rejects.
/// <para>
/// Requires Ollama 0.35.0 or later and a local GGUF model — cloud and MLX models answer 400. The body is
/// capped at 64 KiB, or 32 MiB once it carries images, and every rendered prompt must fit the loaded
/// context window with two positions to spare, because the state is never truncated.
/// </para>
/// </remarks>
public sealed record SystemOneRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("state")] object State,
    [property: JsonPropertyName("questions")] IReadOnlyDictionary<string, SystemOneQuestion> Questions)
{
    /// <summary>
    /// Base64-encoded PNG, JPEG or WebP images every question sees alongside the state, in this order.
    /// </summary>
    /// <remarks>
    /// Needs Ollama 0.35.1 and a vision-capable decision model such as Clef or Clef Flash; the images are
    /// scored jointly with the state, not question by question. Raw base64 only — Ollama rejects URLs and
    /// data URIs, so strip any "data:image/png;base64," prefix.
    /// </remarks>
    [JsonPropertyName("images")]
    public IReadOnlyList<string>? Images { get; init; }

    /// <summary>
    /// How long the model stays loaded after the request, e.g. "5m"; null leaves Ollama's default.
    /// </summary>
    [JsonPropertyName("keep_alive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeepAlive { get; init; }
}
