using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SFerrand.Common.Extensions;

/// <summary>
/// String helpers: case conversion, null/empty fallbacks, Base64 and diacritics.
/// </summary>
public static class StringExtensions
{
    // Splits identifiers into words, whatever their original convention:
    //   "XMLHttpRequest2" => XML, Http, Request2     "snake_case-value" => snake, case, value
    private static readonly Regex WordPattern = new(
        @"\p{Lu}+(?=\p{Lu}\p{Ll})|\p{Lu}?\p{Ll}+\p{Nd}*|\p{Lu}+\p{Nd}*|\p{Nd}+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static IEnumerable<string> Words(string value)
        => WordPattern.Matches(value).Select(m => m.Value);

    private static string Capitalize(string word)
        => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    #region Case conversion

    /// <summary>Converts to PascalCase: <c>"hello big world"</c> gives <c>"HelloBigWorld"</c>.</summary>
    public static string ToPascalCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return string.Concat(Words(str).Select(Capitalize));
    }

    /// <summary>Converts to camelCase: <c>"hello big world"</c> gives <c>"helloBigWorld"</c>.</summary>
    public static string ToCamelCase(this string str)
    {
        var pascal = str.ToPascalCase();
        return pascal.Length == 0 ? pascal : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    /// <summary>
    /// Converts to Title Case using the invariant culture: <c>"hello big world"</c> gives <c>"Hello Big World"</c>.
    /// Words that are already fully upper-case are left untouched (acronyms).
    /// </summary>
    public static string ToTitleCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(str);
    }

    /// <summary>Converts to snake_case: <c>"Hello big World"</c> gives <c>"hello_big_world"</c>.</summary>
    public static string ToSnakeCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return string.Join('_', Words(str)).ToLowerInvariant();
    }

    /// <summary>Converts to CONSTANT_CASE: <c>"Hello big World"</c> gives <c>"HELLO_BIG_WORLD"</c>.</summary>
    public static string ToConstantCase(this string str) => str.ToSnakeCase().ToUpperInvariant();

    /// <summary>Converts to kebab-case: <c>"Hello big World"</c> gives <c>"hello-big-world"</c>.</summary>
    public static string ToKebabCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return string.Join('-', Words(str)).ToLowerInvariant();
    }

    /// <summary>Converts to COBOL-CASE: <c>"Hello big World"</c> gives <c>"HELLO-BIG-WORLD"</c>.</summary>
    public static string ToCobolCase(this string str) => str.ToKebabCase().ToUpperInvariant();

    /// <summary>Converts to Train-Case: <c>"hello big world"</c> gives <c>"Hello-Big-World"</c>.</summary>
    public static string ToTrainCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return string.Join('-', Words(str).Select(Capitalize));
    }

    /// <summary>Converts to aLtErNaTiNg cAsE, starting with a lower-case first character: <c>"hello"</c> gives <c>"hElLo"</c>.</summary>
    public static string ToAlternatingCase(this string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return string.Create(str.Length, str, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = i % 2 == 0 ? char.ToLowerInvariant(source[i]) : char.ToUpperInvariant(source[i]);
        });
    }

    #endregion

    #region Null / empty fallbacks

    /// <summary>Returns <paramref name="defaultValue"/> when the string is <see langword="null"/>.</summary>
    public static string WhenNull(this string? str, string defaultValue = "") => str ?? defaultValue;

    /// <summary>Returns <paramref name="defaultValue"/> when the string is <see langword="null"/> or empty.</summary>
    public static string WhenNullOrEmpty(this string? str, string defaultValue)
        => string.IsNullOrEmpty(str) ? defaultValue : str;

    /// <summary>Returns <paramref name="defaultValue"/> when the string is <see langword="null"/>, empty or white space.</summary>
    public static string WhenNullOrEmptyOrWhiteSpace(this string? str, string defaultValue = "")
        => string.IsNullOrWhiteSpace(str) ? defaultValue : str;

    /// <summary>Fluent form of <see cref="string.IsNullOrEmpty(string?)"/>.</summary>
    public static bool IsNullOrEmpty(this string? str) => string.IsNullOrEmpty(str);

    /// <summary>Fluent form of <see cref="string.IsNullOrWhiteSpace(string?)"/>.</summary>
    public static bool IsNullOrWhiteSpace(this string? str) => string.IsNullOrWhiteSpace(str);

    #endregion

    #region Encoding

    /// <summary>Decodes a byte array as UTF-16 (<see cref="Encoding.Unicode"/>).</summary>
    public static string ToUnicodeString(this byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Encoding.Unicode.GetString(bytes);
    }

    /// <summary>
    /// Returns <see langword="true"/> when the string is valid, padded, standard-alphabet Base64.
    /// Whitespace is not tolerated.
    /// </summary>
    public static bool IsBase64String(this string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (s.Length == 0 || s.Length % 4 != 0 || s.Any(char.IsWhiteSpace))
            return false;

        var buffer = new byte[s.Length / 4 * 3];
        return Convert.TryFromBase64String(s, buffer, out _);
    }

    /// <summary>Encodes the string to Base64 using <paramref name="encoding"/> (UTF-8 by default).</summary>
    public static string ToBase64String(this string s, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return Convert.ToBase64String((encoding ?? Encoding.UTF8).GetBytes(s));
    }

    /// <summary>Decodes a Base64 string using <paramref name="encoding"/> (UTF-8 by default).</summary>
    public static string FromBase64String(this string s, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return (encoding ?? Encoding.UTF8).GetString(Convert.FromBase64String(s));
    }

    /// <summary>
    /// Removes diacritics: <c>"Sébastien Ferrând"</c> gives <c>"Sebastien Ferrand"</c>.
    /// Returns an empty string for <see langword="null"/>.
    /// </summary>
    public static string RemoveDiacritics(this string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    #endregion
}
