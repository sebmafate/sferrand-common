using static SFerrand.Common.Internal.Layout;

namespace SFerrand.Common.Validation;

/// <summary>
/// Check-digit validation for identifiers that carry one: payment cards (Luhn), IBAN (mod 97), French RIB, SIREN, SIRET,
/// social security number (NIR) and VAT number.
/// </summary>
/// <remarks>
/// <para>
/// A passing check means the number is <b>well-formed</b>, not that it exists or belongs to anyone: a random number passes a
/// Luhn check about one time in ten. Use these methods to reject typos and to cut false positives when scanning text
/// (see <see cref="Security.SecretRedactor"/>), never as proof of identity.
/// </para>
/// <para>
/// Every method accepts <see langword="null"/> (the answer is <see langword="false"/>), never throws, and ignores the usual
/// grouping separators (spaces and hyphens), so the value can be validated as it is displayed.
/// </para>
/// </remarks>
public static class Checksums
{
    /// <summary>
    /// Validates a number with the Luhn algorithm (payment cards, SIREN, SIRET, IMEI...).
    /// </summary>
    /// <param name="value">At least two ASCII digits, optionally grouped with spaces or hyphens.</param>
    /// <returns><see langword="true"/> when the digits satisfy the Luhn checksum.</returns>
    /// <example>
    /// <code>
    /// "4111 1111 1111 1111".IsValidLuhn();   // true
    /// "4111 1111 1111 1112".IsValidLuhn();   // false
    /// </code>
    /// </example>
    public static bool IsValidLuhn(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var digits = WithoutSeparators(value);
        return digits.Length >= 2 && digits.AsSpan().IndexOfAnyExcept(AsciiDigits) < 0 && LuhnSum(digits) % 10 == 0;
    }

    /// <summary>
    /// Validates an IBAN: shape (2 letters, 2 check digits, 11 to 30 letters or digits) and the ISO 7064 mod 97-10 checksum.
    /// </summary>
    /// <param name="value">The IBAN, in upper or lower case, optionally grouped with spaces or hyphens.</param>
    /// <remarks>The length required by each country is <b>not</b> checked, only the generic 15 to 34 range.</remarks>
    /// <example>
    /// <code>
    /// "FR14 2004 1010 0505 0001 3M02 606".IsValidIban();   // true
    /// "FR14 2004 1010 0505 0001 3M02 607".IsValidIban();   // false
    /// </code>
    /// </example>
    public static bool IsValidIban(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var iban = WithoutSeparators(value);
        if (iban.Length is < 15 or > 34
            || !IsAsciiLetter(iban[0]) || !IsAsciiLetter(iban[1]) || !IsAsciiDigit(iban[2]) || !IsAsciiDigit(iban[3])
            || iban.AsSpan(4).IndexOfAnyExcept(AsciiLettersAndDigits) >= 0)
        {
            return false;
        }

        // Move the first four characters to the end, read letters as 10..35, and compute the remainder digit group by digit group
        // so no big integer is needed.
        var remainder = 0;
        for (var i = 0; i < iban.Length; i++)
        {
            var c = iban[(i + 4) % iban.Length];
            remainder = IsAsciiDigit(c)
                ? (remainder * 10 + (c - '0')) % 97
                : (remainder * 100 + (char.ToUpperInvariant(c) - 'A' + 10)) % 97;
        }

        return remainder == 1;
    }

    /// <summary>
    /// Validates the key of a French RIB (bank code, branch code, account number, key).
    /// </summary>
    /// <param name="value">23 characters: 5 digits, 5 digits, 11 digits or letters, 2 digits; optionally grouped.</param>
    /// <remarks>
    /// Letters of the account number are converted to digits as the banking standard prescribes (A-I to 1-9, J-R to 1-9, S-Z to 2-9),
    /// then the key must equal <c>97 - (89 × bank + 15 × branch + 3 × account) mod 97</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// "20041 01005 0500013M026 06".IsValidRibKey();   // true
    /// </code>
    /// </example>
    public static bool IsValidRibKey(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var rib = WithoutSeparators(value);
        if (rib.Length != 23
            || rib.AsSpan(0, 10).IndexOfAnyExcept(AsciiDigits) >= 0
            || rib.AsSpan(10, 11).IndexOfAnyExcept(AsciiLettersAndDigits) >= 0
            || rib.AsSpan(21, 2).IndexOfAnyExcept(AsciiDigits) >= 0)
        {
            return false;
        }

        long bank = long.Parse(rib.AsSpan(0, 5));
        long branch = long.Parse(rib.AsSpan(5, 5));
        long account = 0;
        foreach (var c in rib.AsSpan(10, 11))
            account = account * 10 + RibLetterValue(c);

        var key = 97 - (89 * bank + 15 * branch + 3 * account) % 97;
        return key == long.Parse(rib.AsSpan(21, 2));
    }

