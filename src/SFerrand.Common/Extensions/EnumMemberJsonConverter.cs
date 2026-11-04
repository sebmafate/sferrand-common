using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SFerrand.Common.Extensions;

/// <summary>
/// Reads and writes an enum as a string, honouring <see cref="EnumMemberAttribute"/>.
/// </summary>
/// <remarks>
/// <code>
/// [JsonConverter(typeof(EnumMemberJsonConverter&lt;State&gt;))]
/// public enum State { Open, [EnumMember(Value = "in_progress")] InProgress }
/// </code>
/// Reading accepts the member name or its <c>EnumMember</c> value (case-insensitive) and defined numeric values.
/// Anything else raises a <see cref="JsonException"/> instead of silently producing the default value.
/// Writing uses the <c>EnumMember</c> value when present, the member name otherwise.
/// <see cref="FlagsAttribute"/> combinations are not supported.
/// </remarks>
/// <typeparam name="TEnum">The enum type.</typeparam>
public sealed class EnumMemberJsonConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private readonly Dictionary<string, TEnum> _fromString = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<TEnum, string> _toString = new();

    /// <summary>Builds the name lookup tables for <typeparamref name="TEnum"/>.</summary>
    public EnumMemberJsonConverter()
    {
        foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = (TEnum)field.GetValue(null)!;
            var wireName = field.GetCustomAttribute<EnumMemberAttribute>()?.Value ?? field.Name;

            _fromString.TryAdd(field.Name, value);
            _fromString.TryAdd(wireName, value);
            _toString.TryAdd(value, wireName);
        }
    }

    /// <inheritdoc />
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var text = reader.GetString();
                if (text is not null && _fromString.TryGetValue(text, out var fromName))
                    return fromName;
                throw new JsonException($"'{text}' is not a valid {typeof(TEnum).Name} value.");

            case JsonTokenType.Number when reader.TryGetInt64(out var number):
                var candidate = (TEnum)Enum.ToObject(typeof(TEnum), number);
                if (Enum.IsDefined(candidate))
                    return candidate;
                throw new JsonException($"{number} is not a defined {typeof(TEnum).Name} value.");

            default:
                throw new JsonException($"Unexpected token {reader.TokenType} when reading {typeof(TEnum).Name}.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        if (_toString.TryGetValue(value, out var name))
            writer.WriteStringValue(name);
        else
            writer.WriteNumberValue(Convert.ToInt64(value)); // undefined value: write its number rather than failing (reading it back is rejected, see remarks)
    }

    /// <inheritdoc />
    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (text is not null && _fromString.TryGetValue(text, out var value))
            return value;
        throw new JsonException($"'{text}' is not a valid {typeof(TEnum).Name} value.");
    }

    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        => writer.WritePropertyName(_toString.TryGetValue(value, out var name) ? name : Convert.ToInt64(value).ToString());
}
