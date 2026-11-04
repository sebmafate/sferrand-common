using SFerrand.Common.Validation;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="Checksums"/>. Every expected result comes from an independent implementation of each algorithm (and, for the
/// classic Luhn example, from the specification), not from the code under test. All numbers are public examples or arithmetic constructions,
/// not real accounts, people or companies.
/// </summary>
[TestClass]
public class ChecksumsTests
{
    #region Luhn

    [TestMethod]
    [DataRow("4111111111111111")]
    [DataRow("4111 1111 1111 1111")]
    [DataRow("4111-1111-1111-1111")]
    [DataRow("5555555555554444")]
    [DataRow("378282246310005")]
    [DataRow("79927398713")]     // the example of the algorithm's description
    [DataRow("18")]              // shortest accepted
    public void IsValidLuhn_AcceptsValidNumbers(string value)
        => Assert.IsTrue(value.IsValidLuhn());

    [TestMethod]
    [DataRow("4111111111111112")]    // last digit changed
    [DataRow("4111111111111121")]    // two adjacent digits swapped
    [DataRow("1")]                   // a single digit is not a number to check
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("41111111111111a1")]
    [DataRow("4111.1111.1111.1111")] // a separator we do not accept
    [DataRow("٤١١١١١١١١١١١١١١١")]    // digits of another script
    public void IsValidLuhn_RejectsEverythingElse(string value)
        => Assert.IsFalse(value.IsValidLuhn());

    [TestMethod]
    public void IsValidLuhn_Null_IsFalse()
        => Assert.IsFalse(((string?)null).IsValidLuhn());

    #endregion

    #region IBAN

    [TestMethod]
    [DataRow("FR14 2004 1010 0505 0001 3M02 606")]
    [DataRow("FR76 3000 6000 0112 3456 7890 189")]
    [DataRow("DE89 3704 0044 0532 0130 00")]
    [DataRow("GB82 WEST 1234 5698 7654 32")]
    [DataRow("NO93 8601 1117 947")]                  // shortest national IBAN
    [DataRow("DE89370400440532013000")]
    [DataRow("GB82-WEST-1234-5698-7654-32")]
    [DataRow("gb82 west 1234 5698 7654 32")]
    public void IsValidIban_AcceptsValidIbans(string iban)
        => Assert.IsTrue(iban.IsValidIban());

    [TestMethod]
    [DataRow("DE89 3704 0044 0532 0130 01")]        // last digit changed
    [DataRow("DE88 3704 0044 0532 0130 00")]        // check digits changed
    [DataRow("DE89 3704 0044 0532 0130 0")]         // truncated
    [DataRow("NO93 8601 1117 94")]                  // 14 characters: below the minimum
    [DataRow("1234 5678 9012 3456 7890")]           // no country code
    [DataRow("DEXX 3704 0044 0532 0130 00")]        // letters instead of check digits
    [DataRow("DE89 3704 0044 0532 0130 0!")]
    [DataRow("")]
    public void IsValidIban_RejectsEverythingElse(string iban)
        => Assert.IsFalse(iban.IsValidIban());

    [TestMethod]
    public void IsValidIban_TooLong_IsFalse()
        => Assert.IsFalse(("DE89" + new string('0', 31)).IsValidIban());

    [TestMethod]
    public void IsValidIban_Null_IsFalse()
        => Assert.IsFalse(((string?)null).IsValidIban());

    #endregion

    #region RIB

    [TestMethod]
    [DataRow("30006 00001 12345678901 89")]
    [DataRow("30006-00001-12345678901-89")]
    [DataRow("30006000011234567890189")]
    [DataRow("20041 01005 0500013M026 06")]
    [DataRow("30002 00550 0000157841Z 25")]
    public void IsValidRibKey_AcceptsValidRibs(string rib)
        => Assert.IsTrue(rib.IsValidRibKey());

    /// <summary>
    /// The letters of the account number are converted as the standard prescribes: A-I to 1-9, J-R to 1-9, S-Z to 2-9. Each row
    /// uses a key computed independently, so a wrong conversion table would change the expected key and fail.
    /// </summary>
    [TestMethod]
    [DataRow("11111 22222 ABCDEFGHIJK 96")]
    [DataRow("11111 22222 LMNOPQRSTUV 44")]
    [DataRow("11111 22222 WXYZ0123456 10")]
    [DataRow("11111 22222 0000000000I 68")]
    [DataRow("11111 22222 0000000000R 68")]
    [DataRow("11111 22222 0000000000Z 68")]
    [DataRow("11111 22222 0000000000J 92")]
    [DataRow("11111 22222 0000000000S 89")]
    public void IsValidRibKey_ConvertsAccountLettersLikeTheStandard(string rib)
        => Assert.IsTrue(rib.IsValidRibKey());

    [TestMethod]
    [DataRow("11111 22222 0000000000I 69")]    // right shape, key off by one
    [DataRow("30006 00001 12345678901 88")]
    [DataRow("30006 00001 12345678901 8")]     // 22 characters
    [DataRow("30006 00001 12345678901 890")]   // 24 characters
    [DataRow("3000A 00001 12345678901 89")]    // letter in the bank code
    [DataRow("30006 0000B 12345678901 89")]    // letter in the branch code
    [DataRow("30006 00001 12345678901 8A")]    // letter in the key
    [DataRow("30006 00001 1234567890! 89")]
    [DataRow("")]
    public void IsValidRibKey_RejectsEverythingElse(string rib)
        => Assert.IsFalse(rib.IsValidRibKey());

    [TestMethod]
    public void IsValidRibKey_Null_IsFalse()
        => Assert.IsFalse(((string?)null).IsValidRibKey());

    #endregion

    #region SIREN / SIRET

    [TestMethod]
    [DataRow("732829320")]
    [DataRow("732 829 320")]
    [DataRow("443061841")]
    [DataRow("552032534")]
    public void IsValidSiren_AcceptsValidNumbers(string siren)
        => Assert.IsTrue(siren.IsValidSiren());

    [TestMethod]
    [DataRow("123456789")]
    [DataRow("73282932")]
    [DataRow("7328293200")]
    [DataRow("73282932A")]
    [DataRow("")]
    public void IsValidSiren_RejectsEverythingElse(string siren)
        => Assert.IsFalse(siren.IsValidSiren());

    [TestMethod]
    [DataRow("73282932000074")]
    [DataRow("732 829 320 00074")]
    [DataRow("44306184100047")]
    [DataRow("55203253400646")]
    public void IsValidSiret_AcceptsValidNumbers(string siret)
        => Assert.IsTrue(siret.IsValidSiret());

    [TestMethod]
    [DataRow("73282932000075")]     // Luhn broken
    [DataRow("7328293200007")]      // 13 digits
    [DataRow("732829320000744")]    // 15 digits
    [DataRow("7328293200007A")]
    [DataRow("")]
    public void IsValidSiret_RejectsEverythingElse(string siret)
        => Assert.IsFalse(siret.IsValidSiret());

    /// <summary>
    /// The establishments of La Poste (SIREN 356 000 000) are validated by "the digits add up to a multiple of 5" instead of Luhn.
    /// <c>…01</c> only satisfies that rule, <c>…14</c> only satisfies Luhn: swapping the rules would flip both results.
    /// </summary>
    [TestMethod]
    [DataRow("35600000000001", true)]    // digit sum 15, Luhn fails
    [DataRow("35600000000006", true)]    // satisfies both
    [DataRow("35600000000014", false)]   // Luhn holds, digit sum is not a multiple of 5
    [DataRow("35600000000000", false)]
    public void IsValidSiret_AppliesTheLaPosteRuleToItsEstablishments(string siret, bool expected)
        => Assert.AreEqual(expected, siret.IsValidSiret());

    [TestMethod]
    public void IsValidSirenAndSiret_Null_IsFalse()
    {
        Assert.IsFalse(((string?)null).IsValidSiren());
        Assert.IsFalse(((string?)null).IsValidSiret());
    }

    #endregion

    #region NIR

    [TestMethod]
    [DataRow("184127645108946")]
    [DataRow("1 84 12 76 451 089 46")]
    [DataRow("1-84-12-76-451-089-46")]
    [DataRow("1 55 05 2A 004 025 72")]    // Corsica: 2A counts as 19
    [DataRow("155052a00402572")]          // lower case is accepted
    [DataRow("1 55 05 2B 004 025 02")]    // Corsica: 2B counts as 18
    public void IsValidNir_AcceptsValidNumbers(string nir)
        => Assert.IsTrue(nir.IsValidNir());

    [TestMethod]
    [DataRow("184127645108947")]          // wrong key
    [DataRow("1 55 05 2A 004 025 02")]    // the key of 2B on a 2A number
    [DataRow("1841276451089")]            // 13 characters: no key
    [DataRow("18412764510894")]           // 14 characters
    [DataRow("1841276451089466")]         // 16 characters
    [DataRow("1 84 12 2A 451 089 46")]    // consistent shape but wrong key for these digits
    [DataRow("1 84 1A 76 451 089 46")]    // a letter outside the department
    [DataRow("1 84 12 3A 451 089 46")]    // only 2A and 2B are departments with letters
    [DataRow("1 84 12 2C 451 089 46")]    // a letter after the 2, but not A or B
    [DataRow("")]
    public void IsValidNir_RejectsEverythingElse(string nir)
        => Assert.IsFalse(nir.IsValidNir());

    [TestMethod]
    public void IsValidNir_Null_IsFalse()
        => Assert.IsFalse(((string?)null).IsValidNir());

    #endregion

    #region VAT number

    [TestMethod]
    [DataRow("FR44732829320")]
    [DataRow("FR 44 732 829 320")]
    [DataRow("fr44732829320")]
    [DataRow("FR64443061841")]
    public void IsValidFrenchVatNumber_AcceptsValidNumbers(string vat)
        => Assert.IsTrue(vat.IsValidFrenchVatNumber());

    [TestMethod]
    [DataRow("FR45732829320")]      // wrong key
    [DataRow("FR32123456789")]      // the key matches, but the embedded SIREN is not valid
    [DataRow("DE44732829320")]      // not a French number
    [DataRow("FRXX732829320")]      // keys with letters cannot be checked
    [DataRow("FR4473282932")]       // too short
    [DataRow("FR447328293200")]     // too long
    [DataRow("")]
    public void IsValidFrenchVatNumber_RejectsEverythingElse(string vat)
        => Assert.IsFalse(vat.IsValidFrenchVatNumber());

    [TestMethod]
    public void IsValidFrenchVatNumber_Null_IsFalse()
        => Assert.IsFalse(((string?)null).IsValidFrenchVatNumber());

    #endregion
}
