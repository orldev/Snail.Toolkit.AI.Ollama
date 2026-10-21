using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Mapping;

/// <summary>Source-generated metadata for the tool-call arguments the mapping writes and reads.</summary>
[JsonSerializable(typeof(IDictionary<string, object?>))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
internal sealed partial class MappingJson : JsonSerializerContext;
