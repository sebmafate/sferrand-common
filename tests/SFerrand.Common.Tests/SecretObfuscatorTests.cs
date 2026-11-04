using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Example-based tests of <see cref="Security.SecretObfuscator"/>: exact expected outputs for each method, the adaptive thresholds and
/// every fail-safe path (null, empty, malformed input). Properties that must hold for <i>any</i> input live in
/// <see cref="ObfuscatorInvariantTests"/>.
/// </summary>
[TestClass]
public class SecretObfuscatorTests
{
    #region GetNumberOfPreservedChars

    [TestMethod]
    [DataRow(int.MinValue, 0)]
    [DataRow(-1, 0)]
    [DataRow(0, 0)]
    [DataRow(4, 0)]
    [DataRow(5, 1)]
    [DataRow(8, 1)]
    [DataRow(9, 2)]
    [DataRow(16, 2)]
    [DataRow(17, 4)]
    [DataRow(1000, 4)]
    [DataRow(int.MaxValue, 4)]
    public void GetNumberOfPreservedChars_FollowsThresholds(int length, int expected)
        => Assert.AreEqual(expected, SecretObfuscator.GetNumberOfPreservedChars(length));

    #endregion

    #region Obfuscate

    [TestMethod]
    [DataRow("abcd", "****")]
    [DataRow("exampleplatforms", "ex************ms")]
    [DataRow("aSuperMegaLongSecret", "aSup************cret")]
    [DataRow("A", "*")]
    [DataRow("", "")]
    public void Obfuscate_Adaptive(string secret, string expected)
        => Assert.AreEqual(expected, secret.Obfuscate());

    [TestMethod]
    public void Obfuscate_Null_ReturnsNull()
        => Assert.IsNull(((string?)null).Obfuscate());

    [TestMethod]
    [DataRow(1, "abcd", "a***")]
    [DataRow(null, "exampleplatforms", "ex************ms")]
    [DataRow(0, "exampleplatforms", "**************ms")]
    [DataRow(6, "exampleplatforms", "exampl********ms")]
    [DataRow(-3, "exampleplatforms", "**************ms")]
    public void Obfuscate_Leading(int? nbLeading, string secret, string expected)
        => Assert.AreEqual(expected, secret.Obfuscate(nbLeading: nbLeading));

    [TestMethod]
    [DataRow(0, "exampleplatforms", "ex**************")]
    [DataRow(null, "exampleplatforms", "ex************ms")]
    [DataRow(8, "exampleplatforms", "ex******latforms")]
    [DataRow(1, "abcd", "***d")]
    public void Obfuscate_Trailing(int? nbTrailing, string secret, string expected)
        => Assert.AreEqual(expected, secret.Obfuscate(nbTrailing: nbTrailing));

    [TestMethod]
    [DataRow('#', "samplesecrets", "sa#########ts")]
    [DataRow(' ', "samplesecrets", "sa         ts")]
    [DataRow('█', "samplesecrets", "sa█████████ts")]
    public void Obfuscate_MaskCharacter(char mask, string secret, string expected)
        => Assert.AreEqual(expected, secret.Obfuscate(mask: mask));

    [TestMethod]
    public void Obfuscate_TooManyLeadingAndTrailing_FallsBackToAdaptive()
        => Assert.AreEqual("so***********et", "somethingsecret".Obfuscate(nbLeading: 9, nbTrailing: 6));

    /// <summary>
    /// Asking to keep 2 + 2 characters of a 4-character value would return it in clear: the adaptive default (0 for four characters) applies instead.
    /// </summary>
    [TestMethod]
    public void Obfuscate_ShortValueWithGenerousCounts_NeverRevealsEverything()
        => Assert.AreEqual("****", "abcd".Obfuscate(nbLeading: 2, nbTrailing: 2));

    #endregion

    #region ObfuscateUri

