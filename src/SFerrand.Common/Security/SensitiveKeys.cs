using System.Text;

namespace SFerrand.Common.Security;

/// <summary>
/// Masks values by the <b>name</b> they travel under: HTTP headers, dictionaries, query strings and form bodies where
/// <c>Authorization</c>, <c>password</c> or <c>access_token</c> must not be logged.
/// </summary>
/// <remarks>
/// <para>
/// A name is sensitive when, ignoring case and the characters <c>- _ . space</c>, it <b>contains</b> one of: <c>password</c>,
/// <c>passwd</c>, <c>pwd</c>, <c>secret</c>, <c>token</c>, <c>apikey</c>, <c>accesskey</c>, <c>privatekey</c>, <c>authorization</c>,
/// <c>cookie</c>, <c>credential</c>, <c>session</c>; or is exactly <c>sig</c> (shared access signatures). So <c>X-Api-Key</c>,
/// <c>client_secret</c> and <c>Set-Cookie</c> match. Matching is deliberately broad: masking too much is the safe direction.
/// Pass <c>additionalNames</c> to add your own (matched the same way).
/// </para>
/// <para>Values are masked with <see cref="SecretObfuscator.Obfuscate"/>, so their length is preserved and a short value is fully hidden.</para>
/// </remarks>
public static class SensitiveKeys
{
    private static readonly string[] Fragments =
    {
        "password", "passwd", "pwd", "secret", "token", "apikey", "accesskey", "privatekey",
        "authorization", "cookie", "credential", "session",
    };

    /// <summary>Tells whether values stored under <paramref name="name"/> must be masked.</summary>
    /// <param name="name">A header, field, dictionary key or query parameter name.</param>
    /// <param name="additionalNames">Extra names to treat as sensitive.</param>
    /// <returns><see langword="false"/> for a <see langword="null"/> or empty name.</returns>
    /// <example>
    /// <code>
    /// SensitiveKeys.IsSensitive("X-Api-Key");     // true
    /// SensitiveKeys.IsSensitive("page");          // false
    /// </code>
    /// </example>
    public static bool IsSensitive(string? name, IEnumerable<string>? additionalNames = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = Normalize(name);
        if (normalized == "sig")
            return true;

        foreach (var fragment in Fragments)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
                return true;
        }

