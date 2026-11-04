using System.Diagnostics;
using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Example-based tests of <see cref="SecretRedactor"/>: one group per category of sensitive data, the false positives it must <i>not</i>
/// produce (timestamps, dates, order numbers), the options, and the behaviour on hostile input. Properties that must hold on random text
/// live in <see cref="SecretRedactorInvariantTests"/>. Tokens and numbers are fakes or public examples.
/// </summary>
[TestClass]
public class SecretRedactorTests
{
    private static string Stars(int count) => new('*', count);

    #region Credentials in URIs and headers

    [TestMethod]
    public void UriCredentials_AreMasked_InsideASentence()
        => Assert.AreEqual(
            "Connecting to postgres://app:s3*******ss@db/shop now",
            "Connecting to postgres://app:s3cr3t-pass@db/shop now".Redact());

    /// <summary>
    /// Regression found by the random tests, and a real leak: a URI token ends at white space only, so URIs glued by a comma or a semicolon
    /// form a single token. Masking only the first one left the password of the others in clear.
    /// </summary>
    [TestMethod]
    [DataRow("urls=http://a:pw123456@h1/x,http://b:secret99@h2/y", "urls=http://a:p******6@h1/x,http://b:s******9@h2/y")]
    [DataRow("ftp://u:abcdefgh@h;ws://u:ijklmnop@k;smtp://u:qrstuvwx@m", "ftp://u:a******h@h;ws://u:i******p@k;smtp://u:q******x@m")]
    [DataRow("AKIAhttps://u:password1@h/-x", "AKIAhttps://u:pa*****d1@h/-x")]   // glued to the previous word: the scheme is "AKIAhttps"
    public void SeveralUrisInOneToken_AreAllMasked(string text, string expected)
        => Assert.AreEqual(expected, text.Redact());

    /// <summary>The scheme of a glued URI starts at its first letter: a digit just before it belongs to the previous URI, not to the scheme.</summary>
    [TestMethod]
    public void ASchemeThatFollowsADigit_StartsAtItsFirstLetter()
        => Assert.AreEqual("http://h1,9ws://u:pw******78@k", "http://h1,9ws://u:pw12345678@k".Redact());

    /// <summary>Characters that are allowed in a scheme but cannot start one (<c>12+</c>) do not make a URI either.</summary>
    [TestMethod]
    public void ASchemeWithoutAnyLetter_IsNotAUri()
        => Assert.AreEqual("http://h,12+://u:pw12345678@k", "http://h,12+://u:pw12345678@k".Redact());

    /// <summary>A one-letter "scheme" (a Windows drive such as <c>C://</c>) is not a URI: the pattern requires at least two characters.</summary>
    [TestMethod]
    public void AOneLetterScheme_IsNotAUri()
        => Assert.AreEqual("http://h,a://u:pw12345678@k", "http://h,a://u:pw12345678@k".Redact());

    [TestMethod]
    public void SeveralUrisInOneToken_OnlyThoseWithCredentialsChange()
        => Assert.AreEqual(
            "http://example.com/a,https://u:pw******78@h/b,ftp://files.test/c",
            "http://example.com/a,https://u:pw12345678@h/b,ftp://files.test/c".Redact());

    [TestMethod]
    public void UriWithoutCredentials_IsLeftAlone()
        => Assert.AreEqual("GET https://api.example.com/v1/items?page=2 -> 200", "GET https://api.example.com/v1/items?page=2 -> 200".Redact());

    [TestMethod]
    [DataRow("Authorization: Bearer abcdefghijklmnopqrstuv failed", "Authorization: Bearer abcd**************stuv failed")]
    [DataRow("sent bearer abcdefghijklmnopqrstuv", "sent bearer abcd**************stuv")]
    [DataRow("Authorization: Basic dXNlcjpwYXNzd29yZA== ok", "Authorization: Basic dXNl************ZA== ok")]
    public void AuthorizationCredentials_AreMasked_AndTheSchemeKept(string text, string expected)
        => Assert.AreEqual(expected, text.Redact());

    /// <summary>A credential shorter than 16 characters after "Bearer" is far more likely a word in a sentence than a token.</summary>
    [TestMethod]
    public void ShortWordsAfterBearer_AreNotCredentials()
        => Assert.AreEqual("Bearer authentication is required", "Bearer authentication is required".Redact());

    [TestMethod]
    public void Jwt_IsMasked()
        => Assert.AreEqual(
            "tok eyJh" + Stars(52) + "_123 end",
            "tok eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc-DEF_123 end".Redact());

    #endregion

    #region Tokens and named secrets

