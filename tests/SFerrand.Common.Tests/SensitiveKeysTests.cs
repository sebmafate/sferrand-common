using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="SensitiveKeys"/>: which names are considered sensitive, masking of header and dictionary values, and log-safe URLs
/// and query strings (parameter names matched after URL decoding, structure and length preserved).
/// </summary>
[TestClass]
public class SensitiveKeysTests
{
    #region IsSensitive

    [TestMethod]
    [DataRow("Authorization")]
    [DataRow("Proxy-Authorization")]
    [DataRow("X-Api-Key")]
    [DataRow("x_api_key")]
    [DataRow("apikey")]
    [DataRow("Set-Cookie")]
    [DataRow("Cookie")]
    [DataRow("password")]
    [DataRow("PASSWORD")]
    [DataRow("db.password")]
    [DataRow("user_pwd")]
    [DataRow("client_secret")]
    [DataRow("access_token")]
    [DataRow("Refresh-Token")]
    [DataRow("sig")]
    [DataRow("SIG")]
    [DataRow("credentials")]
    [DataRow("Session-Id")]
    [DataRow("private key")]
    [DataRow("AccessKey")]
    public void IsSensitive_RecognizesCommonSecretNames(string name)
        => Assert.IsTrue(SensitiveKeys.IsSensitive(name));

    [TestMethod]
    [DataRow("page")]
    [DataRow("Content-Type")]
    [DataRow("Accept")]
    [DataRow("user")]
    [DataRow("signature")]      // only the exact name "sig" is matched: the fragment would catch "design", "signal"...
    [DataRow("design")]
    [DataRow("")]
    [DataRow("   ")]
    public void IsSensitive_DoesNotFlagOrdinaryNames(string name)
        => Assert.IsFalse(SensitiveKeys.IsSensitive(name));

    [TestMethod]
    public void IsSensitive_Null_IsFalse()
        => Assert.IsFalse(SensitiveKeys.IsSensitive(null));

    [TestMethod]
    public void IsSensitive_AcceptsAdditionalNames_MatchedLikeTheBuiltInOnes()
    {
        Assert.IsFalse(SensitiveKeys.IsSensitive("X-IBAN"));
        Assert.IsTrue(SensitiveKeys.IsSensitive("X-IBAN", new[] { "iban" }));
        Assert.IsTrue(SensitiveKeys.IsSensitive("customer_iban_value", new[] { "IBAN" }));
        Assert.IsFalse(SensitiveKeys.IsSensitive("page", new[] { "iban", "", "  " }));
    }

    #endregion

    #region Values

    /// <summary>For an authorization header the scheme is not secret and helps diagnosing, so only the credential is masked.</summary>
    [TestMethod]
    [DataRow("Authorization", "Bearer abcdefghijklmnop")]
    [DataRow("authorization", "Basic dXNlcjpwYXNzd29yZA==")]
    [DataRow("Proxy-Authorization", "Bearer x")]
    [DataRow("Authorization", "Bearer 1234 5678")]      // only the first space separates the scheme
    public void MaskValue_KeepsTheSchemeOfAnAuthorizationHeader(string name, string value)
    {
        var space = value.IndexOf(' ');

        Assert.AreEqual(value[..(space + 1)] + value[(space + 1)..].Obfuscate(), SensitiveKeys.MaskValue(name, value));
    }

    [TestMethod]
    public void MaskValue_AnAuthorizationHeader_HasAKnownExactOutput()
        => Assert.AreEqual("Bearer ab" + new string('*', 12) + "op", SensitiveKeys.MaskValue("Authorization", "Bearer abcdefghijklmnop"));

    /// <summary>No recognizable scheme (none, empty credential, or a first word too long to be one): the whole value is masked.</summary>
    [TestMethod]
    [DataRow("Authorization", "abcdefghijklmnop")]
    [DataRow("Authorization", "Bearer ")]
    [DataRow("Authorization", "AVeryLongSchemeName abcdefghij")]
    [DataRow("Authorization", "B3arer abcdefghijklmnop")]        // a scheme is letters only
    public void MaskValue_AnAuthorizationValueWithoutARecognizableScheme_IsMaskedWhole(string name, string value)
        => Assert.AreEqual(value.Obfuscate(), SensitiveKeys.MaskValue(name, value));

    /// <summary>Only an authorization header keeps a scheme: a password that happens to contain a space must not leak its first word.</summary>
    [TestMethod]
    public void MaskValue_AValueWithASpaceUnderAnotherName_IsMaskedWhole()
    {
        const string passphrase = "correct horse battery";

        Assert.AreEqual(passphrase.Obfuscate(), SensitiveKeys.MaskValue("password", passphrase));
        Assert.DoesNotContain("correct h", SensitiveKeys.MaskValue("password", passphrase)!);
    }

    [TestMethod]
    public void MaskValue_LeavesOrdinaryNamesAlone()
    {
        Assert.AreEqual("application/json", SensitiveKeys.MaskValue("Content-Type", "application/json"));
        Assert.IsNull(SensitiveKeys.MaskValue("password", null));
        Assert.AreEqual(string.Empty, SensitiveKeys.MaskValue("password", string.Empty));
        Assert.AreEqual("x", SensitiveKeys.MaskValue(null, "x"));
    }

    [TestMethod]
    public void MaskValue_HonoursAdditionalNames()
        => Assert.AreEqual("FR76" + new string('*', 10) + "2189", SensitiveKeys.MaskValue("X-IBAN", "FR7630006000012189", new[] { "iban" }));   // 18 characters: 4 on each side

