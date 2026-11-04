using System.Diagnostics.CodeAnalysis;
using SFerrand.Common.Validation;
using static SFerrand.Common.Internal.Layout;

namespace SFerrand.Common.Security;

/// <summary>
/// Helpers that mask French personal and company identifiers (social security number, SIREN, SIRET, VAT number) so they can be logged safely.
/// </summary>
/// <remarks>
/// <para>
/// Same contract as <see cref="FinancialObfuscator"/>: fail-safe, deterministic, length-preserving, layout-preserving (spaces and hyphens
/// stay in place and do not count), never throwing, with a hard ceiling on what can be revealed. A value of the wrong shape is masked
/// <b>as a whole</b> with <see cref="SecretObfuscator.Obfuscate"/>. The check digits are not verified (see <see cref="Checksums"/>).
/// </para>
/// <para>
/// The social security number (NIR) is the sensitive one: it encodes sex, year and month of birth, department and commune. Its
/// ceiling therefore allows at most the first character (sex) and the last two (the key) to be shown, and nothing is shown by default.
/// SIREN and SIRET are public registry data, but for a sole trader they identify a person: they are masked with a lighter default.
/// </para>
/// </remarks>
public static class FrenchIdentifierObfuscator
{
    /// <summary>
    /// Masks a social security number (NIR): <c>1 84 12 76 451 089 46</c> gives <c>* ** ** ** *** *** **</c>.
    /// </summary>
    /// <param name="nir">15 characters (a Corsican department is <c>2A</c> or <c>2B</c>), optionally grouped with spaces or hyphens.</param>
    /// <param name="nbLeading">Characters kept at the start. Default: 0. Capped at 1 (the sex digit).</param>
    /// <param name="nbTrailing">Characters kept at the end. Default: 0. Capped at 2 (the key).</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>
    /// The masked number; <paramref name="nir"/> itself when <see langword="null"/> or empty;
    /// the value masked as a whole when it does not have the shape of a NIR.
    /// </returns>
    /// <remarks>Year, month, department and commune can never be shown: the ceiling stops before them.</remarks>
    /// <example>
    /// <code>
    /// "1 84 12 76 451 089 46".ObfuscateNir();                  // * ** ** ** *** *** **
    /// "184127645108946".ObfuscateNir(nbTrailing: 2);           // *************46
    /// </code>
    /// </example>
    [return: NotNullIfNotNull(nameof(nir))]
    public static string? ObfuscateNir(this string? nir, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(nir))
            return nir;

        if (!Checksums.HasNirShape(WithoutSeparators(nir)))
            return SecretObfuscator.Obfuscate(nir, mask: mask);

        return MaskKeepingSeparators(nir, Math.Clamp(nbLeading ?? 0, 0, 1), Math.Clamp(nbTrailing ?? 0, 0, 2), mask);
    }

    /// <summary>
    /// Masks a SIREN: <c>732 829 320</c> gives <c>*** *** 320</c>.
    /// </summary>
    /// <param name="siren">9 digits, optionally grouped with spaces or hyphens.</param>
    /// <param name="nbLeading">Digits kept at the start. Default: 0. Capped at 3.</param>
    /// <param name="nbTrailing">Digits kept at the end. Default: 3. Capped at 3.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>The masked SIREN; the value itself when <see langword="null"/> or empty; the value masked as a whole when it is not 9 digits.</returns>
    [return: NotNullIfNotNull(nameof(siren))]
    public static string? ObfuscateSiren(this string? siren, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(siren))
            return siren;

        var compact = WithoutSeparators(siren);
        if (compact.Length != 9 || compact.AsSpan().IndexOfAnyExcept(AsciiDigits) >= 0)
            return SecretObfuscator.Obfuscate(siren, mask: mask);

        return MaskKeepingSeparators(siren, Math.Clamp(nbLeading ?? 0, 0, 3), Math.Clamp(nbTrailing ?? 3, 0, 3), mask);
    }

    /// <summary>
    /// Masks a SIRET: <c>732 829 320 00074</c> gives <c>*** *** *** *0074</c>.
    /// </summary>
    /// <param name="siret">14 digits, optionally grouped with spaces or hyphens.</param>
    /// <param name="nbLeading">Digits kept at the start. Default: 0. Capped at 4.</param>
    /// <param name="nbTrailing">Digits kept at the end. Default: 4. Capped at 5 (the establishment number).</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>The masked SIRET; the value itself when <see langword="null"/> or empty; the value masked as a whole when it is not 14 digits.</returns>
    [return: NotNullIfNotNull(nameof(siret))]
    public static string? ObfuscateSiret(this string? siret, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(siret))
            return siret;

        var compact = WithoutSeparators(siret);
        if (compact.Length != 14 || compact.AsSpan().IndexOfAnyExcept(AsciiDigits) >= 0)
            return SecretObfuscator.Obfuscate(siret, mask: mask);

        return MaskKeepingSeparators(siret, Math.Clamp(nbLeading ?? 0, 0, 4), Math.Clamp(nbTrailing ?? 4, 0, 5), mask);
    }

    /// <summary>
    /// Masks a French VAT number: <c>FR 44 732 829 320</c> gives <c>FR ** *** *** 320</c>.
    /// </summary>
    /// <param name="vatNumber">13 characters: <c>FR</c>, a 2-character key, a 9-digit SIREN; optionally grouped.</param>
    /// <param name="nbLeading">Characters kept at the start. Default: 2 (the country code). Capped at 4.</param>
    /// <param name="nbTrailing">Characters kept at the end. Default: 3. Capped at 4.</param>
    /// <param name="mask">The masking character.</param>
    /// <returns>The masked number; the value itself when <see langword="null"/> or empty; the value masked as a whole when it does not have the shape of a French VAT number.</returns>
    [return: NotNullIfNotNull(nameof(vatNumber))]
    public static string? ObfuscateFrenchVatNumber(this string? vatNumber, int? nbLeading = null, int? nbTrailing = null, char mask = SecretObfuscator.DefaultMask)
    {
        if (string.IsNullOrEmpty(vatNumber))
            return vatNumber;

        var compact = WithoutSeparators(vatNumber);
        if (compact.Length != 13
            || !IsAsciiLetter(compact[0]) || char.ToUpperInvariant(compact[0]) != 'F'
            || char.ToUpperInvariant(compact[1]) != 'R'
            || compact.AsSpan(2, 2).IndexOfAnyExcept(AsciiLettersAndDigits) >= 0
            || compact.AsSpan(4).IndexOfAnyExcept(AsciiDigits) >= 0)
        {
            return SecretObfuscator.Obfuscate(vatNumber, mask: mask);
        }

        return MaskKeepingSeparators(vatNumber, Math.Clamp(nbLeading ?? 2, 0, 4), Math.Clamp(nbTrailing ?? 3, 0, 4), mask);
    }
}
