using System.Buffers;

namespace SFerrand.Common.Internal;

/// <summary>
/// Shared plumbing for identifiers written with grouping separators (<c>4111 1111 1111 1111</c>, <c>30006-00001-...</c>):
/// ASCII character classes, separator handling and masking that keeps the layout.
/// </summary>
internal static class Layout
{
    public static readonly SearchValues<char> AsciiDigits = SearchValues.Create("0123456789");

    public static readonly SearchValues<char> AsciiLettersAndDigits =
        SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz");

    // Character classes are ASCII on purpose: char.IsDigit also accepts digits of other scripts, which are not banking digits.
    public static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    public static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>The grouping separators accepted inside an identifier.</summary>
    public static bool IsSeparator(char c) => c is ' ' or '-';

    public static string WithoutSeparators(string value)
        => string.Create(value.Count(c => !IsSeparator(c)), value, static (span, source) =>
        {
            var i = 0;
            foreach (var c in source)
            {
                if (!IsSeparator(c))
                    span[i++] = c;
            }
        });

    /// <summary>
    /// Masks every significant character except the first <paramref name="keepStart"/> and the last <paramref name="keepEnd"/>,
    /// counting separators as neither: they stay where they are and do not use up the budget.
    /// The caller guarantees that <paramref name="keepStart"/> + <paramref name="keepEnd"/> is smaller than the number of significant characters.
    /// </summary>
    public static string MaskKeepingSeparators(string value, int keepStart, int keepEnd, char mask)
    {
        var significant = value.Count(c => !IsSeparator(c));
        var result = new char[value.Length];
        var position = 0;

        for (var i = 0; i < value.Length; i++)
        {
            if (IsSeparator(value[i]))
            {
                result[i] = value[i];
                continue;
            }

            result[i] = position < keepStart || position >= significant - keepEnd ? value[i] : mask;
            position++;
        }

        return new string(result);
    }
}
