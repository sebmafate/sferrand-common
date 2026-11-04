using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Example-based tests of <see cref="FinancialObfuscator"/>: exact outputs for cards, IBAN and RIB in their usual layouts, the
/// ceilings on what can be revealed, and the fail-safe path for values of the wrong shape.
/// All numbers are public test or documentation examples (payment scheme test cards, IBAN registry examples), not real accounts.
/// Properties that must hold for <i>any</i> input live in <see cref="FinancialObfuscatorInvariantTests"/>.
/// </summary>
[TestClass]
public class FinancialObfuscatorTests
{
    #region Card numbers

    [TestMethod]
    [DataRow("4111 1111 1111 1111", "**** **** **** 1111")]
    [DataRow("4111-1111-1111-1111", "****-****-****-1111")]
    [DataRow("4111111111111111", "************1111")]
    [DataRow("3782 822463 10005", "**** ****** *0005")]
    [DataRow("378282246310005", "***********0005")]
    [DataRow("123456789012", "********9012")]
    [DataRow("1234567890123456789", "***************6789")]
    public void ObfuscateCardNumber_KeepsLayoutAndLastFourDigits(string card, string expected)
        => Assert.AreEqual(expected, card.ObfuscateCardNumber());

    [TestMethod]
    public void ObfuscateCardNumber_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateCardNumber());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateCardNumber());
    }

    [TestMethod]
    [DataRow(6, 4, "5555-55**-****-4444")]
    [DataRow(0, 0, "****-****-****-****")]
    [DataRow(4, 0, "5555-****-****-****")]
    [DataRow(0, 2, "****-****-****-**44")]
    public void ObfuscateCardNumber_HonoursRequestedCounts(int leading, int trailing, string expected)
        => Assert.AreEqual(expected, "5555-5555-5555-4444".ObfuscateCardNumber(leading, trailing));

    /// <summary>
    /// Separators do not use up the budget: asking for 6 leading digits shows six digits, even though a hyphen sits among them.
    /// </summary>
    [TestMethod]
    public void ObfuscateCardNumber_CountsDigitsNotSeparators()
        => Assert.AreEqual("4111-11**-****-****", "4111-1111-1111-1111".ObfuscateCardNumber(nbLeading: 6, nbTrailing: 0));

    /// <summary>
    /// The PCI DSS ceiling (first six, last four) holds whatever is asked: a caller cannot turn the method into a no-op.
    /// </summary>
    [TestMethod]
    [DataRow(8, 9, "411111******1111")]
    [DataRow(int.MaxValue, int.MaxValue, "411111******1111")]
    [DataRow(-5, -5, "****************")]
    public void ObfuscateCardNumber_CapsWhatCanBeRevealed(int leading, int trailing, string expected)
        => Assert.AreEqual(expected, "4111111111111111".ObfuscateCardNumber(leading, trailing));

    [TestMethod]
    public void ObfuscateCardNumber_ShortestNumberStillHasMaskedDigits()
        => Assert.AreEqual("123456**9012", "123456789012".ObfuscateCardNumber(6, 4));

    [TestMethod]
    public void ObfuscateCardNumber_UsesTheRequestedMaskCharacter()
        => Assert.AreEqual("############1111", "4111111111111111".ObfuscateCardNumber(mask: '#'));

    [TestMethod]
    public void ObfuscateCardNumber_KeepsSurroundingSeparators()
        => Assert.AreEqual("  **** **** **** 1111 ", "  4111 1111 1111 1111 ".ObfuscateCardNumber());

    /// <summary>
    /// A value that is not 12 to 19 ASCII digits is not treated as a card: it is masked as a whole by the generic rule
    /// (a few characters at each end at most), never returned untouched.
    /// </summary>
    [TestMethod]
    [DataRow("12345678901", "12*******01")]                      // 11 digits: too short
    [DataRow("12345678901234567890", "1234************7890")]    // 20 digits: too long
    [DataRow("4111 1111 1111 11x1", "4111***********11x1")]      // a letter
    [DataRow("4111.1111.1111.1111", "4111***********1111")]      // a separator we do not accept
    [DataRow("٤١١١١١١١١١١١١١١١", "٤١************١١")]            // digits of another script (not banking digits)
    [DataRow("----", "****")]                                    // nothing but separators
    public void ObfuscateCardNumber_WrongShape_IsMaskedAsAWhole(string value, string expected)
    {
        var masked = value.ObfuscateCardNumber();

        Assert.AreEqual(expected, masked);
        Assert.AreNotEqual(value, masked);
    }

    #endregion

    #region IBAN

    [TestMethod]
    [DataRow("FR14 2004 1010 0505 0001 3M02 606", "FR** **** **** **** **** ***2 606")]
    [DataRow("FR76 3000 6000 0112 3456 7890 189", "FR** **** **** **** **** ***0 189")]
    [DataRow("DE89 3704 0044 0532 0130 00", "DE** **** **** **** **30 00")]
    [DataRow("GB82-WEST-1234-5698-7654-32", "GB**-****-****-****-**54-32")]
    [DataRow("NO93 8601 1117 947", "NO** **** ***7 947")]
    public void ObfuscateIban_KeepsCountryAndLastFour(string iban, string expected)
        => Assert.AreEqual(expected, iban.ObfuscateIban());

    [TestMethod]
    public void ObfuscateIban_WithoutSeparators_KeepsTheSameCharacters()
        => Assert.AreEqual("FR" + new string('*', 21) + "0189", "FR7630006000011234567890189".ObfuscateIban());

    [TestMethod]
    public void ObfuscateIban_AcceptsAndPreservesLowerCase()
        => Assert.AreEqual("gb** **** **** **** **54 32", "gb82 west 1234 5698 7654 32".ObfuscateIban());

    [TestMethod]
    public void ObfuscateIban_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateIban());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateIban());
    }

    [TestMethod]
    [DataRow(0, 4)]
    [DataRow(4, 0)]
    [DataRow(0, 0)]
    [DataRow(2, 2)]
    public void ObfuscateIban_HonoursRequestedCounts(int leading, int trailing)
    {
        const string iban = "DE89370400440532013000";
        var masked = iban.ObfuscateIban(leading, trailing);

        Assert.AreEqual(iban[..leading] + new string('*', iban.Length - leading - trailing) + iban[^trailing..], masked);
    }

    /// <summary>
    /// The IBAN ceiling is four characters on each side, whatever is asked, so the IBAN of a 15-character country keeps at least seven masked.
    /// </summary>
    [TestMethod]
    public void ObfuscateIban_CapsWhatCanBeRevealed()
    {
        Assert.AreEqual("DE89" + new string('*', 14) + "3000", "DE89370400440532013000".ObfuscateIban(100, 100));
        Assert.AreEqual(new string('*', 22), "DE89370400440532013000".ObfuscateIban(-3, -3));
        Assert.AreEqual("NO93" + new string('*', 7) + "7947", "NO9386011117947".ObfuscateIban(int.MaxValue, int.MaxValue));
    }

    [TestMethod]
    public void ObfuscateIban_AcceptsTheLongestPossibleShape()
    {
        var iban = "AB12" + new string('X', 30);   // 34 characters: the ISO 13616 maximum (synthetic, shape only)

        Assert.AreEqual("AB" + new string('*', 28) + "XXXX", iban.ObfuscateIban());
    }

    /// <summary>
    /// 14 characters is below the shortest IBAN and 35 above the longest; a leading digit, letters among the check digits,
    /// a symbol or a French RIB (no country code) are not IBANs either. All of them fall back to the generic masking.
    /// </summary>
    [TestMethod]
    [DataRow("NO93 8601 1117 94")]
    [DataRow("1234 5678 9012 3456 7890")]
    [DataRow("DEAB 3704 0044 0532 0130 00")]
    [DataRow("DE89 3704 0044 0532 0130 0!")]
    [DataRow("DE89.3704.0044.0532.0130.00")]
    [DataRow("30006 00001 12345678901 89")]
    [DataRow("DE")]
    public void ObfuscateIban_WrongShape_FallsBackToTheGenericMasking(string value)
    {
        var masked = value.ObfuscateIban();

        Assert.AreEqual(value.Obfuscate(), masked);
        Assert.AreNotEqual(value, masked);
    }

    [TestMethod]
    public void ObfuscateIban_TooLong_FallsBackToTheGenericMasking()
    {
        var value = "AB12" + new string('X', 31);   // 35 characters

        Assert.AreEqual(value.Obfuscate(), value.ObfuscateIban());
    }

    #endregion

    #region RIB

    [TestMethod]
    [DataRow("30006 00001 12345678901 89", "30006 ***** *********** **")]
    [DataRow("30006-00001-12345678901-89", "30006-*****-***********-**")]
    [DataRow("20041 01005 0500013M026 06", "20041 ***** *********** **")]
    public void ObfuscateRib_KeepsTheBankCodeOnly(string rib, string expected)
        => Assert.AreEqual(expected, rib.ObfuscateRib());

    [TestMethod]
    public void ObfuscateRib_WithoutSeparators_KeepsTheBankCodeOnly()
        => Assert.AreEqual("30006" + new string('*', 18), "30006000011234567890189".ObfuscateRib());

    [TestMethod]
    public void ObfuscateRib_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateRib());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateRib());
    }

    [TestMethod]
    public void ObfuscateRib_CanKeepTrailingCharacters_ToTellAccountsApart()
    {
        Assert.AreEqual("30006" + new string('*', 15) + "189", "30006000011234567890189".ObfuscateRib(nbTrailing: 3));
        Assert.AreEqual("30006 ***** **********1 89", "30006 00001 12345678901 89".ObfuscateRib(nbTrailing: 3));
    }

    [TestMethod]
    public void ObfuscateRib_CanKeepTheBranchCode()
        => Assert.AreEqual("30006 00001 *********** **", "30006 00001 12345678901 89".ObfuscateRib(nbLeading: 10));

    [TestMethod]
    public void ObfuscateRib_CanMaskEverything()
        => Assert.AreEqual(new string('*', 23), "30006000011234567890189".ObfuscateRib(0, 0));

    /// <summary>
    /// The RIB ceiling is the bank and branch codes on one side and four characters on the other (14 of 23): the account number
    /// can never be shown in full, however the method is called.
    /// </summary>
    [TestMethod]
    public void ObfuscateRib_CapsWhatCanBeRevealed()
        => Assert.AreEqual("3000600001" + new string('*', 9) + "0189", "30006000011234567890189".ObfuscateRib(100, 100));

    [TestMethod]
    public void ObfuscateRib_UsesTheRequestedMaskCharacter()
        => Assert.AreEqual("30006 ##### ########### ##", "30006 00001 12345678901 89".ObfuscateRib(mask: '#'));

    /// <summary>
    /// Wrong length, a letter where only digits are allowed (bank code, branch code, key) or an IBAN (27 characters)
    /// are not RIBs: they fall back to the generic masking instead of being treated as one.
    /// </summary>
    [TestMethod]
    [DataRow("30006 00001 12345678901 8")]
    [DataRow("30006 00001 12345678901 890")]
    [DataRow("3000A 00001 12345678901 89")]
    [DataRow("30006 0000B 12345678901 89")]
    [DataRow("30006 00001 12345678901 8A")]
    [DataRow("30006 00001 1234567890! 89")]
    [DataRow("FR76 3000 6000 0112 3456 7890 189")]
    public void ObfuscateRib_WrongShape_FallsBackToTheGenericMasking(string value)
    {
        var masked = value.ObfuscateRib();

        Assert.AreEqual(value.Obfuscate(), masked);
        Assert.AreNotEqual(value, masked);
    }

    #endregion
}
