using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// The JSON every client writes requests and reads responses with.
/// </summary>
internal static class Wire
{
    /// <summary>
    /// Web defaults, unset properties left off the wire, and a type discriminator read from any position.
    /// </summary>
    /// <remarks>
    /// Ollama documents absent fields, not null ones, and treats some nulls differently from absence, so a
    /// request carries only what the caller set. Dictionary values are not properties and keep their
    /// nulls — a System One choice criterion without a description must still arrive as null.
    /// <para>
    /// System.Text.Json reads a polymorphic "type" only as the first property by default; nothing in the
    /// Ollama contract promises that order, and a reordered field would fail the whole response.
    /// </para>
    /// </remarks>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        AllowOutOfOrderMetadataProperties = true
    };
}
