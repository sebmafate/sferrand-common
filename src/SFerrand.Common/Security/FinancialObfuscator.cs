using System.Diagnostics.CodeAnalysis;
using static SFerrand.Common.Internal.Layout;

namespace SFerrand.Common.Security;

/// <summary>
/// Helpers that mask banking identifiers (payment card numbers, IBAN, French RIB) so they can be logged safely.
/// </summary>
/// <remarks>
/// <para>
/// Same contract as <see cref="SecretObfuscator"/>: fail-safe, deterministic, length-preserving, never throwing, and never
/// returning a value in clear. A value that does not have the shape of the identifier it is supposed to be
/// (wrong length, unexpected characters) is masked <b>as a whole</b> with <see cref="SecretObfuscator.Obfuscate"/>.
/// </para>
/// <para>
/// Spaces and hyphens, the usual grouping separators, are accepted anywhere and kept in place, so a masked value keeps the
/// layout of the original (<c>**** **** **** 1111</c>). The shape is checked, but not the checksum (Luhn for cards, mod 97 for
/// IBAN, RIB key): a mistyped number is just as sensitive as a valid one and must be masked too.
/// </para>
/// <para>
/// Each method has a <b>hard ceiling</b> on the characters it can reveal, whatever the arguments, so a careless call cannot
/// turn it into a no-op. The ceiling for cards follows the PCI DSS display rule (at most the first six and last four digits).
/// Masking is a logging aid, not a security boundary: do not store card numbers because they are masked, and never log a
/// card verification code (CVV/CVC) or a PIN at all — if one must be passed through a logging helper, <see cref="SecretObfuscator.Obfuscate"/>
/// hides a 3 or 4 digit code completely.
/// </para>
/// </remarks>
public static class FinancialObfuscator
{
    // Bounds on the number of significant characters (separators excluded) of each identifier.
    private const int CardMinDigits = 12;       // shortest PAN in use (some debit schemes)
    private const int CardMaxDigits = 19;       // ISO/IEC 7812 maximum
    private const int IbanMinLength = 15;       // shortest national IBAN (Norway)
    private const int IbanMaxLength = 34;       // ISO 13616 maximum
    private const int RibLength = 23;           // 5 bank + 5 branch + 11 account + 2 key

    private const int CardMaxLeading = 6;       // PCI DSS: the BIN...
    private const int CardMaxTrailing = 4;      // ...and the last four digits
    private const int IbanMaxVisible = 4;       // country + check digits / last four
    private const int RibMaxLeading = 10;       // bank code + branch code
    private const int RibMaxTrailing = 4;

    /// <summary>
    /// Masks a payment card number (PAN), keeping its grouping: <c>4111 1111 1111 1111</c> gives <c>**** **** **** 1111</c>.
    /// </summary>
    /// <param name="cardNumber">12 to 19 digits, optionally grouped with spaces or hyphens.</param>
    /// <param name="nbLeading">Digits kept at the start. Default: 0. Capped at 6 (the issuer identification number).</param>
    /// <param name="nbTrailing">Digits kept at the end. Default: 4. Capped at 4.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>
    /// The masked number; <paramref name="cardNumber"/> itself when <see langword="null"/> or empty;
    /// the value masked as a whole when it is not 12 to 19 digits.
    /// </returns>
    /// <remarks>
    /// The caps are applied silently: asking for 8 leading digits keeps 6. At least two digits are therefore always masked,
    /// even for the shortest number. Negative counts mean 0.
    /// </remarks>
    /// <example>
    /// <code>
    /// "4111 1111 1111 1111".ObfuscateCardNumber();                   // **** **** **** 1111
    /// "5555-5555-5555-4444".ObfuscateCardNumber(nbLeading: 6);       // 5555-55**-****-4444
    /// "378282246310005".ObfuscateCardNumber();                       // ***********0005
    /// "4111111111111111".ObfuscateCardNumber(8, 9);                  // 411111******1111  (capped at 6 + 4)
    /// </code>
    /// </example>
    [return: NotNullIfNotNull(nameof(cardNumber))]
    public static string? ObfuscateCardNumber(this string? cardNumber, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(cardNumber))
            return cardNumber;

        var compact = WithoutSeparators(cardNumber);
        if (compact.Length is < CardMinDigits or > CardMaxDigits || !compact.All(IsAsciiDigit))
            return SecretObfuscator.Obfuscate(cardNumber, mask: mask);

