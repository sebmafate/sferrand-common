using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="FrenchIdentifierObfuscator"/>: exact outputs, the ceilings, the wrong-shape fallback, and random inputs checking
/// that the layout is kept and that the ceilings hold. Numbers are public examples or arithmetic constructions.
/// </summary>
[TestClass]
public class FrenchIdentifierObfuscatorTests
{
    #region NIR

    [TestMethod]
    [DataRow("1 84 12 76 451 089 46", "* ** ** ** *** *** **")]
    [DataRow("184127645108946", "***************")]
    [DataRow("1-84-12-76-451-089-46", "*-**-**-**-***-***-**")]
    [DataRow("1 55 05 2A 004 025 72", "* ** ** ** *** *** **")]
    public void ObfuscateNir_ShowsNothingByDefault(string nir, string expected)
        => Assert.AreEqual(expected, nir.ObfuscateNir());

    [TestMethod]
    [DataRow("1 55 05 2a 004 025 72")]
    [DataRow("1 55 05 2b 004 025 02")]
    [DataRow("1 55 05 2A 004 025 72")]
    [DataRow("1 55 05 2B 004 025 02")]
    public void ObfuscateNir_AcceptsCorsicanDepartmentsInEitherCase(string nir)
        => Assert.AreEqual("* ** ** ** *** *** **", nir.ObfuscateNir());

    [TestMethod]
    [DataRow(1, 2, "1 ** ** ** *** *** 46")]
    [DataRow(0, 2, "* ** ** ** *** *** 46")]
    [DataRow(1, 0, "1 ** ** ** *** *** **")]
    public void ObfuscateNir_HonoursRequestedCounts(int leading, int trailing, string expected)
        => Assert.AreEqual(expected, "1 84 12 76 451 089 46".ObfuscateNir(leading, trailing));

    /// <summary>
    /// The ceiling stops before the birth year, month, department and commune (characters 2 to 13): whatever is asked,
    /// at most the sex digit and the two key digits can appear.
    /// </summary>
    [TestMethod]
    public void ObfuscateNir_NeverRevealsBirthDataWhateverIsAsked()
    {
        Assert.AreEqual("1 ** ** ** *** *** 46", "1 84 12 76 451 089 46".ObfuscateNir(100, 100));
        Assert.AreEqual("* ** ** ** *** *** **", "1 84 12 76 451 089 46".ObfuscateNir(-1, -1));
        Assert.AreEqual("1 ** ** ** *** *** 72", "1 55 05 2A 004 025 72".ObfuscateNir(int.MaxValue, int.MaxValue));
    }

    [TestMethod]
    public void ObfuscateNir_UsesTheRequestedMaskCharacter()
        => Assert.AreEqual("# ## ## ## ### ### ##", "1 84 12 76 451 089 46".ObfuscateNir(mask: '#'));

    [TestMethod]
    [DataRow("1 84 12 76 451 089 4")]       // 14 characters
    [DataRow("1 84 12 76 451 089 466")]     // 16 characters
    [DataRow("1 84 1A 76 451 089 46")]      // a letter outside the department
    [DataRow("1 84 12 3A 451 089 46")]      // only 2A / 2B
    [DataRow("1 84 12 2C 451 089 46")]      // a letter after the 2, but not A or B
    [DataRow("1 84 12 76 451 089 4X")]
    public void ObfuscateNir_WrongShape_FallsBackToTheGenericMasking(string value)
    {
        var masked = value.ObfuscateNir();

        Assert.AreEqual(value.Obfuscate(), masked);
        Assert.AreNotEqual(value, masked);
    }

