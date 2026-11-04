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
    /// <param name="obj">The object. <see langword="null"/> gives <c>{}</c>.</param>
    /// <param name="options">Serializer options. Defaults to indented output.</param>
    public static string ToJsonString<TObject>(this TObject? obj, JsonSerializerOptions? options = null)
        => obj is null ? EmptyJsonObject : JsonSerializer.Serialize(obj, options ?? IndentedOptions);

    /// <summary>
    /// Deserializes a JSON string.
    /// </summary>
    /// <param name="jsonString">The JSON. <see langword="null"/> gives <paramref name="defaultValue"/>.</param>
    /// <param name="defaultValue">Returned when the input is <see langword="null"/>. Also lets the compiler infer an anonymous type.</param>
    /// <param name="options">Serializer options.</param>
    /// <exception cref="JsonException">The input is not valid JSON for <typeparamref name="TObject"/>.</exception>
    public static TObject? ToObject<TObject>(this string? jsonString, TObject? defaultValue = default, JsonSerializerOptions? options = null)
        => jsonString is null ? defaultValue : JsonSerializer.Deserialize<TObject>(jsonString, options);
}