    [TestMethod]
    public void MaskValues_MasksTheSensitivePairs_KeepingOrderAndDuplicates()
    {
        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("Accept", "text/plain"),
            new("Authorization", "Bearer abcdefghijklmnop"),
            new("X-Api-Key", "abcdefghijklmnop"),
            new("X-Api-Key", null),
            new("Accept", "text/html"),
        };

        var masked = SensitiveKeys.MaskValues(pairs).ToList();

        CollectionAssert.AreEqual(
            new[] { "Accept=text/plain", "Authorization=Bearer ab************op", "X-Api-Key=ab************op", "X-Api-Key=", "Accept=text/html" },
            masked.Select(p => $"{p.Key}={p.Value}").ToArray());
        Assert.AreEqual("Bearer abcdefghijklmnop", pairs[1].Value);   // the input is not modified
    }

    [TestMethod]
    public void MaskValues_NullSequence_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => SensitiveKeys.MaskValues(null!));

    #endregion

    #region URLs and queries

    [TestMethod]
    public void ObfuscateUrl_MasksTheSensitiveQueryParameters_AndKeepsTheRest()
        => Assert.AreEqual(
            "https://api.example.com/v1/items?page=2&access_token=ab************op&sort=name",
            "https://api.example.com/v1/items?page=2&access_token=abcdefghijklmnop&sort=name".ObfuscateUrl());

    [TestMethod]
    public void ObfuscateUrl_AlsoMasksThePasswordOfTheUserInfo()
        => Assert.AreEqual(
            "https://app:pw******78@example.com/v1?token=ab************op",
            "https://app:pw12345678@example.com/v1?token=abcdefghijklmnop".ObfuscateUrl());

    [TestMethod]
    public void ObfuscateUrl_LeavesTheFragmentAlone()
        => Assert.AreEqual("https://x.test/a?sig=a******h#section-1", "https://x.test/a?sig=abcdefgh#section-1".ObfuscateUrl());

    /// <summary>A <c>?</c> that comes after a <c>#</c> belongs to the fragment, which is never sent to a server: it is not a query.</summary>
    [TestMethod]
    public void ObfuscateUrl_AQuestionMarkInsideTheFragment_IsNotAQuery()
        => Assert.AreEqual("https://x.test/a#top?token=abcdefghij", "https://x.test/a#top?token=abcdefghij".ObfuscateUrl());

    [TestMethod]
    [DataRow("https://x.test/a/b")]
    [DataRow("/relative/path")]
    [DataRow("https://x.test/a?page=2&sort=name")]
    [DataRow("https://x.test/a?")]
    public void ObfuscateUrl_WithNothingToHide_IsReturnedUnchanged(string url)
        => Assert.AreEqual(url, url.ObfuscateUrl());

    [TestMethod]
    public void ObfuscateUrl_WorksOnRelativeUrls()
        => Assert.AreEqual("/cb?token=12" + new string('*', 12) + "ef&state=xyz", "/cb?token=1234567890abcdef&state=xyz".ObfuscateUrl());

    [TestMethod]
    public void ObfuscateUrl_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateUrl());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateUrl());
    }

    [TestMethod]
    public void ObfuscateUrl_HonoursAdditionalNames()
        => Assert.AreEqual("https://x.test/a?iban=FR" + new string('*', 12) + "89", "https://x.test/a?iban=FR76300060000189".ObfuscateUrl(new[] { "iban" }));

    [TestMethod]
    public void ObfuscateQuery_MasksTheSensitiveValues()
        => Assert.AreEqual("a=1&password=hu**********r2&sig=a******h&b=", "a=1&password=hunter2hunter2&sig=abcdefgh&b=".ObfuscateQuery());

    /// <summary>The name is compared after URL decoding (<c>%5F</c> is <c>_</c>, <c>+</c> is a space), because that is how the server reads it.</summary>
    [TestMethod]
    [DataRow("access%5Ftoken=abcdefghijklmnop", "access%5Ftoken=ab************op")]
    [DataRow("access%5ftoken=abcdefghijklmnop", "access%5ftoken=ab************op")]
    [DataRow("client+secret=abcdefghijklmnop", "client+secret=ab************op")]
    [DataRow("%70assword=abcdefghijklmnop", "%70assword=ab************op")]
    [DataRow("pass+word=abcdefghijklmnop", "pass+word=ab************op")]    // '+' is a space, which the name normalization then removes
    public void ObfuscateQuery_DecodesTheParameterNameBeforeMatching(string query, string expected)
        => Assert.AreEqual(expected, query.ObfuscateQuery());

    [TestMethod]
    public void ObfuscateQuery_KeepsParametersWithoutAValue_AndInvalidEscapes()
    {
        Assert.AreEqual("flag&token&=x&a=1", "flag&token&=x&a=1".ObfuscateQuery());
        Assert.AreEqual("pass%zzword=abcdefgh", "pass%zzword=abcdefgh".ObfuscateQuery());   // not a sensitive name once decoded; must not throw
    }

    [TestMethod]
    public void ObfuscateQuery_KeepsTheLength_WhateverTheContent()
    {
        var random = new Random(20260505);
        const string alphabet = "abc=&%+-_ 0123tokenpwd";
        for (var i = 0; i < 3000; i++)
        {
            var query = string.Concat(Enumerable.Range(0, random.Next(0, 30)).Select(_ => alphabet[random.Next(alphabet.Length)]));

            Assert.AreEqual(query.Length, query.ObfuscateQuery()!.Length, query);
        }
    }

    [TestMethod]
    public void ObfuscateQuery_HonoursAdditionalNames_AndNullOrEmpty()
    {
        Assert.AreEqual("iban=FR" + new string('*', 12) + "89", "iban=FR76300060000189".ObfuscateQuery(new[] { "iban" }));
        Assert.IsNull(((string?)null).ObfuscateQuery());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateQuery());
    }

    #endregion
}
