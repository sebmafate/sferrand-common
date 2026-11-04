using System.Numerics;
using System.Text;
using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Property-style tests for <see cref="SecretRedactor"/> on random text: it never throws, keeps the length, is idempotent, leaves secret-free
/// text untouched, and masks a secret planted anywhere in noise. Valid numbers are produced by generators written <b>in this file</b>
/// (their own Luhn and mod 97 computations), so the redactor is not checked against its own validators. A fixed seed keeps every run reproducible.
/// </summary>
[TestClass]
public class SecretRedactorInvariantTests
{
    private const int Iterations = 2500;
    private const string Words = "abcdefghijklmnopqrstuvwxyz";

    private static string Noise(Random random)
    {
        var parts = Enumerable.Range(0, random.Next(0, 6)).Select(_ => string.Concat(Enumerable.Range(0, random.Next(1, 9)).Select(_ => Words[random.Next(Words.Length)])));
        return string.Join(' ', parts);
    }

    private static string Around(Random random, string secret) => $"{Noise(random)} {secret} {Noise(random)}".Trim();

    // Independent generators ---------------------------------------------------------------------------------------

    private static string RandomLuhnNumber(Random random, int length)
    {
        var digits = new int[length];
        digits[0] = random.Next(2, 7);
        for (var i = 1; i < length - 1; i++) digits[i] = random.Next(10);

        // Find the check digit by brute force with a straightforward Luhn implementation.
        for (var check = 0; check < 10; check++)
        {
            digits[length - 1] = check;
            var sum = 0;
            for (var i = 0; i < length; i++)
            {
                var d = digits[length - 1 - i];
                if (i % 2 == 1) { d *= 2; if (d > 9) d -= 9; }
                sum += d;
            }

            if (sum % 10 == 0) return string.Concat(digits);
        }

        throw new InvalidOperationException("unreachable: one check digit always satisfies Luhn");
    }

    private static string RandomIban(Random random)
    {
        var country = $"{(char)('A' + random.Next(26))}{(char)('A' + random.Next(26))}";
        var bban = string.Concat(Enumerable.Range(0, random.Next(11, 31)).Select(_ => "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"[random.Next(36)]));

        // check digits = 98 - (BBAN + country + "00" as digits) mod 97
        var numeric = string.Concat((bban + country + "00").Select(c => char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));
        var check = 98 - (int)(BigInteger.Parse(numeric) % 97);
        return $"{country}{check:D2}{bban}";
    }

    private static string Group(string value, int size) => string.Join(' ', Enumerable.Range(0, (value.Length + size - 1) / size).Select(i => value.Substring(i * size, Math.Min(size, value.Length - i * size))));

    // Properties ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TextWithoutAnythingSensitive_IsReturnedAsTheSameInstance()
    {
        var random = new Random(20260601);
        for (var i = 0; i < Iterations; i++)
        {
            var text = Noise(random) + ".";

            Assert.AreSame(text, text.Redact(), text);
        }
    }

    [TestMethod]
    public void OnArbitraryText_TheRedactorNeverThrows_KeepsTheLength_AndIsIdempotent()
    {
        var random = new Random(20260602);
        string[] fragments =
        {
            "password=", "token:", "Bearer ", "Basic ", "http://", "https://u:p@h/", "eyJ", "ghp_", "AKIA", "xoxb-", "FR76 ", "DE89", "@", ".com",
            "4111 1111 1111 1111", "1 84 12 76 451 089 46", " ", "-", "=", "\"", "'", ",", ";", "&", "0123456789", "abcdefghijklmnopqrstuvwxyz", "é", "\n",
        };

        for (var i = 0; i < Iterations; i++)
        {
            var text = string.Concat(Enumerable.Range(0, random.Next(0, 25)).Select(_ => fragments[random.Next(fragments.Length)]));

            var once = text.Redact()!;

            Assert.AreEqual(text.Length, once.Length, text);
            Assert.AreEqual(once, once.Redact(), $"not idempotent: '{text}' -> '{once}'");
        }
    }

