using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// The model's judgement on one question, shaped by the kind of question asked.
/// </summary>
/// <remarks>
/// Match it against <see cref="ChoiceAnswer"/>, <see cref="NoulAnswer"/> or <see cref="ScoreAnswer"/>:
/// each question comes back as the kind it was asked as.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChoiceAnswer), "choice")]
[JsonDerivedType(typeof(NoulAnswer), "noul")]
[JsonDerivedType(typeof(ScoreAnswer), "score")]
public abstract record SystemOneAnswer;
