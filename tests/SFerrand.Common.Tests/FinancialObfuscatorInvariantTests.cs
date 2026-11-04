using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Property-style tests for <see cref="FinancialObfuscator"/>: whatever the digits, the grouping and the requested counts,
/// the ceiling on what is revealed, the layout and the fail-safe behaviour must hold. A fixed seed keeps every run reproducible.
/// </summary>
[TestClass]
public class FinancialObfuscatorInvariantTests
{
    private const int Iterations = 4000;
    private const string Digits = "0123456789";
    private const string Upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string UpperAndDigits = Upper + Digits;
    private const string Broad = "abcXYZ0123456789 -.:/_é٤*";

    private static string Pick(Random random, int length, string alphabet)
        => string.Create(length, (random, alphabet), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = state.alphabet[state.random.Next(state.alphabet.Length)];
        });

    /// <summary>Inserts a space or a hyphen after some characters, and sometimes at the ends, like real-world layouts.</summary>
    private static string WithRandomSeparators(Random random, string value)
    {
        var builder = new System.Text.StringBuilder();
        if (random.Next(8) == 0) builder.Append(' ');
        foreach (var c in value)
        {
            builder.Append(c);
            if (random.Next(4) == 0) builder.Append(random.Next(2) == 0 ? ' ' : '-');
        }
        if (random.Next(8) == 0) builder.Append(' ');
        return builder.ToString();
    }

    private static int? RandomCount(Random random) => random.Next(5) == 0 ? null : random.Next(-3, 40);

    private static bool IsSeparator(char c) => c is ' ' or '-';

    /// <summary>
    /// Checks the contract shared by the three methods on a valid-shaped input: same length, separators untouched, visible characters
    /// unchanged and confined to a prefix and a suffix of the significant characters within the ceilings, everything else masked.
    /// </summary>
    private static void AssertContract(string input, string masked, int maxLeading, int maxTrailing, int minMasked)
    {
        Assert.AreEqual(input.Length, masked.Length, $"length: '{input}'");

        var significant = new List<int>();
        for (var i = 0; i < input.Length; i++)
        {
            if (IsSeparator(input[i]))
                Assert.AreEqual(input[i], masked[i], $"separator moved: '{input}' -> '{masked}'");
            else
                significant.Add(i);
        }

        var isVisible = significant.Select(i => masked[i] != '*').ToList();
        for (var k = 0; k < significant.Count; k++)
        {
            if (isVisible[k])
                Assert.AreEqual(input[significant[k]], masked[significant[k]], $"changed character: '{input}' -> '{masked}'");
        }

        var firstMasked = isVisible.IndexOf(false);
        var lastMasked = isVisible.LastIndexOf(false);
        Assert.IsGreaterThanOrEqualTo(0, firstMasked, $"nothing masked: '{input}'");

        var maskedCount = isVisible.Count(v => !v);
        Assert.IsGreaterThanOrEqualTo(minMasked, maskedCount, $"too much revealed: '{input}' -> '{masked}'");
        Assert.IsLessThanOrEqualTo(maxLeading, firstMasked, $"leading ceiling: '{input}' -> '{masked}'");
        Assert.IsLessThanOrEqualTo(maxTrailing, significant.Count - 1 - lastMasked, $"trailing ceiling: '{input}' -> '{masked}'");
        Assert.AreEqual(lastMasked - firstMasked + 1, maskedCount, $"mask must be contiguous: '{input}' -> '{masked}'");
    }

    [TestMethod]
    public void ObfuscateCardNumber_RespectsThePciCeiling_ForAnyDigitsLayoutAndCounts()
    {
        var random = new Random(20260401);
        for (var i = 0; i < Iterations; i++)
        {
            var card = WithRandomSeparators(random, Pick(random, random.Next(12, 20), Digits));

            var masked = card.ObfuscateCardNumber(RandomCount(random), RandomCount(random));

            // 12 digits minus (6 + 4) leaves at least 2 masked, for every length and every pair of counts.
            AssertContract(card, masked, maxLeading: 6, maxTrailing: 4, minMasked: 2);
        }
    }

    [TestMethod]
    public void ObfuscateCardNumber_DefaultShowsExactlyTheLastFourDigits()
    {
        var random = new Random(3);
        for (var i = 0; i < Iterations; i++)
        {
            var digits = Pick(random, random.Next(12, 20), Digits);
            var card = WithRandomSeparators(random, digits);

            var masked = card.ObfuscateCardNumber();

            var shown = new string(masked.Where(c => c != '*' && !IsSeparator(c)).ToArray());
            Assert.AreEqual(digits[^4..], shown, card);
        }
    }

    [TestMethod]
    public void ObfuscateIban_RespectsTheCeiling_ForAnyShapeLayoutAndCounts()
    {
        var random = new Random(20260402);
        for (var i = 0; i < Iterations; i++)
        {
            var iban = Pick(random, 2, Upper) + Pick(random, 2, Digits) + Pick(random, random.Next(11, 31), UpperAndDigits);
            if (random.Next(3) == 0) iban = iban.ToLowerInvariant();
            iban = WithRandomSeparators(random, iban);

            var masked = iban.ObfuscateIban(RandomCount(random), RandomCount(random));

            // 15 characters minus (4 + 4) leaves at least 7 masked.
            AssertContract(iban, masked, maxLeading: 4, maxTrailing: 4, minMasked: 7);
        }
    }

    [TestMethod]
    public void ObfuscateRib_RespectsTheCeiling_ForAnyShapeLayoutAndCounts()
    {
        var random = new Random(20260403);
        for (var i = 0; i < Iterations; i++)
        {
            var rib = Pick(random, 10, Digits) + Pick(random, 11, UpperAndDigits) + Pick(random, 2, Digits);
            rib = WithRandomSeparators(random, rib);

            var masked = rib.ObfuscateRib(RandomCount(random), RandomCount(random));

            // 23 characters minus (10 + 4) leaves at least 9 masked: the account number is never fully shown.
            AssertContract(rib, masked, maxLeading: 10, maxTrailing: 4, minMasked: 9);
        }
    }

    [TestMethod]
    public void ObfuscateRib_DefaultShowsOnlyTheBankCode()
    {
        var random = new Random(5);
        for (var i = 0; i < Iterations; i++)
        {
            var digits = Pick(random, 10, Digits) + Pick(random, 11, UpperAndDigits) + Pick(random, 2, Digits);
            var rib = WithRandomSeparators(random, digits);

            var masked = rib.ObfuscateRib();

            var shown = new string(masked.Where(c => c != '*' && !IsSeparator(c)).ToArray());
            Assert.AreEqual(digits[..5], shown, rib);
        }
    }

    /// <summary>
    /// Fail-safe: on arbitrary text (most of it not a card, IBAN or RIB) the methods never throw, keep the length, and never
    /// return the input unchanged. The alphabet contains <c>*</c> only to prove it is not mistaken for a mask already applied.
    /// </summary>
    [TestMethod]
    public void AllMethods_OnArbitraryText_NeverThrowNeverReturnTheInputAndKeepTheLength()
    {
        var random = new Random(20260404);
        Func<string, string>[] methods =
        {
            v => v.ObfuscateCardNumber()!,
            v => v.ObfuscateIban()!,
            v => v.ObfuscateRib()!,
        };

        for (var i = 0; i < Iterations; i++)
        {
            var input = Pick(random, random.Next(1, 60), Broad);
            // "*" would make a value equal to its own masking; keep at least one other character.
            if (input.All(c => c == '*')) continue;

            foreach (var method in methods)
            {
                var masked = method(input);

                Assert.AreEqual(input.Length, masked.Length, input);
                Assert.AreNotEqual(input, masked, input);
            }
        }
    }

    /// <summary>
    /// A value of the right length and digits but the wrong kind (a card number is not an IBAN, a RIB is not a card)
    /// must go through the generic path of the method called, not be reinterpreted by another.
    /// </summary>
    [TestMethod]
    public void MethodsDoNotAcceptEachOthersIdentifiers()
    {
        const string rib = "30006000011234567890189";
        const string card = "4111111111111111";

        Assert.AreEqual(rib.Obfuscate(), rib.ObfuscateIban());
        Assert.AreEqual(rib.Obfuscate(), rib.ObfuscateCardNumber());
        Assert.AreEqual(card.Obfuscate(), card.ObfuscateRib());
        Assert.AreEqual(card.Obfuscate(), card.ObfuscateIban());
    }
}