    [TestMethod]
    [DataRow("pushed with ghp_abcdefghijklmnopqrstuvwxyz1 ok", "pushed with ghp_abcd*******************xyz1 ok")]
    [DataRow("key AKIAIOSFODNN7EXAMPLE used", "key AKIAIO************LE used")]
    [DataRow("slack xoxb-1234567890-abcdefghij here", "slack xoxb-1234*************ghij here")]
    [DataRow("pat github_pat_abcdefghijklmnopqrstuvwxyz end", "pat github_pat_abcd******************wxyz end")]
    public void PrefixedTokens_AreMasked_KeepingTheirPrefix(string text, string expected)
        => Assert.AreEqual(expected, text.Redact());

    [TestMethod]
    public void PrefixedTokens_TooShortToBeTokens_AreLeftAlone()
        => Assert.AreEqual("ghp_short and AKIA123", "ghp_short and AKIA123".Redact());

    [TestMethod]
    [DataRow("""{"password": "hunter2hunter2", "user": "ada"}""", """{"password": "hu**********r2", "user": "ada"}""")]
    [DataRow("Server=x;Password=hunter2hunter2;User=y", "Server=x;Password=hu**********r2;User=y")]
    [DataRow("GET /cb?access_token=abcdefghijklmnop&page=2", "GET /cb?access_token=ab************op&page=2")]
    [DataRow("client_secret: abcdefghijklmnop,", "client_secret: ab************op,")]
    [DataRow("x-api-key=abcdefghijklmnop", "x-api-key=ab************op")]
    [DataRow("DB_PWD='hunter2hunter2'", "DB_PWD='hu**********r2'")]
    [DataRow("token=abc", "token=***")]
    public void NamedSecrets_HaveTheirValueMasked(string text, string expected)
        => Assert.AreEqual(expected, text.Redact());

    /// <summary>The value of a named secret ends at the first space, quote, <c>&amp;</c>, <c>;</c> or comma: documented, and tested so it stays visible.</summary>
    [TestMethod]
    public void ANamedSecret_EndsAtTheFirstDelimiter()
        => Assert.AreEqual("password=a****f and more words", "password=abcdef and more words".Redact());

    [TestMethod]
    public void AuthorizationHeaderLine_KeepsItsScheme_WhenTheValueIsAlsoANamedSecret()
        => Assert.AreEqual("authorization: Bearer abcd**************stuv", "authorization: Bearer abcdefghijklmnopqrstuv".Redact());

    #endregion

    #region Banking and identity numbers

    [TestMethod]
    public void PaymentCard_IsMasked_InText()
        => Assert.AreEqual("card **** **** **** 1111 expires 12/30", "card 4111 1111 1111 1111 expires 12/30".Redact());

    [TestMethod]
    [DataRow("pay 4111111111111111.", "pay ************1111.")]
    [DataRow("pay 4111-1111-1111-1111,", "pay ****-****-****-1111,")]
    [DataRow("amex 378282246310005!", "amex ***********0005!")]
    public void PaymentCard_IsMasked_WhateverSurroundsIt(string text, string expected)
        => Assert.AreEqual(expected, text.Redact());

    /// <summary>
    /// These look like card numbers but are not: a Luhn failure, a millisecond timestamp (starts with 1, no payment network does), and dates
    /// and counts. Masking them would make logs useless.
    /// </summary>
    [TestMethod]
    [DataRow("id=4111111111111112")]                // Luhn fails
    [DataRow("ts=1700000000000")]                   // 13 digits, starts with 1
    [DataRow("ts=1700000000004")]                   // same, and this one does pass Luhn: only its first digit tells it is not a card
    [DataRow("n=0234567890123454")]                 // Luhn-valid 16 digits, one per first digit that no payment network uses
    [DataRow("n=1234567890123452")]
    [DataRow("n=7234567890123459")]
    [DataRow("n=8234567890123457")]
    [DataRow("n=9234567890123455")]
    [DataRow("on 2024-05-12 at 10:30, 42 items")]
    [DataRow("order 123456789012345678")]
    [DataRow("phone 0123456789")]
    public void NumbersThatAreNotCards_AreLeftAlone(string text)
        => Assert.AreEqual(text, text.Redact());

    [TestMethod]
    public void Iban_IsMasked_InText()
        => Assert.AreEqual("pay FR** **** **** **** **** ***0 189 now", "pay FR76 3000 6000 0112 3456 7890 189 now".Redact());

    /// <summary>
    /// The pattern is greedy and an upper-case word right after an IBAN looks like more of it ("... 0130 00 AND"): shorter candidates are tried
    /// until one is valid, and the word is left untouched.
    /// </summary>
    [TestMethod]
    public void Iban_FollowedByAnUpperCaseWord_IsMaskedWithoutTheWord()
        => Assert.AreEqual("pay FR** **** **** **** **** ***0 189 AND more", "pay FR76 3000 6000 0112 3456 7890 189 AND more".Redact());

