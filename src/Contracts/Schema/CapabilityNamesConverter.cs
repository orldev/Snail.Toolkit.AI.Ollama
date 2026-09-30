using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.Toolkit.AI.Ollama.Contracts.Schema;

/// <summary>
/// Reads a model's capabilities leniently: the names in the list are kept, anything else in it is skipped, and a value
/// that is not a list reads as "not reported".
/// </summary>
/// <remarks>
/// Capabilities are advisory metadata beside the window, the template and the think values of the same answer. Read
/// strictly, one entry of an unexpected shape failed the whole /api/show and cost the caller all of them — the same
/// trade <see cref="Clients.Extensions.Wire"/> already refuses for a reordered "type" field.
/// </remarks>
internal sealed class CapabilityNamesConverter : JsonConverter<IReadOnlyList<string>?>
{
    /// <inheritdoc />
    public override bool HandleNull => true;

    /// <inheritdoc />
    public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.StartArray)
        {
            reader.Skip();
            return null;
        }

        List<string> names = [];

        while (reader.Read() && reader.TokenType is not JsonTokenType.EndArray)
        {
            if (reader.TokenType is JsonTokenType.String)
                names.Add(reader.GetString()!);
            else
                reader.Skip();
        }

        return names;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string>? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();

        foreach (var name in value)
            writer.WriteStringValue(name);

        writer.WriteEndArray();
    }
}
