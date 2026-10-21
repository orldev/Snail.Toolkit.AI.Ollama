using System.Text.Json;
using System.Text.Json.Serialization;
using Snail.Toolkit.AI.Ollama.Contracts.Requests;
using Snail.Toolkit.AI.Ollama.Contracts.Responses;

namespace Snail.Toolkit.AI.Ollama.Clients.Extensions;

/// <summary>
/// Source-generated metadata for every request and answer on the wire, with the settings of <see cref="Wire.Json"/>.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
[JsonSerializable(typeof(ChatRequest))]
[JsonSerializable(typeof(ChatResponse))]
[JsonSerializable(typeof(GenerateRequest))]
[JsonSerializable(typeof(GenerateResponse))]
[JsonSerializable(typeof(EmbeddingsRequest))]
[JsonSerializable(typeof(EmbeddingsResponse))]
[JsonSerializable(typeof(SystemOneRequest))]
[JsonSerializable(typeof(SystemOneResponse))]
[JsonSerializable(typeof(ModelDescription))]
[JsonSerializable(typeof(ModelsClient.VersionReply))]
[JsonSerializable(typeof(ModelsClient.LocalModels))]
[JsonSerializable(typeof(ModelsClient.RunningModels))]
[JsonSerializable(typeof(ModelsClient.ShowRequest))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Dictionary<string, object?>))]
internal sealed partial class OllamaJson : JsonSerializerContext;
