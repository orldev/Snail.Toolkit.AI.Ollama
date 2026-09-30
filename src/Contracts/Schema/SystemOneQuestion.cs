using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// One question put to a System One model; its kind decides the shape of the answer.
/// </summary>
/// <param name="Instructions">What to judge: a string, or any object or array, sent as JSON.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChoiceQuestion), "choice")]
[JsonDerivedType(typeof(NoulQuestion), "noul")]
[JsonDerivedType(typeof(ScoreQuestion), "score")]
public abstract record SystemOneQuestion(
    [property: JsonPropertyName("instructions")] object Instructions);
