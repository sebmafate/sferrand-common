using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace SFerrand.Common.Security;

/// <summary>
/// Serializes objects to JSON for logs, with every property marked <see cref="SensitiveDataAttribute"/> masked.
/// </summary>
/// <remarks>
/// <para>
/// <b>Behaviour.</b> A sensitive <see cref="string"/> property is written masked according to its <see cref="SensitiveDataKind"/>
/// (same length as the original). A sensitive property of any other type (a number, an array, a nested object) is written as the
/// constant string <c>"***"</c>, since there is no meaningful partial masking. The output is meant to be read by people and log
/// tools: it is not meant to be read back. A sensitive <see cref="string"/> property would be deserialized as its masked text, and
/// reading any other sensitive property throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// <b>Scope.</b> It relies on the reflection-based metadata of <c>System.Text.Json</c>. A source-generated context does not run the
/// modifier. Only the declared attribute is honoured: there is no guessing from property names (see <see cref="SensitiveKeys"/> for
/// dictionaries and headers).
/// </para>
/// </remarks>
public static class SensitiveDataJson
{
    private const string ConstantMask = "***";

    private static readonly Lazy<JsonSerializerOptions> DefaultOptions = new(() => CreateOptions());

    /// <summary>
    /// A type-info modifier that masks the properties marked <see cref="SensitiveDataAttribute"/>.
    /// Add it to a <see cref="DefaultJsonTypeInfoResolver"/> to build your own options.
    /// </summary>
    /// <param name="typeInfo">The metadata being built.</param>
    public static void Modifier(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            var attribute = property.AttributeProvider?.GetCustomAttributes(typeof(SensitiveDataAttribute), inherit: true)
                .OfType<SensitiveDataAttribute>().FirstOrDefault();
            if (attribute is null)
                continue;

            var getter = property.Get;
            if (property.PropertyType == typeof(string) && getter is not null)
            {
                property.Get = instance => attribute.Mask((string?)getter(instance));
            }
            else
            {
                var converterType = typeof(ConstantMaskConverter<>).MakeGenericType(property.PropertyType);
                property.CustomConverter = (JsonConverter)Activator.CreateInstance(converterType)!;
            }
        }
    }

    /// <summary>Creates serializer options that apply <see cref="Modifier"/>.</summary>
    /// <param name="template">
    /// Options to copy (naming policy, indentation, converters...). Its <see cref="JsonSerializerOptions.TypeInfoResolver"/> is
    /// replaced by a default reflection resolver carrying the modifier. <see langword="null"/> uses the <c>System.Text.Json</c> defaults.
    /// </param>
    /// <remarks>Build the options <b>once</b> and keep them: <c>System.Text.Json</c> caches its metadata per options instance.</remarks>
    public static JsonSerializerOptions CreateOptions(JsonSerializerOptions? template = null)
    {
        var options = template is null ? new JsonSerializerOptions() : new JsonSerializerOptions(template);
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(Modifier);
        options.TypeInfoResolver = resolver;
        return options;
    }

    /// <summary>Serializes an object to JSON for logging, masking its sensitive properties.</summary>
    /// <typeparam name="T">Type of the object (the declared type is serialized, as for <see cref="JsonSerializer"/>).</typeparam>
    /// <param name="value">The object. <see langword="null"/> gives <c>null</c>.</param>
    /// <param name="options">Options built with <see cref="CreateOptions"/>. <see langword="null"/> uses a shared compact default.</param>
    /// <returns>The JSON text.</returns>
    public static string ToSafeJsonString<T>(this T? value, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(value, options ?? DefaultOptions.Value);

    private sealed class ConstantMaskConverter<T> : JsonConverter<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException("A sensitive property is masked for logging and cannot be read back.");

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(ConstantMask);
    }
}
