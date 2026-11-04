using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using SFerrand.Common.Validation;
using static SFerrand.Common.Internal.Layout;

namespace SFerrand.Common.Security;

/// <summary>The categories of sensitive data <see cref="SecretRedactor"/> looks for.</summary>
[Flags]
public enum RedactionKinds
{
    /// <summary>Nothing.</summary>
    None = 0,

    /// <summary>The password of URIs written <c>scheme://user:password@host</c>.</summary>
    UriCredentials = 1,

    /// <summary>Credentials after <c>Bearer</c>, <c>Basic</c>, <c>Digest</c> or <c>Token</c>.</summary>
    AuthorizationHeaders = 2,

    /// <summary>JSON Web Tokens (<c>eyJ...</c>).</summary>
    Jwt = 4,

    /// <summary>Tokens with a well-known prefix: GitHub (<c>ghp_</c>, <c>github_pat_</c>...), AWS access key IDs, Slack tokens.</summary>
    ApiTokens = 8,

    /// <summary>The value of <c>password=</c>, <c>"secret": </c>, <c>x-api-key:</c> and similar named secrets.</summary>
    KeyValueSecrets = 16,

    /// <summary>IBAN.</summary>
    Iban = 32,

    /// <summary>French RIB.</summary>
    Rib = 64,

    /// <summary>French social security numbers (NIR).</summary>
    FrenchNir = 128,

    /// <summary>Payment card numbers.</summary>
    PaymentCards = 256,

    /// <summary>E-mail addresses.</summary>
    Emails = 512,

    /// <summary>Every category.</summary>
    All = UriCredentials | AuthorizationHeaders | Jwt | ApiTokens | KeyValueSecrets | Iban | Rib | FrenchNir | PaymentCards | Emails,
}

/// <summary>Settings of <see cref="SecretRedactor"/>.</summary>
public sealed class RedactionOptions
{
    /// <summary>The categories to look for. Default: <see cref="RedactionKinds.All"/>.</summary>
    public RedactionKinds Kinds { get; init; } = RedactionKinds.All;

    /// <summary>
    /// Requires a number to pass its check digit before it is masked (Luhn for cards, mod 97 for IBAN, the RIB and NIR keys), and a card
    /// number to start with a digit from 2 to 6 (the range of the payment networks). Default: <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// <c>true</c> favours precision: random digit strings (identifiers, timestamps) are left alone, but a mistyped card number is not masked.
    /// <c>false</c> favours recall: anything shaped like a card, IBAN, RIB or NIR is masked, at the price of more false positives.
    /// </remarks>
    public bool RequireChecksum { get; init; } = true;

    /// <summary>Optional predicate receiving an e-mail address: when it returns <see langword="true"/> the address is left as is (shared mailboxes...).</summary>
    public Func<string, bool>? ShouldSkipEmail { get; init; }
}

/// <summary>
/// Finds and masks sensitive data inside <b>free text</b>: log lines, exception messages, request and response bodies.
/// </summary>
/// <remarks>
/// <para>
/// The other obfuscators need to know what a value is. This one reads text and recognizes, by shape and check digit: credentials in URIs,
/// <c>Bearer</c>/<c>Basic</c> credentials, JWTs, tokens with a known prefix, <c>password=...</c>-style pairs, IBAN, RIB, NIR, payment cards
/// and e-mail addresses. Each match is masked with the obfuscator of its kind, so the result keeps <b>the same length</b> and the
/// same layout, and masking twice gives the same text.
/// </para>
/// <para>
/// <b>It is a best effort, not a guarantee.</b> A secret in a form it does not know (a random password in prose, a token without prefix)
/// is not found, and a named value ends at the first space, quote, <c>&amp;</c>, <c>;</c> or <c>,</c>. When you know where the secret is,
/// use the typed obfuscators or <see cref="SensitiveDataAttribute"/>; use this class as a safety net for text you do not control.
/// Precision is tuned by <see cref="RedactionOptions.RequireChecksum"/>.
/// </para>
/// <para>
/// <b>Safe on hostile input.</b> Every pattern is bounded (no unbounded repetition before the anchor that identifies a match), so the
/// cost grows linearly with the size of the text. This is asserted by tests on half a megabyte of worst-case shapes.
/// </para>
/// </remarks>
public static class SecretRedactor
{
    private static readonly RedactionOptions DefaultOptions = new();

