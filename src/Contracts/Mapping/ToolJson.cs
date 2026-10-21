using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>
/// The JSON tool-call arguments and results are written and read with, in both directions of the mapping.
/// </summary>
internal static class ToolJson
{
    /// <summary>
    /// Names matched in any case, metadata from <see cref="MappingJson"/> first and from MEAI's resolver for
    /// anything else.
    /// </summary>
    /// <remarks>
    /// MEAI's resolver adds reflection only where the application allows it, so a tool result of any
    /// type serializes under the JIT as before; under Native AOT it has to be a type MEAI knows.
    /// </remarks>
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = JsonTypeInfoResolver.Combine(MappingJson.Default, AIJsonUtilities.DefaultOptions.TypeInfoResolver)
    };

    /// <summary>The arguments of a call the model made earlier in the conversation.</summary>
    public static readonly JsonTypeInfo<IDictionary<string, object?>?> WrittenArguments =
        (JsonTypeInfo<IDictionary<string, object?>?>)Options.GetTypeInfo(typeof(IDictionary<string, object?>));

    /// <summary>A tool result of any type.</summary>
    public static readonly JsonTypeInfo<object> WrittenResult =
        (JsonTypeInfo<object>)Options.GetTypeInfo(typeof(object));

    /// <summary>The arguments of a call the model makes now.</summary>
    public static readonly JsonTypeInfo<Dictionary<string, object?>> ReadArguments =
        (JsonTypeInfo<Dictionary<string, object?>>)Options.GetTypeInfo(typeof(Dictionary<string, object?>));
}
