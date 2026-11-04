using System.Text;
using SFerrand.Common.Extensions;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of the Base64url helpers: the RFC 4648 test vectors, the URL-safe alphabet, padding rules, strict rejection of anything else,
/// round trips on random data and, where the framework offers it (.NET 9+), a comparison with <c>System.Buffers.Text.Base64Url</c>.
/// </summary>
[TestClass]
public class Base64UrlTests
{
    /// <summary>The test vectors of RFC 4648 section 10, without padding (the unpadded form is the Base64url norm).</summary>
    [TestMethod]
    [DataRow("", "")]
    [DataRow("f", "Zg")]
    [DataRow("fo", "Zm8")]
    [DataRow("foo", "Zm9v")]
    [DataRow("foob", "Zm9vYg")]
    [DataRow("fooba", "Zm9vYmE")]
    [DataRow("foobar", "Zm9vYmFy")]
    public void Rfc4648Vectors_RoundTrip(string text, string encoded)
    {
        Assert.AreEqual(encoded, text.ToBase64UrlString());
        Assert.AreEqual(text, encoded.FromBase64UrlString());
    }

    /// <summary>0xFB 0xFF encodes to <c>+/8=</c> in standard Base64: the two characters that must change, plus the padding that must go.</summary>
    [TestMethod]
    public void Encoding_UsesTheUrlSafeAlphabet_AndNoPadding()
    {
        Assert.AreEqual("-_8", new byte[] { 0xFB, 0xFF }.ToBase64UrlString());
        Assert.AreEqual("-_-_", new byte[] { 0xFB, 0xFF, 0xBF }.ToBase64UrlString());
        Assert.AreEqual(string.Empty, Array.Empty<byte>().ToBase64UrlString());
    }

    [TestMethod]
    public void Encoding_UsesUtf8ByDefault_AndHonoursAnotherEncoding()
    {
        Assert.AreEqual("Y2Fmw6k", "café".ToBase64UrlString());
        Assert.AreEqual("Y2Fm6Q", "café".ToBase64UrlString(Encoding.Latin1));
        Assert.AreEqual("café", "Y2Fm6Q".FromBase64UrlString(Encoding.Latin1));
    }

    [TestMethod]
    [DataRow("Zg")]
    [DataRow("Zg==")]       // padding is accepted when it is correct
    [DataRow("Zm8")]
    [DataRow("Zm8=")]
    [DataRow("Zm9v")]
    [DataRow("-_8")]
    [DataRow("-_8=")]
    public void Decoding_AcceptsPaddedAndUnpaddedInput(string encoded)
    {
        Assert.IsTrue(encoded.IsBase64UrlString());
        Assert.IsNotNull(encoded.FromBase64UrlBytes());
    }

    [TestMethod]
    [DataRow("a")]           // one leftover character cannot encode a byte
    [DataRow("Zm9vY")]
    [DataRow("Zg=")]         // wrong amount of padding
    [DataRow("Zm8==")]
    [DataRow("Zg===")]
    [DataRow("Zg=a")]        // '=' in the middle
    [DataRow("=Zg")]
    [DataRow("=")]
    [DataRow("====")]
    [DataRow("Zm9v ")]       // whitespace
    [DataRow(" Zm9v")]
    [DataRow("Zm 9v")]
    [DataRow("Zm9v\n")]
    [DataRow("+/8=")]        // the standard alphabet is not Base64url
    [DataRow("Zm+v")]
    [DataRow("Zm/v")]
    [DataRow("Zm9é")]
    public void Decoding_RejectsEverythingElse(string encoded)
    {
        Assert.IsFalse(encoded.IsBase64UrlString());
        Assert.ThrowsExactly<FormatException>(() => encoded.FromBase64UrlBytes());
    }

    /// <summary>The empty string decodes to an empty array (so encoding then decoding an empty payload works) but is not reported as Base64url, like <c>IsBase64String</c>.</summary>
    [TestMethod]
    public void EmptyString_DecodesToNothing_ButIsNotReportedAsBase64Url()
    {
        Assert.IsEmpty(string.Empty.FromBase64UrlBytes());
        Assert.IsFalse(string.Empty.IsBase64UrlString());
    }

    [TestMethod]
    public void NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ((byte[])null!).ToBase64UrlString());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).ToBase64UrlString());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).FromBase64UrlBytes());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).FromBase64UrlString());
        Assert.ThrowsExactly<ArgumentNullException>(() => ((string)null!).IsBase64UrlString());
    }

    [TestMethod]
    public void RandomBytes_RoundTrip_WithoutAnyCharacterOfTheStandardAlphabet()
    {
        var random = new Random(20260503);
        for (var i = 0; i < 3000; i++)
        {
            var bytes = new byte[random.Next(0, 70)];
            random.NextBytes(bytes);

            var encoded = bytes.ToBase64UrlString();

            Assert.DoesNotContain("=", encoded);
            Assert.DoesNotContain("+", encoded);
            Assert.DoesNotContain("/", encoded);
            CollectionAssert.AreEqual(bytes, encoded.FromBase64UrlBytes());
            Assert.AreEqual(Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'), encoded);
            if (bytes.Length > 0)
                Assert.IsTrue(encoded.IsBase64UrlString());
        }
    }

#if NET9_0_OR_GREATER
    /// <summary>Where the framework has its own implementation, ours must produce and accept exactly what it does.</summary>
    [TestMethod]
    public void AgreesWithTheFrameworkImplementation()
    {
        var random = new Random(20260504);
        for (var i = 0; i < 3000; i++)
        {
            var bytes = new byte[random.Next(1, 70)];
            random.NextBytes(bytes);

            var encoded = bytes.ToBase64UrlString();

            Assert.AreEqual(System.Buffers.Text.Base64Url.EncodeToString(bytes), encoded);
            CollectionAssert.AreEqual(System.Buffers.Text.Base64Url.DecodeFromChars(encoded), encoded.FromBase64UrlBytes());
        }
    }
#endif
}
