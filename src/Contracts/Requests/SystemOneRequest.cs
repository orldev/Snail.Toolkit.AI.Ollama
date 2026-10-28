using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Schema;

namespace Snail.Toolkit.AI.Ollama.Contracts.Requests;

/// <summary>
/// Named questions a System One model scores against one state over /v1/systemone.
/// </summary>
/// <param name="Model">A model trained for System One: a local one such as "nimble", or "clef:cloud" for Ollama's cloud.</param>
/// <param name="State">The context being judged: a string, or any object or array, sent as JSON.</param>
/// <param name="Questions">
/// One to 64 questions, each scored on its own and answered under the same name; cloud models take names of up to
/// 100 ASCII letters, digits, "_", "." or "-".
/// </param>
/// <remarks>
/// Deliberately not a <see cref="RequestBase"/>: the endpoint refuses streaming, output formats, sampling
/// options and thinking, so the shared request surface would send fields it rejects.
/// <para>
/// Requires Ollama 0.35.0 or later, and 0.40.5 or later for a cloud model — through a local server signed in to
/// ollama.com, or with BaseUrl https://ollama.com and an ApiKey; cloud models need a paid plan or usage credits.
/// Local models run on GGUF or, on Apple silicon since 0.40.0, on MLX.
/// </para>
/// <para>
/// Input is never truncated. Locally the text and schema, images excluded, must fit 64 KiB and the whole request
/// 32 MiB, and every rendered prompt must fit the loaded context window with two positions to spare. A cloud request
/// must fit 13 MiB and the model's 65,536-token window.
/// </para>
/// </remarks>
public sealed record SystemOneRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("state")] object State,
    [property: JsonPropertyName("questions")] IReadOnlyDictionary<string, SystemOneQuestion> Questions)
{
    /// <summary>
    /// Base64-encoded PNG, JPEG or WebP images every question sees alongside the state, in this order; a model with
    /// audio input, such as d1-omni, also takes WAV clips here.
    /// </summary>
    /// <remarks>
    /// Needs Ollama 0.35.1 and a decision model with vision, such as Clef; the images are scored jointly with the
    /// state, not question by question. Raw base64 only — Ollama rejects URLs and data URIs, so strip any
    /// "data:image/png;base64," prefix. A cloud model takes up to 4 images of at most 4 MiB and 16 megapixels
    /// each, 8 MiB in all.
    /// </remarks>
    [JsonPropertyName("images")]
    public IReadOnlyList<string>? Images { get; init; }

    /// <summary>
    /// How long the model stays loaded after the request, e.g. "5m"; null leaves Ollama's default.
    /// </summary>
    [JsonPropertyName("keep_alive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeepAlive { get; init; }
}
