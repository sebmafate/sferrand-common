using System.Text;

namespace SFerrand.Common.Extensions;

/// <summary>
/// Base64url (RFC 4648 section 5): the Base64 variant that is safe in URLs, file names and JWTs. It uses <c>-</c> and <c>_</c> instead
/// of <c>+</c> and <c>/</c>, and normally omits the <c>=</c> padding.
/// </summary>
/// <remarks>
/// <c>System.Buffers.Text.Base64Url</c> exists from .NET 9; these helpers give the same result on .NET 8 and add a strict validation.
/// Encoding never emits padding. Decoding accepts input with or without correct padding, and rejects whitespace, the standard
/// alphabet (<c>+</c>, <c>/</c>) and any other character.
/// </remarks>
public static class Base64UrlExtensions
{
    /// <summary>Encodes bytes as unpadded Base64url.</summary>
    /// <param name="bytes">The bytes to encode.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    /// <example>
    /// <code>
    /// new byte[] { 0xFB, 0xFF }.ToBase64UrlString();   // -_8
    /// </code>
    /// </example>
    public static string ToBase64UrlString(this byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Encodes the string to unpadded Base64url using <paramref name="encoding"/> (UTF-8 by default).</summary>
    /// <param name="s">The text to encode.</param>
    /// <param name="encoding">Encoding used to turn the text into bytes. Default: UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    public static string ToBase64UrlString(this string s, Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(s);
        return (encoding ?? Encoding.UTF8).GetBytes(s).ToBase64UrlString();
    }

    /// <summary>Decodes a Base64url string to bytes. An empty string gives an empty array.</summary>
    /// <param name="s">Base64url text, with or without padding.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not valid Base64url.</exception>
    public static byte[] FromBase64UrlBytes(this string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return TryDecode(s, out var bytes) ? bytes : throw new FormatException("The input is not a valid Base64url string.");
    }

    /// <summary>Decodes a Base64url string using <paramref name="encoding"/> (UTF-8 by default).</summary>
    /// <param name="s">Base64url text, with or without padding.</param>
    /// <param name="encoding">Encoding used to turn the decoded bytes into text. Default: UTF-8.</param>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    /// <exception cref="FormatException"><paramref name="s"/> is not valid Base64url.</exception>
    public static string FromBase64UrlString(this string s, Encoding? encoding = null)
        => (encoding ?? Encoding.UTF8).GetString(s.FromBase64UrlBytes());

    /// <summary>
    /// Returns <see langword="true"/> when the string is valid, non-empty Base64url: only <c>A-Z a-z 0-9 - _</c>, optionally followed by correct padding.
    /// </summary>
    /// <param name="s">The text to test.</param>
    /// <remarks>An empty string is not reported as Base64url (as for <see cref="StringExtensions.IsBase64String"/>), although it decodes to an empty array.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="s"/> is <see langword="null"/>.</exception>
    public static bool IsBase64UrlString(this string s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.Length > 0 && TryDecode(s, out _);
    }

    private static bool TryDecode(string s, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (s.Length == 0)
            return true;

        var end = s.Length;
        while (end > 0 && s[end - 1] == '=')
            end--;

        var padding = s.Length - end;
        if (padding > 2 || (padding > 0 && s.Length % 4 != 0))
            return false;

        for (var i = 0; i < end; i++)
        {
            var c = s[i];
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
                return false;
        }

        // A single leftover character cannot encode a byte.
        if (end % 4 == 1)
            return false;

        var standard = new StringBuilder(end + 3).Append(s, 0, end).Replace('-', '+').Replace('_', '/');
        standard.Append('=', (4 - end % 4) % 4);
        bytes = Convert.FromBase64String(standard.ToString());
        return true;
    }
}