    [TestMethod]
    public void ACardPlantedInNoise_IsMasked_AndKeepsItsLastFourDigits()
    {
        var random = new Random(20260603);
        for (var i = 0; i < Iterations; i++)
        {
            var digits = RandomLuhnNumber(random, random.Next(13, 20));
            var card = random.Next(3) switch { 0 => digits, 1 => Group(digits, 4), _ => Group(digits, 4).Replace(' ', '-') };
            var text = Around(random, card);

            var redacted = text.Redact()!;

            Assert.AreEqual(text.Length, redacted.Length, text);
            Assert.DoesNotContain(card, redacted, text);
            Assert.DoesNotContain(digits[..^4][^6..], redacted.Replace(" ", "").Replace("-", ""), $"middle digits survived: '{text}' -> '{redacted}'");
            Assert.AreEqual(digits[^4..], new string(redacted.Where(char.IsDigit).ToArray())[^4..], text);
        }
    }

    [TestMethod]
    public void AnIbanPlantedInNoise_IsMasked_EvenWhenFollowedByAnUpperCaseWord()
    {
        var random = new Random(20260604);
        for (var i = 0; i < Iterations; i++)
        {
            var iban = RandomIban(random);
            var written = random.Next(2) == 0 ? iban : Group(iban, 4);
            var suffix = random.Next(3) == 0 ? " AND" : string.Empty;
            var text = $"{Noise(random)} {written}{suffix} {Noise(random)}".Trim();

            var redacted = text.Redact()!;

            Assert.AreEqual(text.Length, redacted.Length, text);
            Assert.DoesNotContain(iban[4..^4], redacted.Replace(" ", ""), $"IBAN body survived: '{text}' -> '{redacted}'");
            if (suffix.Length > 0)
                Assert.IsTrue(redacted.Contains("AND", StringComparison.Ordinal), $"the word after the IBAN was damaged: '{text}' -> '{redacted}'");
        }
    }

    [TestMethod]
    public void ANamedSecretPlantedInNoise_NeverSurvives()
    {
        var random = new Random(20260605);
        string[] keys = { "password", "Password", "client_secret", "access_token", "x-api-key", "pwd", "api_key" };
        string[] separators = { "=", ": ", "=\"", ":'" };
        const string valueAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";   // upper case: cannot be found by chance in the lower-case noise or key names

        for (var i = 0; i < Iterations; i++)
        {
            var value = string.Concat(Enumerable.Range(0, random.Next(8, 31)).Select(_ => valueAlphabet[random.Next(valueAlphabet.Length)]));
            var text = Around(random, $"{keys[random.Next(keys.Length)]}{separators[random.Next(separators.Length)]}{value}");

            var redacted = text.Redact()!;

            Assert.AreEqual(text.Length, redacted.Length, text);
            Assert.DoesNotContain(value, redacted, text);
        }
    }

    [TestMethod]
    public void AnEmailPlantedInNoise_HasItsLocalPartMasked_AndKeepsItsDomain()
    {
        var random = new Random(20260606);
        for (var i = 0; i < Iterations; i++)
        {
            var local = string.Concat(Enumerable.Range(0, random.Next(5, 15)).Select(_ => "ABCDEFGHJKLMNPQRSTUVWXYZ"[random.Next(24)]));
            var email = $"{local}@example.com";
            var text = Around(random, email);

            var redacted = text.Redact()!;

            Assert.AreEqual(text.Length, redacted.Length, text);
            Assert.DoesNotContain(local, redacted, text);
            Assert.IsTrue(redacted.Contains("@example.com", StringComparison.Ordinal), redacted);
        }
    }

    [TestMethod]
    public void RandomLongDigitStrings_ThatFailTheChecksum_AreNotCardsOrIbans()
    {
        var random = new Random(20260607);
        var spared = 0;
        for (var i = 0; i < Iterations; i++)
        {
            var length = random.Next(13, 20);
            var number = RandomLuhnNumber(random, length);
            // Change the last digit: the Luhn check can no longer hold.
            var broken = number[..^1] + (char)('0' + (number[^1] - '0' + 1 + random.Next(9)) % 10);
            var text = Around(random, broken);

            Assert.AreEqual(text, text.Redact(), text);
            spared++;
        }

        Assert.AreEqual(Iterations, spared);
    }
}
