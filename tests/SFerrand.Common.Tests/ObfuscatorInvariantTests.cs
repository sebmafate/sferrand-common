using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Property-style tests: the guarantees that matter for a masking library must hold for any input,
/// not only for the hand-picked examples. A fixed seed keeps every run reproducible.
/// </summary>
[TestClass]
public class ObfuscatorInvariantTests
{
    private const int Iterations = 4000;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_./:+é";

    private static string RandomText(Random random, int minLength, int maxLength, string alphabet = Alphabet)
    {
        var length = random.Next(minLength, maxLength + 1);
        return string.Create(length, (random, alphabet), static (span, state) =>
        {
            for (var i = 0; i < span.Length; i++)
                span[i] = state.alphabet[state.random.Next(state.alphabet.Length)];
        });
    }

    private static int? RandomCount(Random random) => random.Next(5) == 0 ? null : random.Next(-3, 40);

    [TestMethod]
    public void Obfuscate_PreservesLength_AndAlwaysMasksAtLeastOneCharacter()
    {
        var random = new Random(20260101);
        for (var i = 0; i < Iterations; i++)
        {
            var input = RandomText(random, 1, 80);
            var leading = RandomCount(random);
            var trailing = RandomCount(random);

            var masked = input.Obfuscate(leading, trailing);

            Assert.AreEqual(input.Length, masked.Length, $"length: '{input}' {leading}/{trailing}");
            Assert.AreNotEqual(input, masked, $"fully revealed: '{input}' {leading}/{trailing}");
            Assert.Contains("*", masked, $"nothing masked: '{input}' {leading}/{trailing}");
        }
    }

    [TestMethod]
    public void Obfuscate_OnlyKeepsAPrefixAndASuffixOfTheOriginal()
    {
        var random = new Random(7);
        for (var i = 0; i < Iterations; i++)
        {
            var input = RandomText(random, 1, 80);
            var masked = input.Obfuscate(RandomCount(random), RandomCount(random));

            var firstMask = masked.IndexOf('*');
            var lastMask = masked.LastIndexOf('*');

            Assert.AreEqual(input[..firstMask], masked[..firstMask]);
            Assert.AreEqual(input[(lastMask + 1)..], masked[(lastMask + 1)..]);
            Assert.IsTrue(masked[firstMask..(lastMask + 1)].All(c => c == '*'), $"mask must be contiguous: '{masked}'");
        }
    }

    [TestMethod]
    public void Obfuscate_AdaptiveDefaults_NeverRevealMoreThanTwiceTheDocumentedCount()
    {
        var random = new Random(11);
        for (var i = 0; i < Iterations; i++)
        {
            var input = RandomText(random, 1, 200);

            var visible = input.Obfuscate().Count(c => c != '*');

            Assert.IsLessThanOrEqualTo(2 * SecretObfuscator.GetNumberOfPreservedChars(input.Length), visible);
        }
    }

    [TestMethod]
    public void ObfuscateUri_NeverLeaksThePassword_AndPreservesLength()
    {
        var random = new Random(13);
        for (var i = 0; i < Iterations; i++)
        {
            var user = RandomText(random, 1, 12, "abcdefghij");
            var password = RandomText(random, 5, 40, "abcdefghijklmnopqrstuvwxyz0123456789@:!");
            var uri = $"https://{user}:{password}@db.example.com:5432/shop?x=1";

            var masked = uri.ObfuscateUri();

            Assert.AreEqual(uri.Length, masked.Length, uri);
            Assert.DoesNotContain(password, masked, uri);
            Assert.StartsWith($"https://{user}:", masked);
            Assert.EndsWith("@db.example.com:5432/shop?x=1", masked);
        }
    }

    [TestMethod]
    public void ObfuscateConnectionString_NeverLeaksTheSecret_AndKeepsTheRest()
    {
        var random = new Random(17);
        for (var i = 0; i < Iterations; i++)
        {
            var secret = RandomText(random, 5, 60, "abcdefghijklmnopqrstuvwxyz0123456789+/=");
            var input = $"Server=db;Pwd={secret};Timeout=30";

            var masked = input.ObfuscateConnectionString("pwd");

            Assert.AreEqual(input.Length, masked.Length);
            Assert.DoesNotContain(secret, masked, input);
            Assert.StartsWith("Server=db;Pwd=", masked);
            Assert.EndsWith(";Timeout=30", masked);
        }
    }

    [TestMethod]
    public void ObfuscateEmail_NeverLeaksTheWholeLocalPart_AndKeepsTheDomain()
    {
        var random = new Random(19);
        for (var i = 0; i < Iterations; i++)
        {
            var local = RandomText(random, 1, 24, "abcdefghijklmnopqrstuvwxyz0123456789.-_");
            var email = $"{local}@Example.com";

            var masked = email.ObfuscateEmail();

            Assert.AreEqual(email.Length, masked.Length, email);
            Assert.EndsWith("@Example.com", masked);
            if (local.Any(char.IsLetterOrDigit))
                Assert.AreNotEqual(email, masked, email);
        }
    }

    [TestMethod]
    public void ObfuscateGuid_KeepsOnlyTheVersionGroupAndShape()
    {
        var random = new Random(23);
        for (var i = 0; i < 1000; i++)
        {
            var guid = Guid.NewGuid();
            var masked = guid.ObfuscateGuid();
            var original = guid.ToString("D").Split('-');
            var parts = masked.Split('-');

            Assert.AreEqual(36, masked.Length);
            Assert.AreEqual(original[2], parts[2]);
            Assert.AreNotEqual(guid.ToString("D"), masked);
        }
    }

    [TestMethod]
    public void ObfuscateToken_NeverRevealsTheBodyOfAKnownPrefixedToken()
    {
        var random = new Random(29);
        foreach (var prefix in SecretObfuscator.KnownTokenPrefixes)
        {
            for (var i = 0; i < 500; i++)
            {
                var body = RandomText(random, 6, 60, "abcdefghijklmnopqrstuvwxyz0123456789");
                var token = prefix + body;

                var masked = token.ObfuscateToken();

                Assert.AreEqual(token.Length, masked.Length);
                Assert.StartsWith(prefix, masked);
                Assert.AreNotEqual(token, masked);
                Assert.DoesNotContain(body, masked);
            }
        }
    }
}