    [TestMethod]
    public void ObfuscateUri_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateUri());
        Assert.IsNull(((string?)null).ObfuscateUri());
    }

    [TestMethod]
    [DataRow("postgres://myuser:mypassword@host:5432/database", "postgres://myuser:my******rd@host:5432/database")]
    [DataRow("postgres://myuser:mypassword@host:5432/database?some_attr=123", "postgres://myuser:my******rd@host:5432/database?some_attr=123")]
    [DataRow("postgres://myuser:mypassword@host:5432,other_host:5432/database?x=1", "postgres://myuser:my******rd@host:5432,other_host:5432/database?x=1")]
    [DataRow("https://user:p@ss:word@example.com/a@b", "https://user:p@*****rd@example.com/a@b")]
    public void ObfuscateUri_WithCredentials_MasksThePassword(string uri, string expected)
        => Assert.AreEqual(expected, uri.ObfuscateUri());

    [TestMethod]
    public void ObfuscateUri_UserInfoWithoutPassword_MasksTheWholeUserInfo()
        => Assert.AreEqual("https://ghp_********************uvwx@github.com/org/repo", "https://ghp_abcdefghijklmnopqrstuvwx@github.com/org/repo".ObfuscateUri());

    [TestMethod]
    [DataRow("https://example.com/path?token=abc")]
    [DataRow("https://example.com")]
    [DataRow("https://example.com/users/me@home")]
    public void ObfuscateUri_WithoutCredentials_IsUnchanged(string uri)
        => Assert.AreEqual(uri, uri.ObfuscateUri());

    [TestMethod]
    [DataRow("myuser:mypassword@domain.com", "myus********************.com")]
    [DataRow("not a uri at all", "no************ll")]
    public void ObfuscateUri_UnknownFormat_HidesEverything(string uri, string expected)
        => Assert.AreEqual(expected, uri.ObfuscateUri());

    [TestMethod]
    public void ObfuscateUri_UriOverload_Works()
        => Assert.AreEqual("ftp://bob:s******2@host/", new Uri("ftp://bob:secret12@host/").ObfuscateUri());

    #endregion

    #region ObfuscateConnectionString

    private const string StorageConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=somestorageaccount;AccountKey=averysecretkey==;EndpointSuffix=core.windows.net";

    [TestMethod]
    public void ObfuscateConnectionString_Empty_IsReturnedAsIs()
        => Assert.AreEqual(string.Empty, string.Empty.ObfuscateConnectionString("somekey"));

    [TestMethod]
    [DataRow("AccountKey", "DefaultEndpointsProtocol=https;AccountName=somestorageaccount;AccountKey=av************==;EndpointSuffix=core.windows.net")]
    [DataRow("accountkey", "DefaultEndpointsProtocol=https;AccountName=somestorageaccount;AccountKey=av************==;EndpointSuffix=core.windows.net")]
    [DataRow(" AccountKey ", "DefaultEndpointsProtocol=https;AccountName=somestorageaccount;AccountKey=av************==;EndpointSuffix=core.windows.net")]
    [DataRow("AccountName", "DefaultEndpointsProtocol=https;AccountName=some**********ount;AccountKey=averysecretkey==;EndpointSuffix=core.windows.net")]
    public void ObfuscateConnectionString_MasksOnlyTheRequestedKey(string key, string expected)
        => Assert.AreEqual(expected, StorageConnectionString.ObfuscateConnectionString(key));

    /// <summary>
    /// Fail-safe: if the caller names a key that is not present (typo, different casing convention in the source), the string is masked as a whole instead of being logged untouched.
    /// </summary>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    [DataRow("NoSuchKey")]
    public void ObfuscateConnectionString_EmptyOrUnknownKey_HidesEverything(string? key)
    {
        var masked = StorageConnectionString.ObfuscateConnectionString(key);

        Assert.AreEqual(StorageConnectionString.Length, masked.Length);
        Assert.DoesNotContain("averysecretkey", masked);
        Assert.StartsWith("Defa", masked);
        Assert.EndsWith(".net", masked);
    }

    /// <summary>
    /// Real storage keys are Base64: they contain <c>=</c> padding and <c>/</c>. Only the first <c>=</c> of a segment separates key from value,
    /// so the rest of the value must be masked, not mistaken for another pair. (The key used is the well-known public development key of the Azurite emulator.)
    /// </summary>
    [TestMethod]
    public void ObfuscateConnectionString_KeepsAzuriteLikeValuesContainingEqualsAndSlash()
    {
        const string input = "AccountName=dev;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;";
        var masked = input.ObfuscateConnectionString("AccountKey");

        Assert.StartsWith("AccountName=dev;AccountKey=Eby8", masked);
        Assert.EndsWith("Gw==;", masked);
        Assert.AreEqual(input.Length, masked.Length);
    }

    [TestMethod]
    public void ObfuscateConnectionString_KeepsSegmentsWithoutEqualsSign()
        => Assert.AreEqual("a=1;orphan;secret=se****;b=2", "a=1;orphan;secret=secret;b=2".ObfuscateConnectionString("secret", nbLeading: 2, nbTrailing: 0));

    [TestMethod]
    public void ObfuscateConnectionString_MasksEveryOccurrenceOfTheKey()
        => Assert.AreEqual("pwd=****;x=1;PWD=****", "pwd=abcd;x=1;PWD=wxyz".ObfuscateConnectionString("pwd"));

    #endregion

    #region ObfuscateEmail

    [TestMethod]
    public void ObfuscateEmail_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateEmail());
        Assert.IsNull(((string?)null).ObfuscateEmail());
    }

    [TestMethod]
    [DataRow("john.doe@example.com", "****.***@example.com")]
    [DataRow("jean-claude.van-damme@example.com", "****-c****e.***-d***e@example.com")]
    [DataRow("ada.lovelace@example.org", "***.l******e@example.org")]
    [DataRow("jdoe42@example.com", "j****2@example.com")]
    [DataRow("Jane_Roe+news@Example.com", "****_***+****@Example.com")]
    public void ObfuscateEmail_MasksEachNamePart_AndKeepsCaseAndDomain(string email, string expected)
        => Assert.AreEqual(expected, email.ObfuscateEmail());

    [TestMethod]
    public void ObfuscateEmail_HonoursLeadingAndTrailingCounts_ForPlainLocalParts()
        => Assert.AreEqual("jo**d@example.com", "johnd@example.com".ObfuscateEmail(nbLeading: 2, nbTrailing: 1));

    [TestMethod]
    public void ObfuscateEmail_ShouldSkipPredicate_LeavesMatchingAddressesUntouched()
    {
        static bool IsTeamMailbox(string e) => e.StartsWith("team-", StringComparison.OrdinalIgnoreCase);

        Assert.AreEqual("team-platform@example.com", "team-platform@example.com".ObfuscateEmail(shouldSkip: IsTeamMailbox));
        Assert.AreEqual("****.***@example.com", "john.doe@example.com".ObfuscateEmail(shouldSkip: IsTeamMailbox));
    }

    /// <summary>
    /// Each alphanumeric run is masked on its own and separators are kept as is, including leading, trailing and repeated ones;
    /// a run too short to mask anything (a single character) stays masked, an address with no run at all is unchanged.
    /// </summary>
    [TestMethod]
    [DataRow(".a..b@x.com", ".*..*@x.com")]
    [DataRow("-john--doe-@x.com", "-****--***-@x.com")]
    [DataRow("...@x.com", "...@x.com")]
    public void ObfuscateEmail_HandlesLeadingTrailingAndRepeatedSeparators(string email, string expected)
        => Assert.AreEqual(expected, email.ObfuscateEmail());

    /// <summary>
    /// Logging code must never throw because of a bad value: malformed addresses are masked as a whole, with their length preserved.
    /// </summary>
    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("@example.com")]
    [DataRow("john@")]
    [DataRow("a@b@c.com")]
    public void ObfuscateEmail_MalformedAddress_IsMaskedInsteadOfThrowing(string value)
    {
        var masked = value.ObfuscateEmail();

        Assert.AreEqual(value.Length, masked.Length);
        Assert.AreNotEqual(value, masked);
    }

    #endregion

    #region ObfuscateGuid

    [TestMethod]
    public void ObfuscateGuid_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateGuid());
        Assert.IsNull(((string?)null).ObfuscateGuid());
    }

    [TestMethod]
    [DataRow("0000-0000-0000-0000-0000", "0000****************0000")]
    [DataRow("NotAGuid", "N******d")]
    [DataRow("{00000000-0000-0000-0000-000000000000}", "{000******************************000}")]
    public void ObfuscateGuid_NotACanonicalGuid_HidesEverything(string guid, string expected)
        => Assert.AreEqual(expected, guid.ObfuscateGuid());

    [TestMethod]
    public void ObfuscateGuid_KeepsTheVersionGroup()
        => Assert.AreEqual("0******0-****-0000-****-00********00", "00000000-0000-0000-0000-000000000000".ObfuscateGuid());

    [TestMethod]
    public void ObfuscateGuid_GuidOverload_UsesTheCanonicalForm()
        => Assert.AreEqual("0******0-****-0000-****-00********00", Guid.Empty.ObfuscateGuid());

    #endregion

    #region ObfuscateToken

    [TestMethod]
    [DataRow("ghp_1234567890abcdefghijklmnop", "ghp_1234******************mnop")]
    [DataRow("GHP_1234567890abcdefghijklmnop", "GHP_1234******************mnop")]
    [DataRow("github_pat_11ABCDEFG0123456789", "github_pat_11AB***********6789")]
    [DataRow("sk-1234567890abcdef", "sk-1***********cdef")]
    public void ObfuscateToken_KeepsKnownPrefixVisible(string token, string expected)
        => Assert.AreEqual(expected, token.ObfuscateToken());

    [TestMethod]
    public void ObfuscateToken_CustomPrefixes()
        => Assert.AreEqual("xoxb-12************ef", "xoxb-1234567890abcdef".ObfuscateToken(knownPrefixes: new[] { "xoxb-" }));

    /// <summary>
    /// A token that is nothing but its prefix has no body to keep; treating it as an unknown format masks it entirely.
    /// </summary>
    [TestMethod]
    public void ObfuscateToken_PrefixOnly_IsFullyMasked()
        => Assert.AreEqual("****", "ghp_".ObfuscateToken());

    [TestMethod]
    public void ObfuscateToken_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateToken());
        Assert.IsNull(((string?)null).ObfuscateToken());
    }

    #endregion
}
