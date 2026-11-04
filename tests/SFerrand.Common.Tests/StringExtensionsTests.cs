using System.Text;
using SFerrand.Common.Extensions;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="Extensions.StringExtensions"/>: the seven case conventions on identifiers of every shape (acronyms, digits, separators,
/// accents), the null/empty fallbacks, strict Base64 validation and diacritics removal.
/// </summary>
[TestClass]
public class StringExtensionsTests
{
    private const string Sentence = "Ipsum sed nibh lobortis";

    #region Case conversion

    /// <summary>
    /// One table drives all the conventions, so the word-splitting rules are checked once and the outputs stay consistent with each other.
    /// </summary>
    [TestMethod]
    [DataRow(Sentence, "IpsumSedNibhLobortis", "ipsumSedNibhLobortis", "ipsum_sed_nibh_lobortis", "IPSUM_SED_NIBH_LOBORTIS", "ipsum-sed-nibh-lobortis", "IPSUM-SED-NIBH-LOBORTIS", "Ipsum-Sed-Nibh-Lobortis")]
    [DataRow("helloWorld", "HelloWorld", "helloWorld", "hello_world", "HELLO_WORLD", "hello-world", "HELLO-WORLD", "Hello-World")]
    [DataRow("XMLHttpRequest2", "XmlHttpRequest2", "xmlHttpRequest2", "xml_http_request2", "XML_HTTP_REQUEST2", "xml-http-request2", "XML-HTTP-REQUEST2", "Xml-Http-Request2")]
    [DataRow("snake_case-mixed value", "SnakeCaseMixedValue", "snakeCaseMixedValue", "snake_case_mixed_value", "SNAKE_CASE_MIXED_VALUE", "snake-case-mixed-value", "SNAKE-CASE-MIXED-VALUE", "Snake-Case-Mixed-Value")]
    [DataRow("HTTP", "Http", "http", "http", "HTTP", "http", "HTTP", "Http")]
    [DataRow("café crème", "CaféCrème", "caféCrème", "café_crème", "CAFÉ_CRÈME", "café-crème", "CAFÉ-CRÈME", "Café-Crème")]
    [DataRow("", "", "", "", "", "", "", "")]
    public void CaseConversions(string input, string pascal, string camel, string snake, string constant, string kebab, string cobol, string train)
    {
        Assert.AreEqual(pascal, input.ToPascalCase(), nameof(StringExtensions.ToPascalCase));
        Assert.AreEqual(camel, input.ToCamelCase(), nameof(StringExtensions.ToCamelCase));
        Assert.AreEqual(snake, input.ToSnakeCase(), nameof(StringExtensions.ToSnakeCase));
        Assert.AreEqual(constant, input.ToConstantCase(), nameof(StringExtensions.ToConstantCase));
        Assert.AreEqual(kebab, input.ToKebabCase(), nameof(StringExtensions.ToKebabCase));
        Assert.AreEqual(cobol, input.ToCobolCase(), nameof(StringExtensions.ToCobolCase));
        Assert.AreEqual(train, input.ToTrainCase(), nameof(StringExtensions.ToTrainCase));
    }

    [TestMethod]
    public void ToTitleCase_CapitalizesEachWord()
        => Assert.AreEqual("Ipsum Sed Nibh Lobortis", "ipsum sed nibh lobortis".ToTitleCase());

    [TestMethod]
    public void ToAlternatingCase_AlternatesOnCharacterIndex()
        => Assert.AreEqual("iPsUm sEd", "Ipsum sed".ToAlternatingCase());

    [TestMethod]
    public void CaseConversions_OnNull_ThrowArgumentNullException()
    {
        string? value = null;
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToPascalCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToCamelCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToTitleCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToSnakeCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToConstantCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToKebabCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToCobolCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToTrainCase());
        Assert.ThrowsExactly<ArgumentNullException>(() => value!.ToAlternatingCase());
    }

    #endregion

    #region Fallbacks

    [TestMethod]
    [DataRow(null, "fallback", "fallback")]
    [DataRow("", "fallback", "")]
    [DataRow("  ", "fallback", "  ")]
    [DataRow("value", "fallback", "value")]
    public void WhenNull(string? input, string fallback, string expected)
        => Assert.AreEqual(expected, input.WhenNull(fallback));

    [TestMethod]
    [DataRow(null, "fallback", "fallback")]
    [DataRow("", "fallback", "fallback")]
    [DataRow("  ", "fallback", "  ")]
    [DataRow("value", "fallback", "value")]
    public void WhenNullOrEmpty(string? input, string fallback, string expected)
        => Assert.AreEqual(expected, input.WhenNullOrEmpty(fallback));

    [TestMethod]
    [DataRow(null, "fallback", "fallback")]
    [DataRow("", "fallback", "fallback")]
    [DataRow(" ", "fallback", "fallback")]
    [DataRow("\t \r\n", "fallback", "fallback")]
    [DataRow("value", "fallback", "value")]
    public void WhenNullOrEmptyOrWhiteSpace(string? input, string fallback, string expected)
        => Assert.AreEqual(expected, input.WhenNullOrEmptyOrWhiteSpace(fallback));

    [TestMethod]
    [DataRow(null, true, true)]
    [DataRow("", true, true)]
    [DataRow(" ", false, true)]
    [DataRow("\t", false, true)]
    [DataRow("a", false, false)]
    public void IsNullOrEmpty_And_IsNullOrWhiteSpace(string? input, bool isNullOrEmpty, bool isNullOrWhiteSpace)
    {
        Assert.AreEqual(isNullOrEmpty, input.IsNullOrEmpty());
        Assert.AreEqual(isNullOrWhiteSpace, input.IsNullOrWhiteSpace());
    }

    #endregion

    #region Encoding

    [TestMethod]
    public void ToUnicodeString_DecodesUtf16()
        => Assert.AreEqual("Ipsum", Encoding.Unicode.GetBytes("Ipsum").ToUnicodeString());

    [TestMethod]
    public void ToUnicodeString_OnNull_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((byte[])null!).ToUnicodeString());

    [TestMethod]
    [DataRow("AA==")]
    [DataRow("AAA=")]
    [DataRow("AAAA")]
    [DataRow("1+AA")]
    [DataRow("TG9yZW1JcHN1bQ==")]
    public void IsBase64String_OnValid_ReturnsTrue(string value)
        => Assert.IsTrue(value.IsBase64String());

    /// <summary>
    /// The validation is deliberately strict: wrong padding, wrong length, URL-safe alphabet and any whitespace (even a trailing newline) are rejected.
    /// </summary>
    [TestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("LoremIpsum")]
    [DataRow("A===")]
    [DataRow("1===")]
    [DataRow("A==")]
    [DataRow("AAA")]
    [DataRow("A ==")]
    [DataRow("A_==")]
    [DataRow("A-==")]
    [DataRow("A\t==")]
    [DataRow("A\n==")]
    [DataRow("AAAA\n")]
    public void IsBase64String_OnInvalid_ReturnsFalse(string value)
        => Assert.IsFalse(value.IsBase64String());

    [TestMethod]
    public void IsBase64String_OnNull_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).IsBase64String());

    [TestMethod]
    public void ToBase64String_UsesUtf8ByDefault()
        => Assert.AreEqual("Y2Fmw6k=", "café".ToBase64String());

    [TestMethod]
    [DataRow("LoremIpsum")]
    [DataRow("Lorêm ipsum dolor sît amet çîvibùs")]
    public void Base64_RoundTrips_WithEveryEncoding(string value)
    {
        foreach (var encoding in new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.UTF32, Encoding.Latin1 })
            Assert.AreEqual(value, value.ToBase64String(encoding).FromBase64String(encoding), encoding.WebName);
    }

    [TestMethod]
    public void FromBase64String_DefaultsToUtf8_AndRejectsNullAndInvalidInput()
    {
        Assert.AreEqual("café", "Y2Fmw6k=".FromBase64String());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).FromBase64String());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).ToBase64String());
        Assert.ThrowsExactly<FormatException>(() => "not base64!".FromBase64String());
    }

    /// <summary>
    /// Documents a property of the chosen encoding rather than of the library: with ASCII, characters outside the range are lossy (<c>?</c>), which is why UTF-8 is the default.
    /// </summary>
    [TestMethod]
    public void ToBase64String_Ascii_ReplacesNonAsciiWithQuestionMark()
        => Assert.AreEqual("TG9yP20=", "Lorêm".ToBase64String(Encoding.ASCII));

    #endregion

    #region RemoveDiacritics

    [TestMethod]
    public void RemoveDiacritics_OnNullOrEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, ((string?)null).RemoveDiacritics());
        Assert.AreEqual(string.Empty, string.Empty.RemoveDiacritics());
    }

    [TestMethod]
    [DataRow("sébastien", "sebastien")]
    [DataRow("sébas-tien", "sebas-tien")]
    [DataRow("aàâaáäãåā", "aaaaaaaaa")]
    [DataRow("cçćč", "cccc")]
    [DataRow("eèéêëēėę", "eeeeeeee")]
    [DataRow("nñńň", "nnnn")]
    [DataRow("sšśș", "ssss")]
    [DataRow("uùúûüūů", "uuuuuuu")]
    [DataRow("AÀÂÁÄÃÅĀ", "AAAAAAAA")]
    [DataRow("ÇĆČ", "CCC")]
    [DataRow("plain ascii 123", "plain ascii 123")]
    public void RemoveDiacritics_StripsAccents(string input, string expected)
        => Assert.AreEqual(expected, input.RemoveDiacritics());

    #endregion
}
