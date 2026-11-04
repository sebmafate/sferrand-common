using System.Text.Json;

namespace SFerrand.Common.Extensions;

/// <summary>
/// Shortcuts around <see cref="JsonSerializer"/>.
/// </summary>
public static class JsonExtensions
{
    private const string EmptyJsonObject = "{}";

    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };

    /// <summary>
    /// Serializes an object to JSON.
    /// </summary>
    /// <returns>The JSON text, indented unless <paramref name="options"/> says otherwise.</returns>
    /// <typeparam name="TObject">Type of the object; the runtime type is not used, so a variable typed as a base class serializes only the properties of that base class (use <see cref="object"/> to get the runtime type).</typeparam>
    /// <param name="obj">The object. <see langword="null"/> gives <c>{}</c>.</param>
    /// <param name="options">Serializer options. Defaults to indented output.</param>
    public static string ToJsonString<TObject>(this TObject? obj, JsonSerializerOptions? options = null)
        => obj is null ? EmptyJsonObject : JsonSerializer.Serialize(obj, options ?? IndentedOptions);

    /// <summary>
    /// Deserializes a JSON string.
    /// </summary>
    /// <returns>The object, or <paramref name="defaultValue"/> for a <see langword="null"/> input.</returns>
    /// <example>
    /// <code>
    /// var point = "{\"X\":1,\"Y\":2}".ToObject(new { X = 0, Y = 0 });   // anonymous type inferred from the default value
    /// </code>
    /// </example>
    /// <typeparam name="TObject">Type to deserialize into.</typeparam>
    /// <param name="jsonString">The JSON. <see langword="null"/> gives <paramref name="defaultValue"/>.</param>
    /// <param name="defaultValue">Returned when the input is <see langword="null"/>. Also lets the compiler infer an anonymous type.</param>
    /// <param name="options">Serializer options.</param>
    /// <exception cref="JsonException">The input is not valid JSON for <typeparamref name="TObject"/>.</exception>
    public static TObject? ToObject<TObject>(this string? jsonString, TObject? defaultValue = default, JsonSerializerOptions? options = null)
        => jsonString is null ? defaultValue : JsonSerializer.Deserialize<TObject>(jsonString, options);
}