    // Every pattern is built so that the work done from any starting position is bounded: no unbounded repetition in front of the
    // part that anchors the match (a keyword, "@", "://"). That is what keeps a hostile text (a megabyte of "a.a.a.a." or of "token=")
    // from making the standard backtracking engine quadratic. The scaling is asserted by tests. The linear-time engine
    // (RegexOptions.NonBacktracking) was tried and dropped: after very large inputs processed concurrently, a shared instance
    // returned truncated replacements.
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static readonly Lazy<Regex> UriPattern = new(() => new(
        @"[A-Za-z][A-Za-z0-9+.\-]{1,19}://[^\s""'<>]+", Options));

    private static readonly Lazy<Regex> AuthorizationPattern = new(() => new(
        @"\b(?<scheme>Bearer|Basic|Digest|Token)(?<space>\s{1,8})(?<credential>[A-Za-z0-9._~+/=\-]{16,})", Options | RegexOptions.IgnoreCase));

    private static readonly Lazy<Regex> JwtPattern = new(() => new(
        @"\beyJ[A-Za-z0-9_\-]{4,}\.eyJ[A-Za-z0-9_\-]{4,}\.[A-Za-z0-9_\-]*", Options));

    private static readonly Lazy<Regex> TokenPattern = new(() => new(
        @"\b(?<prefix>github_pat_|gh[pousr]_|AKIA|ASIA|xox[abposr]-)(?<body>[A-Za-z0-9_\-]{16,})", Options));

    // Starts at the keyword (what comes before it is not part of the match and is left as is).
    private static readonly Lazy<Regex> KeyValuePattern = new(() => new(
        @"(?<key>(?:password|passwd|pwd|secret|token|api[_\-]?key|access[_\-]?key|private[_\-]?key|authorization|credential)[A-Za-z0-9_.\-]{0,64})"
        + @"(?<sep>[""']?\s{0,8}[:=]\s{0,8})(?<scheme>(?:Bearer|Basic|Digest|Token)\s{1,8})?(?<quote>[""']?)(?<value>[^\s""'&;,]+)",
        Options | RegexOptions.IgnoreCase));

    private static readonly Lazy<Regex> IbanPattern = new(() => new(
        @"\b[A-Z]{2}\d{2}(?:[ \-]?[A-Z0-9]{4}){2,7}(?:[ \-]?[A-Z0-9]{1,3})?\b", Options));

    private static readonly Lazy<Regex> RibPattern = new(() => new(
        @"\b\d{5}[ \-]?\d{5}[ \-]?[0-9A-Za-z]{11}[ \-]?\d{2}\b", Options));

    private static readonly Lazy<Regex> NirPattern = new(() => new(
        @"\b[1-478][ \-]?\d{2}[ \-]?\d{2}[ \-]?(?:\d{2}|2[AB])[ \-]?\d{3}[ \-]?\d{3}[ \-]?\d{2}\b", Options));

    private static readonly Lazy<Regex> CardPattern = new(() => new(
        @"\b\d(?:[ \-]?\d){11,18}\b", Options));

    private static readonly Lazy<Regex> EmailPattern = new(() => new(
        @"\b[A-Za-z0-9._%+\-]{1,254}@[A-Za-z0-9\-]{1,63}(?:\.[A-Za-z0-9\-]{1,63}){0,8}\.[A-Za-z]{2,24}\b", Options));

    /// <summary>Masks the sensitive data found in <paramref name="text"/>.</summary>
    /// <param name="text">Any text. <see langword="null"/> and empty values are returned unchanged.</param>
    /// <param name="options">What to look for and how strict to be. <see langword="null"/> means every category, with check digits required.</param>
    /// <returns>
    /// The text with every match masked. The result has <b>exactly the same length</b>; the input instance itself is returned when nothing was found.
    /// </returns>
    /// <example>
    /// <code>
    /// "Connecting to postgres://app:s3cr3t-pass@db/shop with token=abcdef123456 for ada@example.com".Redact();
    /// // Connecting to postgres://app:s3*******ss@db/shop with token=ab********56 for ***@example.com
    /// "card 4111 1111 1111 1111 expires 12/30".Redact();
    /// // card **** **** **** 1111 expires 12/30
    /// </code>
    /// </example>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? Redact(this string? text, RedactionOptions? options = null)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        options ??= DefaultOptions;
        var kinds = options.Kinds;
        var requireChecksum = options.RequireChecksum;
        var result = text;

        if (kinds.HasFlag(RedactionKinds.UriCredentials))
            result = UriPattern.Value.Replace(result, m => RedactUris(m.Value));

        if (kinds.HasFlag(RedactionKinds.AuthorizationHeaders))
        {
            result = AuthorizationPattern.Value.Replace(result, m =>
                m.Groups["scheme"].Value + m.Groups["space"].Value + SecretObfuscator.Obfuscate(m.Groups["credential"].Value));
        }

