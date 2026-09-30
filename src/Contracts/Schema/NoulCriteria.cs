using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The wording of the two candidates a yes/no question is scored between.
/// </summary>
/// <param name="No">Describes the false candidate; null keeps "No".</param>
/// <param name="Yes">Describes the true candidate; null keeps "Yes".</param>
public sealed record NoulCriteria(
    [property: JsonPropertyName("false"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? No = null,
    [property: JsonPropertyName("true"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Yes = null);