    [TestMethod]
    public void Iban_WithoutSeparators_AndWithAnInvalidChecksum()
    {
        Assert.AreEqual("iban=DE" + Stars(16) + "3000.", "iban=DE89370400440532013000.".Redact());
        Assert.AreEqual("iban=DE89370400440532013001.", "iban=DE89370400440532013001.".Redact());
    }

    [TestMethod]
    public void Rib_IsMasked_OnlyWithAValidKey()
    {
        Assert.AreEqual("rib 30006 ***** *********** ** here", "rib 30006 00001 12345678901 89 here".Redact());
        Assert.AreEqual("rib 30006 00001 12345678901 88 here", "rib 30006 00001 12345678901 88 here".Redact());
    }

    [TestMethod]
    public void Nir_IsMasked_OnlyWithAValidKey()
    {
        Assert.AreEqual("ssn * ** ** ** *** *** ** ok", "ssn 1 84 12 76 451 089 46 ok".Redact());
        Assert.AreEqual("ssn 1 84 12 76 451 089 47 ok", "ssn 1 84 12 76 451 089 47 ok".Redact());
        Assert.AreEqual("ssn * ** ** ** *** *** ** ok", "ssn 1 55 05 2A 004 025 72 ok".Redact());
    }

    [TestMethod]
    public void Email_IsMasked_AndCanBeSkipped()
    {
        Assert.AreEqual("mail ***@example.com now", "mail ada@example.com now".Redact());

        var options = new RedactionOptions { ShouldSkipEmail = e => e.StartsWith("team-", StringComparison.Ordinal) };
        Assert.AreEqual("*@b.com team-x@b.com", "a@b.com team-x@b.com".Redact(options));
    }

    #endregion

    #region Options

    [TestMethod]
    public void OnlyTheRequestedKinds_AreRedacted()
    {
        const string text = "ada@example.com paid with 4111 1111 1111 1111";

        Assert.AreEqual("***@example.com paid with 4111 1111 1111 1111", text.Redact(new RedactionOptions { Kinds = RedactionKinds.Emails }));
        Assert.AreEqual("ada@example.com paid with **** **** **** 1111", text.Redact(new RedactionOptions { Kinds = RedactionKinds.PaymentCards }));
        Assert.AreEqual(text, text.Redact(new RedactionOptions { Kinds = RedactionKinds.None }));
    }

    [TestMethod]
    public void EveryKind_CanBeSwitchedOffOnItsOwn()
    {
        const string text = "u postgres://a:b1234567@h/x Bearer abcdefghijklmnopqrstuv eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.abc-DEF_123 "
                            + "ghp_abcdefghijklmnopqrstuvwxyz1 password=hunter2hunter2 FR76 3000 6000 0112 3456 7890 189 "
                            + "30006 00001 12345678901 89 1 84 12 76 451 089 46 4111 1111 1111 1111 ada@example.com";

        var everything = text.Redact();
        Assert.AreNotEqual(text, everything);

        foreach (var kind in Enum.GetValues<RedactionKinds>().Where(k => k is not (RedactionKinds.None or RedactionKinds.All)))
        {
            var without = text.Redact(new RedactionOptions { Kinds = RedactionKinds.All & ~kind });
            Assert.AreNotEqual(everything, without, $"switching off {kind} changed nothing");
            Assert.AreEqual(text.Length, without.Length, kind.ToString());
        }
    }

    /// <summary>With check digits not required, anything shaped like a number is masked: more recall, more false positives.</summary>
    [TestMethod]
    public void WithoutChecksum_NumbersOfTheRightShapeAreMasked()
    {
        var recall = new RedactionOptions { RequireChecksum = false };

        Assert.AreEqual("card **** **** **** 1112", "card 4111 1111 1111 1112".Redact(recall));
        Assert.AreEqual("ts=" + Stars(9) + "0000", "ts=1700000000000".Redact(recall));
        Assert.AreEqual("rib 30006 ***** *********** **", "rib 30006 00001 12345678901 88".Redact(recall));
        Assert.AreEqual("ssn * ** ** ** *** *** **", "ssn 1 84 12 76 451 089 47".Redact(recall));
        Assert.AreEqual("iban=DE" + Stars(16) + "3001", "iban=DE89370400440532013001".Redact(recall));

        // Even without check digits the length must fit an IBAN: 35 characters is one too many.
        var tooLong = "AB12" + new string('X', 31);
        Assert.AreEqual("x " + tooLong + " y", ("x " + tooLong + " y").Redact(recall));
    }

    #endregion

    #region General behaviour

    [TestMethod]
    public void NullAndEmpty_AreReturnedAsIs()
    {
        Assert.IsNull(((string?)null).Redact());
        Assert.AreEqual(string.Empty, string.Empty.Redact());
    }