        return MaskKeepingSeparators(
            cardNumber,
            Math.Clamp(nbLeading ?? 0, 0, CardMaxLeading),
            Math.Clamp(nbTrailing ?? 4, 0, CardMaxTrailing),
            mask);
    }

    /// <summary>
    /// Masks an IBAN, keeping its grouping: <c>FR14 2004 1010 0505 0001 3M02 606</c> gives <c>FR** **** **** **** **** ***2 606</c>.
    /// </summary>
    /// <param name="iban">2 letters (country), 2 digits (check digits), then 11 to 30 letters or digits;
    /// optionally grouped with spaces or hyphens. Upper and lower case are accepted and preserved.</param>
    /// <param name="nbLeading">Characters kept at the start. Default: 2 (the country code). Capped at 4.</param>
    /// <param name="nbTrailing">Characters kept at the end. Default: 4. Capped at 4.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>
    /// The masked IBAN; <paramref name="iban"/> itself when <see langword="null"/> or empty;
    /// the value masked as a whole when it does not have the shape of an IBAN.
    /// </returns>
    /// <remarks>
    /// The country code is kept by default because it tells support which banking system is involved without identifying anyone.
    /// The check digits are masked by default: with the rest of the IBAN they are redundant, but they are
    /// computable from it, so showing them narrows a guess.
    /// </remarks>
    /// <example>
    /// <code>
    /// "FR76 3000 6000 0112 3456 7890 189".ObfuscateIban();   // FR** **** **** **** **** ***0 189
    /// "DE89370400440532013000".ObfuscateIban(nbLeading: 0);  // ******************3000
    /// </code>
    /// </example>
    [return: NotNullIfNotNull(nameof(iban))]
    public static string? ObfuscateIban(this string? iban, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(iban))
            return iban;

        var compact = WithoutSeparators(iban);
        if (!HasIbanShape(compact))
            return SecretObfuscator.Obfuscate(iban, mask: mask);

        return MaskKeepingSeparators(
            iban,
            Math.Clamp(nbLeading ?? 2, 0, IbanMaxVisible),
            Math.Clamp(nbTrailing ?? 4, 0, IbanMaxVisible),
            mask);
    }

    /// <summary>
    /// Masks a French bank identity statement number (RIB): bank code, branch code, account number and key,
    /// <c>30006 00001 12345678901 89</c> gives <c>30006 ***** *********** **</c>.
    /// </summary>
    /// <param name="rib">23 characters: 5 digits (bank), 5 digits (branch), 11 digits or letters (account), 2 digits (key);
    /// optionally grouped with spaces or hyphens.</param>
    /// <param name="nbLeading">Characters kept at the start. Default: 5 (the bank code, which only identifies the institution).
    /// Capped at 10 (bank and branch codes).</param>
    /// <param name="nbTrailing">Characters kept at the end. Default: 0. Capped at 4.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>
    /// The masked RIB; <paramref name="rib"/> itself when <see langword="null"/> or empty;
    /// the value masked as a whole when it does not have the shape of a RIB.
    /// </returns>
    /// <remarks>
    /// By default nothing of the account number is shown. The key is a checksum of the other 21 characters, so it is masked too.
    /// To tell two accounts apart in a log, ask for a few trailing characters explicitly (<c>nbTrailing: 3</c>).
    /// The IBAN of a French account contains its RIB: use <see cref="ObfuscateIban"/> for it.
    /// </remarks>
    /// <example>
    /// <code>
    /// "30006 00001 12345678901 89".ObfuscateRib();                 // 30006 ***** *********** **
    /// "30006000011234567890189".ObfuscateRib(nbTrailing: 3);       // 30006***************189
    /// </code>
    /// </example>
    [return: NotNullIfNotNull(nameof(rib))]
    public static string? ObfuscateRib(this string? rib, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(rib))
            return rib;

        var compact = WithoutSeparators(rib);
        if (!HasRibShape(compact))
            return SecretObfuscator.Obfuscate(rib, mask: mask);

        return MaskKeepingSeparators(
            rib,
            Math.Clamp(nbLeading ?? 5, 0, RibMaxLeading),
            Math.Clamp(nbTrailing ?? 0, 0, RibMaxTrailing),
            mask);
    }

    // Shapes -----------------------------------------------------------------------------------------------------------

    internal static bool HasIbanShape(string compact)
        => compact.Length is >= IbanMinLength and <= IbanMaxLength
           && IsAsciiLetter(compact[0]) && IsAsciiLetter(compact[1])
           && IsAsciiDigit(compact[2]) && IsAsciiDigit(compact[3])
           && compact.AsSpan(4).IndexOfAnyExcept(AsciiLettersAndDigits) < 0;

    internal static bool HasRibShape(string compact)
        => compact.Length == RibLength
           && compact.AsSpan(0, 10).IndexOfAnyExcept(AsciiDigits) < 0
           && compact.AsSpan(10, 11).IndexOfAnyExcept(AsciiLettersAndDigits) < 0
           && compact.AsSpan(21, 2).IndexOfAnyExcept(AsciiDigits) < 0;
}
