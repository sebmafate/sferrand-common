using System.Diagnostics.CodeAnalysis;

namespace SFerrand.Common.Security;

/// <summary>
/// Helpers that mask sensitive values (secrets, credentials, e-mails, identifiers) so they can be logged safely.
/// </summary>
/// <remarks>
/// Every method is fail-safe: when the input does not have the expected structure, the whole value is masked
/// rather than returned as is, and none of them throws on malformed input.
/// </remarks>
public static class SecretObfuscator
{
    /// <summary>The character used to mask hidden characters by default.</summary>
    public const char DefaultMask = '*';

    /// <summary>
    /// Well-known token prefixes that <see cref="ObfuscateToken"/> keeps visible by default
    /// (GitHub personal access, OAuth, user-to-server, server-to-server and refresh tokens).
    /// </summary>
    public static IReadOnlyList<string> KnownTokenPrefixes { get; } =
        new[] { "github_pat_", "ghp_", "gho_", "ghu_", "ghs_", "ghr_" };

    /// <summary>
    /// Returns how many characters are left visible on each side of a value of the given length
    /// when no explicit count is requested.
    /// </summary>
    /// <param name="totalLength">Length of the value to obfuscate.</param>
    /// <returns>0 up to 4 characters, 1 up to 8, 2 up to 16, 4 above.</returns>
    public static int GetNumberOfPreservedChars(int totalLength) => totalLength switch
    {
        <= 4 => 0,
        <= 8 => 1,
        <= 16 => 2,
        _ => 4,
    };

    /// <summary>
    /// Replaces every character by <paramref name="mask"/>, except a number of leading and trailing characters.
    /// </summary>
    /// <param name="input">The value to obfuscate. <see langword="null"/> and empty values are returned unchanged.</param>
    /// <param name="nbLeading">Characters kept at the start. <see langword="null"/> picks an adaptive default.</param>
    /// <param name="nbTrailing">Characters kept at the end. <see langword="null"/> picks an adaptive default.</param>
    /// <param name="mask">The masking character.</param>
    /// <remarks>
    /// If the requested leading and trailing counts would reveal the whole value, the adaptive default is used instead,
    /// so a value is never returned in clear text.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(input))]
    public static string? Obfuscate(this string? input, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var autoLength = GetNumberOfPreservedChars(input.Length);
        var leading = Math.Max(nbLeading ?? autoLength, 0);
        var trailing = Math.Max(nbTrailing ?? autoLength, 0);

        if (leading + trailing >= input.Length)
            leading = trailing = autoLength;

        return string.Concat(
            input.AsSpan(0, leading),
            new string(mask, input.Length - leading - trailing),
            input.AsSpan(input.Length - trailing));
    }

    /// <summary>
    /// Masks the credentials of a URI shaped like <c>scheme://user:password@host/path</c>.
    /// </summary>
    /// <param name="uri">The URI to sanitize.</param>
    /// <returns>
    /// <list type="bullet">
    /// <item>with <c>user:password</c> credentials: the user name is kept, the password is masked;</item>
    /// <item>with a user info without <c>:</c> (e.g. a token used as user name): the whole user info is masked;</item>
    /// <item>without credentials: the URI, unchanged;</item>
    /// <item>when it does not look like <c>scheme://...</c>: the whole value, masked.</item>
    /// </list>
    /// </returns>
    [return: NotNullIfNotNull(nameof(uri))]
    public static string? ObfuscateUri(this string? uri)
    {
        if (string.IsNullOrEmpty(uri))
            return uri;

        var schemeEnd = uri.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
            return Obfuscate(uri); // unknown structure: hide everything

        var authorityStart = schemeEnd + 3;
        var authorityEnd = uri.IndexOfAny(new[] { '/', '?', '#' }, authorityStart);
        if (authorityEnd < 0)
            authorityEnd = uri.Length;

        // The last '@' of the authority separates the user info from the host (a password may contain '@').
        var at = uri.LastIndexOf('@', authorityEnd - 1, authorityEnd - authorityStart);
        if (at < 0)
            return uri;

        var userInfo = uri.Substring(authorityStart, at - authorityStart);
        var colon = userInfo.IndexOf(':');
        var maskedUserInfo = colon < 0
            ? Obfuscate(userInfo)
            : string.Concat(userInfo.AsSpan(0, colon + 1), Obfuscate(userInfo.Substring(colon + 1)));

        return string.Concat(uri.AsSpan(0, authorityStart), maskedUserInfo, uri.AsSpan(at));
    }

    /// <inheritdoc cref="ObfuscateUri(string?)"/>
    public static string ObfuscateUri(this Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return ObfuscateUri(uri.OriginalString)!;
    }