    [TestMethod]
    public void ObfuscateNir_NullOrEmpty_IsReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateNir());
        Assert.AreEqual(string.Empty, string.Empty.ObfuscateNir());
    }

    #endregion

    #region SIREN / SIRET / VAT

    [TestMethod]
    [DataRow("732 829 320", "*** *** 320")]
    [DataRow("732829320", "******320")]
    [DataRow("732-829-320", "***-***-320")]
    public void ObfuscateSiren_ShowsTheLastThreeDigits(string siren, string expected)
        => Assert.AreEqual(expected, siren.ObfuscateSiren());

    [TestMethod]
    public void ObfuscateSiren_CapsWhatCanBeRevealed()
    {
        Assert.AreEqual("732***320", "732829320".ObfuscateSiren(100, 100));
        Assert.AreEqual("*********", "732829320".ObfuscateSiren(-2, -2));
        Assert.AreEqual("73*******", "732829320".ObfuscateSiren(2, 0));
    }

    [TestMethod]
    [DataRow("732 829 320 00074", "*** *** *** *0074")]
    [DataRow("73282932000074", "**********0074")]
    public void ObfuscateSiret_ShowsTheLastFourDigits(string siret, string expected)
        => Assert.AreEqual(expected, siret.ObfuscateSiret());

    [TestMethod]
    public void ObfuscateSiret_CapsWhatCanBeRevealed()
        => Assert.AreEqual("7328*****00074", "73282932000074".ObfuscateSiret(100, 100));

    [TestMethod]
    [DataRow("FR 44 732 829 320", "FR ** *** *** 320")]
    [DataRow("FR44732829320", "FR********320")]
    [DataRow("fr44732829320", "fr********320")]
    public void ObfuscateFrenchVatNumber_ShowsTheCountryAndTheLastThreeCharacters(string vat, string expected)
        => Assert.AreEqual(expected, vat.ObfuscateFrenchVatNumber());

    [TestMethod]
    public void ObfuscateFrenchVatNumber_CapsWhatCanBeRevealed()
        => Assert.AreEqual("FR44*****9320", "FR44732829320".ObfuscateFrenchVatNumber(100, 100));

    [TestMethod]
    [DataRow("73282932")]
    [DataRow("7328293200")]
    [DataRow("73282932A")]
    [DataRow("")]
    public void ObfuscateSiren_WrongShape_FallsBackToTheGenericMasking(string value)
        => Assert.AreEqual(value.Obfuscate(), value.ObfuscateSiren());

    [TestMethod]
    [DataRow("7328293200007")]
    [DataRow("732829320000744")]
    [DataRow("7328293200007A")]
    [DataRow("")]
    public void ObfuscateSiret_WrongShape_FallsBackToTheGenericMasking(string value)
        => Assert.AreEqual(value.Obfuscate(), value.ObfuscateSiret());

    [TestMethod]
    [DataRow("DE44732829320")]       // another country
    [DataRow("FR4473282932")]        // too short
    [DataRow("FR44A32829320")]       // a letter in the SIREN
    [DataRow("1R44732829320")]       // no country code
    [DataRow("")]
    public void ObfuscateFrenchVatNumber_WrongShape_FallsBackToTheGenericMasking(string value)
        => Assert.AreEqual(value.Obfuscate(), value.ObfuscateFrenchVatNumber());

    [TestMethod]
    public void NullInputs_AreReturnedAsIs()
    {
        Assert.IsNull(((string?)null).ObfuscateSiren());
        Assert.IsNull(((string?)null).ObfuscateSiret());
        Assert.IsNull(((string?)null).ObfuscateFrenchVatNumber());
    }

    #endregion

    #region Random inputs

    private const int Iterations = 3000;

    private static string Digits(Random random, int count) => string.Concat(Enumerable.Range(0, count).Select(_ => (char)('0' + random.Next(10))));

    private static string Grouped(Random random, string value)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var c in value)
        {
            builder.Append(c);
            if (random.Next(4) == 0) builder.Append(random.Next(2) == 0 ? ' ' : '-');
        }

        return builder.ToString();
    }

    private static int? RandomCount(Random random) => random.Next(5) == 0 ? null : random.Next(-3, 20);

    /// <summary>Same length, separators untouched, visible characters unchanged and confined to a prefix and a suffix within the ceilings.</summary>
    private static void AssertWithinCeilings(string input, string masked, int maxLeading, int maxTrailing, int minMasked)
    {
        Assert.AreEqual(input.Length, masked.Length, input);
        var visible = new List<bool>();
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] is ' ' or '-')
            {
                Assert.AreEqual(input[i], masked[i], $"separator moved: '{input}' -> '{masked}'");
                continue;
            }

            visible.Add(masked[i] != '*');
            if (masked[i] != '*')
                Assert.AreEqual(input[i], masked[i], $"changed: '{input}' -> '{masked}'");
        }

        var first = visible.IndexOf(false);
        var last = visible.LastIndexOf(false);
        Assert.IsGreaterThanOrEqualTo(minMasked, visible.Count(v => !v), $"too much revealed: '{input}' -> '{masked}'");
        Assert.IsLessThanOrEqualTo(maxLeading, first, $"leading ceiling: '{input}' -> '{masked}'");
        Assert.IsLessThanOrEqualTo(maxTrailing, visible.Count - 1 - last, $"trailing ceiling: '{input}' -> '{masked}'");
        Assert.IsTrue(visible.Skip(first).Take(last - first + 1).All(v => !v), $"mask must be contiguous: '{input}' -> '{masked}'");
    }

    [TestMethod]
    public void RandomNumbers_KeepTheirLayoutAndRespectTheCeilings()
    {
        var random = new Random(20260501);
        for (var i = 0; i < Iterations; i++)
        {
            var nir = Grouped(random, Digits(random, 13) + Digits(random, 2));
            AssertWithinCeilings(nir, nir.ObfuscateNir(RandomCount(random), RandomCount(random)), maxLeading: 1, maxTrailing: 2, minMasked: 12);

            var siren = Grouped(random, Digits(random, 9));
            AssertWithinCeilings(siren, siren.ObfuscateSiren(RandomCount(random), RandomCount(random)), maxLeading: 3, maxTrailing: 3, minMasked: 3);

            var siret = Grouped(random, Digits(random, 14));
            AssertWithinCeilings(siret, siret.ObfuscateSiret(RandomCount(random), RandomCount(random)), maxLeading: 4, maxTrailing: 5, minMasked: 5);

            var vat = Grouped(random, "FR" + Digits(random, 2) + Digits(random, 9));
            AssertWithinCeilings(vat, vat.ObfuscateFrenchVatNumber(RandomCount(random), RandomCount(random)), maxLeading: 4, maxTrailing: 4, minMasked: 5);
        }
    }

    #endregion
}