        if (kinds.HasFlag(RedactionKinds.Jwt))
            result = JwtPattern.Value.Replace(result, m => SecretObfuscator.Obfuscate(m.Value)!);

        if (kinds.HasFlag(RedactionKinds.ApiTokens))
            result = TokenPattern.Value.Replace(result, m => SecretObfuscator.ObfuscateToken(m.Value, knownPrefixes: new[] { m.Groups["prefix"].Value })!);

        if (kinds.HasFlag(RedactionKinds.KeyValueSecrets))
        {
            result = KeyValuePattern.Value.Replace(result, m =>
                m.Groups["key"].Value + m.Groups["sep"].Value + m.Groups["scheme"].Value + m.Groups["quote"].Value
                + SecretObfuscator.Obfuscate(m.Groups["value"].Value));
        }

        // Most specific first: an IBAN contains a RIB, and a NIR could pass for a card number.
        if (kinds.HasFlag(RedactionKinds.Iban))
            result = IbanPattern.Value.Replace(result, m => RedactIban(m.Value, requireChecksum));

        if (kinds.HasFlag(RedactionKinds.Rib))
        {
            result = RibPattern.Value.Replace(result, m =>
                !requireChecksum || m.Value.IsValidRibKey() ? m.Value.ObfuscateRib()! : m.Value);
        }

        if (kinds.HasFlag(RedactionKinds.FrenchNir))
        {
            result = NirPattern.Value.Replace(result, m =>
                !requireChecksum || m.Value.IsValidNir() ? m.Value.ObfuscateNir()! : m.Value);
        }

        if (kinds.HasFlag(RedactionKinds.PaymentCards))
        {
            result = CardPattern.Value.Replace(result, m =>
                !requireChecksum || (m.Value[0] is >= '2' and <= '6' && m.Value.IsValidLuhn()) ? m.Value.ObfuscateCardNumber()! : m.Value);
        }

        if (kinds.HasFlag(RedactionKinds.Emails))
        {
            // An address glued to a mask character is what is left visible of an address that was already masked ("coma*****wxyz@d.com"
            // leaves "wxyz@d.com"): masking it again would change the text on every pass.
            var current = result;
            result = EmailPattern.Value.Replace(current, m =>
                m.Index > 0 && current[m.Index - 1] == SecretObfuscator.DefaultMask
                    ? m.Value
                    : SecretObfuscator.ObfuscateEmail(m.Value, shouldSkip: options.ShouldSkipEmail)!);
        }

        return result;
    }

    /// <summary>
    /// The URI pattern stops only at white space, so one match can hold several URIs glued together
    /// (<c>a://x:y@h1/p,b://z:w@h2/q</c>). Each one is a segment starting at its scheme and is masked on its own: masking only the first
    /// would leave the password of the next ones in clear.
    /// </summary>
    private static string RedactUris(string token)
    {
        var starts = new List<int> { 0 };
        var separator = token.IndexOf("://", StringComparison.Ordinal);

        for (var next = token.IndexOf("://", separator + 3, StringComparison.Ordinal); next >= 0; next = token.IndexOf("://", next + 3, StringComparison.Ordinal))
        {
            // Walk back over the scheme characters (at most 19, never into the previous "://"), then forward to its first letter.
            var floor = Math.Max(separator + 3, next - 19);
            var start = next;
            while (start > floor && (IsAsciiLetter(token[start - 1]) || IsAsciiDigit(token[start - 1]) || token[start - 1] is '+' or '.' or '-'))
                start--;
            while (start < next && !IsAsciiLetter(token[start]))
                start++;

            if (next - start >= 2)
                starts.Add(start);

            separator = next;
        }

        if (starts.Count == 1)
            return SecretObfuscator.ObfuscateUri(token)!;

        var result = new System.Text.StringBuilder(token.Length);
        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1] : token.Length;
            result.Append(SecretObfuscator.ObfuscateUri(token[starts[i]..end]));
        }

        return result.ToString();
    }

    /// <summary>
    /// The pattern is greedy: an IBAN followed by an upper-case word ("... 0130 00 AND") is matched with that word.
    /// Shorter prefixes are tried until one is a valid IBAN; what follows it is left untouched.
    /// </summary>
    private static string RedactIban(string candidate, bool requireChecksum)
    {
        if (!requireChecksum)
            return FinancialObfuscator.HasIbanShape(WithoutSeparators(candidate)) ? candidate.ObfuscateIban()! : candidate;

        for (var end = candidate.Length; end >= 15; end--)
        {
            if (IsSeparator(candidate[end - 1]))
                continue;

            var piece = candidate[..end];
            if (piece.IsValidIban())
                return piece.ObfuscateIban() + candidate[end..];
        }

        return candidate;
    }
}