    /// <summary>
    /// Masks the value of one or more keys in a <c>key=value;key=value</c> connection string.
    /// Every other part of the string is preserved verbatim.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="key">The key whose value must be masked (case-insensitive).</param>
    /// <param name="nbLeading">Characters kept at the start of the value.</param>
    /// <param name="nbTrailing">Characters kept at the end of the value.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>
    /// The connection string with the value masked, or the whole string masked when <paramref name="key"/> is
    /// empty or not found. Values containing a quoted <c>;</c> are not supported.
    /// </returns>
    [return: NotNullIfNotNull(nameof(connectionString))]
    public static string? ObfuscateConnectionString(this string? connectionString, string? key, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask)
    {
        if (string.IsNullOrEmpty(connectionString))
            return connectionString;

        if (string.IsNullOrWhiteSpace(key))
            return Obfuscate(connectionString, mask: mask);

        var segments = connectionString.Split(';');
        var found = false;
        for (var i = 0; i < segments.Length; i++)
        {
            var separator = segments[i].IndexOf('=');
            if (separator < 0)
                continue;

            if (!segments[i].AsSpan(0, separator).Trim().Equals(key.AsSpan().Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            segments[i] = string.Concat(
                segments[i].AsSpan(0, separator + 1),
                Obfuscate(segments[i].Substring(separator + 1), nbLeading, nbTrailing, mask));
            found = true;
        }

        return found ? string.Join(';', segments) : Obfuscate(connectionString, mask: mask);
    }

    /// <summary>
    /// Masks the local part of an e-mail address. Each alphanumeric run of the local part
    /// (<c>jean-claude.van-damme</c> gives <c>****-c****e.***-d***e</c>) is obfuscated on its own, separators and domain are kept.
    /// </summary>
    /// <param name="email">The e-mail address.</param>
    /// <param name="nbLeading">Characters kept at the start of each run.</param>
    /// <param name="nbTrailing">Characters kept at the end of each run.</param>
    /// <param name="mask">The masking character.</param>
    /// <param name="shouldSkip">
    /// Optional predicate receiving the address: when it returns <see langword="true"/> the address is returned
    /// unchanged (mailing lists, technical accounts, public domains...).
    /// </param>
    /// <returns>The obfuscated address. A value that is not a valid <c>local@domain</c> shape is fully masked.</returns>
    [return: NotNullIfNotNull(nameof(email))]
    public static string? ObfuscateEmail(this string? email, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask, Func<string, bool>? shouldSkip = null)
    {
        if (string.IsNullOrEmpty(email))
            return email;

        if (shouldSkip?.Invoke(email) == true)
            return email;

        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
            return Obfuscate(email, mask: mask);

        var local = email.AsSpan(0, at);
        var result = new System.Text.StringBuilder(email.Length);
        var runStart = -1;
        for (var i = 0; i <= local.Length; i++)
        {
            var isAlphanumeric = i < local.Length && char.IsLetterOrDigit(local[i]);
            if (isAlphanumeric)
            {
                if (runStart < 0) runStart = i;
                continue;
            }

            if (runStart >= 0)
            {
                result.Append(Obfuscate(local[runStart..i].ToString(), nbLeading, nbTrailing, mask));
                runStart = -1;
            }

            if (i < local.Length)
                result.Append(local[i]);
        }

        return result.Append(email, at, email.Length - at).ToString();
    }

    /// <summary>
    /// Masks a GUID written in its canonical <c>D</c> form (<c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>).
    /// The third group, which carries the version, is kept so the kind of GUID stays recognizable.
    /// </summary>
    /// <param name="guid">The GUID.</param>
    /// <param name="nbLeading">Characters kept at the start of each masked group.</param>
    /// <param name="nbTrailing">Characters kept at the end of each masked group.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>The obfuscated GUID. A value that is not a canonical GUID is fully masked.</returns>
    [return: NotNullIfNotNull(nameof(guid))]
    public static string? ObfuscateGuid(this string? guid, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask)
    {
        if (string.IsNullOrEmpty(guid))
            return guid;

        if (!Guid.TryParseExact(guid, "D", out _))
            return Obfuscate(guid, nbLeading, nbTrailing, mask);

        var groups = guid.Split('-');
        for (var i = 0; i < groups.Length; i++)
        {
            if (i != 2)
                groups[i] = Obfuscate(groups[i], nbLeading, nbTrailing, mask)!;
        }

        return string.Join('-', groups);
    }

    /// <inheritdoc cref="ObfuscateGuid(string?, int?, int?, char)"/>
    public static string ObfuscateGuid(this Guid guid, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask)
        => ObfuscateGuid(guid.ToString("D"), nbLeading, nbTrailing, mask)!;

    /// <summary>
    /// Masks an API token while keeping its well-known prefix (such as <c>ghp_</c>) visible.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="nbLeading">Characters kept after the prefix.</param>
    /// <param name="nbTrailing">Characters kept at the end.</param>
    /// <param name="mask">The masking character.</param>
    /// <param name="knownPrefixes">Prefixes to keep visible. <see langword="null"/> uses <see cref="KnownTokenPrefixes"/>.</param>
    [return: NotNullIfNotNull(nameof(token))]
    public static string? ObfuscateToken(this string? token, int? nbLeading = null, int? nbTrailing = null, char mask = DefaultMask, IEnumerable<string>? knownPrefixes = null)
    {
        if (string.IsNullOrEmpty(token))
            return token;

        var prefix = (knownPrefixes ?? KnownTokenPrefixes)
            .Where(p => p.Length > 0 && p.Length < token.Length && token.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();

        return prefix is null
            ? Obfuscate(token, nbLeading, nbTrailing, mask)
            : string.Concat(token.AsSpan(0, prefix.Length), Obfuscate(token[prefix.Length..], nbLeading, nbTrailing, mask));
    }
}
