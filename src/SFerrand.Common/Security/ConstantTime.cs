using System.Security.Cryptography;
using System.Text;

namespace SFerrand.Common.Security;

/// <summary>
/// Comparisons whose duration does not depend on where the two values differ, to compare secrets (tokens, signatures, API keys)
/// without giving a timing side channel to whoever can measure response times.
/// </summary>
/// <remarks>
/// Only the <b>content</b> is protected: the comparison returns as soon as the <b>lengths</b> differ, so the length of the expected
/// value is not hidden. That is fine for fixed-size secrets (hashes, signatures, tokens of a known format). To hide the length,
/// compare hashes of both values instead. An ordinary <c>==</c> or <c>string.Equals</c> stops at the first different character and
/// must not be used for this purpose.
/// </remarks>
public static class ConstantTime
{
    /// <summary>Compares two strings, as UTF-8, in constant time with respect to their content.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>
    /// <see langword="true"/> when both are non-null and identical (ordinal comparison). <b>Two <see langword="null"/> values are not equal</b>:
    /// a missing secret must never match a missing credential.
    /// </returns>
    /// <example>
    /// <code>
    /// if (!ConstantTime.AreEqual(request.Headers["X-Token"], expectedToken))
    ///     return Unauthorized();
    /// </code>
    /// </example>
    public static bool AreEqual(string? left, string? right)
    {
        if (left is null || right is null)
            return false;

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        try
        {
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(leftBytes);
            CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    /// <summary>Compares two byte sequences in constant time with respect to their content.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when both have the same length and the same bytes. Two empty sequences are equal.</returns>
    public static bool AreEqual(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        => CryptographicOperations.FixedTimeEquals(left, right);
}