    /// <summary>
    /// Validates a SIREN (9 digits, Luhn).
    /// </summary>
    /// <param name="value">The SIREN, optionally grouped (<c>732 829 320</c>).</param>
    public static bool IsValidSiren(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var siren = WithoutSeparators(value);
        return siren.Length == 9 && siren.AsSpan().IndexOfAnyExcept(AsciiDigits) < 0 && LuhnSum(siren) % 10 == 0;
    }

    /// <summary>
    /// Validates a SIRET (14 digits: SIREN + 5-digit establishment number, Luhn).
    /// </summary>
    /// <param name="value">The SIRET, optionally grouped.</param>
    /// <remarks>
    /// The establishments of La Poste (SIREN <c>356 000 000</c>) follow a different rule: the sum of the digits must be a multiple of 5.
    /// For them the Luhn check is not applied.
    /// </remarks>
    public static bool IsValidSiret(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var siret = WithoutSeparators(value);
        if (siret.Length != 14 || siret.AsSpan().IndexOfAnyExcept(AsciiDigits) >= 0)
            return false;

        return siret.StartsWith("356000000", StringComparison.Ordinal)
            ? siret.Sum(c => c - '0') % 5 == 0
            : LuhnSum(siret) % 10 == 0;
    }

    /// <summary>
    /// Validates a French social security number (NIR): 13 characters plus a 2-digit key, <c>key = 97 - (first 13 mod 97)</c>.
    /// </summary>
    /// <param name="value">The 15 characters, optionally grouped (<c>1 84 12 76 451 089 46</c>).
    /// A Corsican department is written <c>2A</c> or <c>2B</c>.</param>
    /// <remarks>
    /// For the Corsican departments the letters are replaced by 19 (<c>2A</c>) and 18 (<c>2B</c>) before the computation.
    /// Only the checksum is verified, not the plausibility of the month or of the commune.
    /// </remarks>
    public static bool IsValidNir(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var nir = WithoutSeparators(value);
        if (nir.Length != 15 || !HasNirShape(nir))
            return false;

        var number = nir[..13].ToUpperInvariant().Replace("2A", "19", StringComparison.Ordinal).Replace("2B", "18", StringComparison.Ordinal);
        var key = 97 - long.Parse(number) % 97;
        return key == long.Parse(nir.AsSpan(13, 2));
    }

    /// <summary>
    /// Validates a French VAT number: <c>FR</c>, a 2-digit key and a SIREN, <c>key = (12 + 3 × (SIREN mod 97)) mod 97</c>.
    /// </summary>
    /// <param name="value">The 13 characters, optionally grouped (<c>FR 44 732 829 320</c>).</param>
    /// <remarks>
    /// Numbers whose key contains a letter (a newer format) cannot be checked this way and are reported as invalid.
    /// The embedded SIREN must itself be valid.
    /// </remarks>
    public static bool IsValidFrenchVatNumber(this string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var vat = WithoutSeparators(value);
        if (vat.Length != 13
            || !string.Equals(vat[..2], "FR", StringComparison.OrdinalIgnoreCase)
            || vat.AsSpan(2).IndexOfAnyExcept(AsciiDigits) >= 0
            || !IsValidSiren(vat[4..]))
        {
            return false;
        }

        var key = (12 + 3 * (long.Parse(vat.AsSpan(4)) % 97)) % 97;
        return key == long.Parse(vat.AsSpan(2, 2));
    }

    /// <summary>The shape of a NIR (without grouping): 5 digits, a department (2 digits or 2A/2B), 8 digits.</summary>
    internal static bool HasNirShape(string nir)
        => nir.Length == 15
           && nir.AsSpan(0, 5).IndexOfAnyExcept(AsciiDigits) < 0
           && (nir.AsSpan(5, 2).IndexOfAnyExcept(AsciiDigits) < 0
               || (nir[5] == '2' && nir[6] is 'A' or 'B' or 'a' or 'b'))
           && nir.AsSpan(7, 8).IndexOfAnyExcept(AsciiDigits) < 0;

    // Sum of the digits after doubling every second one from the right (and folding the result back to one digit).
    private static int LuhnSum(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                    d -= 9;
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum;
    }

    private static int RibLetterValue(char c)
    {
        if (IsAsciiDigit(c))
            return c - '0';

        var upper = char.ToUpperInvariant(c);
        return upper switch
        {
            <= 'I' => upper - 'A' + 1,   // A-I -> 1-9
            <= 'R' => upper - 'J' + 1,   // J-R -> 1-9
            _ => upper - 'S' + 2,        // S-Z -> 2-9
        };
    }
}