        if (additionalNames is not null)
        {
            foreach (var extra in additionalNames)
            {
                var normalizedExtra = Normalize(extra);
                if (normalizedExtra.Length > 0 && normalized.Contains(normalizedExtra, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Masks <paramref name="value"/> when <paramref name="name"/> is sensitive; returns it unchanged otherwise.</summary>
    /// <param name="name">The header, field or parameter name.</param>
    /// <param name="value">The value.</param>
    /// <param name="additionalNames">Extra names to treat as sensitive.</param>
    /// <remarks>
    /// For an <c>Authorization</c>-like name, a value written as an authentication scheme followed by a credential
    /// (<c>Bearer abc...</c>, <c>Basic abc...</c>) keeps its scheme, which is useful when diagnosing and is not secret.
    /// For every other name the whole value is masked.
    /// </remarks>
    /// <example>
    /// <code>
    /// SensitiveKeys.MaskValue("Authorization", "Bearer abcdefghijklmnop");   // Bearer ab************op
    /// </code>
    /// </example>
    public static string? MaskValue(string? name, string? value, IEnumerable<string>? additionalNames = null)
    {
        if (string.IsNullOrEmpty(value) || !IsSensitive(name, additionalNames))
            return value;

        // Only for an authorization header: the scheme is not secret there, whereas a password containing a space is.
        var space = value.IndexOf(' ');
        if (Normalize(name!).Contains("authorization", StringComparison.Ordinal) && space > 0 && space <= 10 && space < value.Length - 1 && value.AsSpan(0, space).IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz") < 0)
            return string.Concat(value.AsSpan(0, space + 1), SecretObfuscator.Obfuscate(value[(space + 1)..]));

        return SecretObfuscator.Obfuscate(value);
    }

    /// <summary>Masks the values of the sensitive names in a sequence of name/value pairs (headers, form fields, log properties).</summary>
    /// <param name="pairs">The pairs. The sequence is read lazily and not modified.</param>
    /// <param name="additionalNames">Extra names to treat as sensitive.</param>
    /// <returns>The same pairs, in the same order, with the sensitive values masked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="pairs"/> is <see langword="null"/>.</exception>
    public static IEnumerable<KeyValuePair<string, string?>> MaskValues(IEnumerable<KeyValuePair<string, string?>> pairs, IEnumerable<string>? additionalNames = null)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        return pairs.Select(pair => new KeyValuePair<string, string?>(pair.Key, MaskValue(pair.Key, pair.Value, additionalNames)));
    }

    /// <summary>
    /// Makes a URL safe to log: masks the password of its user info and the value of its sensitive query parameters.
    /// </summary>
    /// <param name="url">An absolute or relative URL. <see langword="null"/> and empty values are returned unchanged.</param>
    /// <param name="additionalNames">Extra parameter names to treat as sensitive.</param>
    /// <returns>The URL with the same length and structure; only the secret parts are replaced. The fragment is left alone.</returns>
    /// <example>
    /// <code>
    /// "https://api.example.com/v1/items?page=2&amp;access_token=abcdefghijklmnop".ObfuscateUrl();
    /// // https://api.example.com/v1/items?page=2&amp;access_token=ab************op
    /// </code>
    /// </example>
    public static string? ObfuscateUrl(this string? url, IEnumerable<string>? additionalNames = null)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        // ObfuscateUri masks a whole value that is not "scheme://...": a relative URL has no user info to hide and must stay readable.
        var withoutCredentials = url.Contains("://", StringComparison.Ordinal) ? url.ObfuscateUri() : url;
        var queryStart = withoutCredentials.IndexOf('?');
        var fragmentStart = withoutCredentials.IndexOf('#');

        // No query, or a '?' that sits inside the fragment (a fragment is never sent to a server and is not a query).
        if (queryStart < 0 || (fragmentStart >= 0 && fragmentStart < queryStart))
            return withoutCredentials;

        var queryEnd = fragmentStart < 0 ? withoutCredentials.Length : fragmentStart;

        return string.Concat(
            withoutCredentials.AsSpan(0, queryStart + 1),
            ObfuscateQuery(withoutCredentials.Substring(queryStart + 1, queryEnd - queryStart - 1), additionalNames),
            withoutCredentials.AsSpan(queryEnd));
    }

    /// <summary>
    /// Masks the values of the sensitive parameters of a bare query string or <c>application/x-www-form-urlencoded</c> body
    /// (<c>a=1&amp;password=hunter2</c>).
    /// </summary>
    /// <param name="query">The query, without the leading <c>?</c>. <see langword="null"/> and empty values are returned unchanged.</param>
    /// <param name="additionalNames">Extra parameter names to treat as sensitive.</param>
    /// <returns>The query with the same length; parameter names, separators and non-sensitive values are untouched.</returns>
    public static string? ObfuscateQuery(this string? query, IEnumerable<string>? additionalNames = null)
    {
        if (string.IsNullOrEmpty(query))
            return query;

        var result = new StringBuilder(query.Length);
        foreach (var (parameter, index) in query.Split('&').Select((p, i) => (p, i)))
        {
            if (index > 0)
                result.Append('&');

            var equals = parameter.IndexOf('=');
            if (equals <= 0)
            {
                result.Append(parameter);
                continue;
            }

            var name = Uri.UnescapeDataString(parameter[..equals].Replace('+', ' ')); // invalid escapes are left as is, never thrown on
            result.Append(parameter, 0, equals + 1);
            result.Append(IsSensitive(name, additionalNames) ? SecretObfuscator.Obfuscate(parameter[(equals + 1)..]) : parameter[(equals + 1)..]);
        }

        return result.ToString();
    }

    private static string Normalize(string name)
        => new(name.Where(c => c is not ('-' or '_' or '.' or ' ')).Select(char.ToLowerInvariant).ToArray());
}