    [TestMethod]
    public void TextWithNothingToHide_IsReturnedAsTheSameInstance()
    {
        var text = new string("a perfectly ordinary log line, 42 items processed in 12 ms".AsSpan());

        Assert.AreSame(text, text.Redact());
    }

    [TestMethod]
    public void SeveralSecrets_InOneText_AreAllMasked_WithTheSameLength()
    {
        const string text = "user ada@example.com paid 4111 1111 1111 1111 via postgres://app:s3cr3t-pass@db/shop using token=abcdef123456";

        var redacted = text.Redact()!;

        Assert.AreEqual(text.Length, redacted.Length);
        foreach (var secret in new[] { "ada@", "1111 1111", "s3cr3t-pass", "abcdef123456" })
            Assert.DoesNotContain(secret, redacted);
    }

    /// <summary>
    /// Regression found by the random tests: the visible tail of a masked address (<c>wxyz@d.com</c>) was taken for a new address
    /// and masked on the next pass.
    /// </summary>
    [TestMethod]
    public void AlreadyMaskedEmail_IsNotMaskedAgain()
    {
        const string text = "to abcdefghijklmnopqrstuvwxyz@example.com now";

        var once = text.Redact()!;

        Assert.AreEqual("to abcd" + Stars(18) + "wxyz@example.com now", once);
        Assert.AreEqual(once, once.Redact());
    }

    /// <summary>The guard also holds at the very start of the text: nothing before the address, only a mask character.</summary>
    [TestMethod]
    public void AnAddressRightAfterAMaskAtTheStartOfTheText_IsNotMaskedAgain()
        => Assert.AreEqual("*wxyz@example.com", "*wxyz@example.com".Redact());

    [TestMethod]
    public void Redacting_Twice_GivesTheSameText()
    {
        const string text = "x token=abcdef123456 4111 1111 1111 1111 ada@example.com FR76 3000 6000 0112 3456 7890 189";

        var once = text.Redact();

        Assert.AreEqual(once, once.Redact());
    }

    private static readonly string[] HostileUnits =
    {
        "1 ", "A1", "token=", "FR76 ", "a@", "http://", "a.", "a@a.", "password", "password=", "Bearer ", "Bearer a", "eyJabcde.", "4 ", "1-",
        "AA11 ", "%", "http://a:b@", "a=", "x@y.", "AKIA", "ghp_", "      ",
    };

    /// <summary>
    /// Every pattern is bounded, so a hundred kilobytes of the worst shapes (long runs of characters that almost match: key characters with no
    /// keyword, addresses without a domain, a keyword with no value...) is processed in well under the generous limit asserted here.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]   // a timing assertion must not compete with other tests for the CPU and the garbage collector
    public void HostileInput_IsProcessedQuickly()
    {
        foreach (var unit in HostileUnits)
        {
            var text = string.Concat(Enumerable.Repeat(unit, 100_000 / unit.Length));
            var clock = Stopwatch.StartNew();

            var redacted = text.Redact();

            clock.Stop();
            Assert.AreEqual(text.Length, redacted!.Length);
            Assert.IsLessThan(10_000.0, clock.Elapsed.TotalMilliseconds, $"took {clock.ElapsedMilliseconds} ms for '{unit}'");
        }
    }

    /// <summary>
    /// A quadratic pattern would cost 16 times more on a text 4 times larger; a linear one costs about 4 times more. The limit leaves a wide margin
    /// for timer noise and the garbage collector: it separates the two behaviours, it does not measure performance. It runs alone (<c>DoNotParallelize</c>)
    /// and keeps the best of several measurements, since a pause can only make a run slower, never faster.
    /// </summary>
    [TestMethod]
    [DoNotParallelize]
    public void HostileInput_CostGrowsLinearlyWithTheSize()
    {
        "warm up token=abc ada@example.com FR76 3000 6000 0112 3456 7890 189 Bearer abcdefghijklmnopqrstuvwxyz 4111 1111 1111 1111".Redact();

        foreach (var unit in HostileUnits)
        {
            double Measure(int size)
            {
                var text = string.Concat(Enumerable.Repeat(unit, size / unit.Length));
                var best = double.MaxValue;
                for (var attempt = 0; attempt < 5; attempt++)
                {
                    var clock = Stopwatch.StartNew();
                    text.Redact();
                    best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
                }

                return best;
            }

            var small = Math.Max(Measure(100_000), 20.0);   // below a few tens of milliseconds the clock is mostly noise
            var large = Measure(400_000);

            Assert.IsLessThan(small * 10, large, $"'{unit}': {small:0.0} ms for 100 KB, {large:0.0} ms for 400 KB");
        }
    }

    #endregion
}
