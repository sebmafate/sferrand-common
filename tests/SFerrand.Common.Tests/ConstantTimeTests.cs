using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Functional tests of <see cref="ConstantTime"/>: what it reports as equal. The "constant time" property itself is not tested here: a
/// timing assertion on a shared CI machine would be flaky, and it is delegated to the framework primitive
/// (<see cref="System.Security.Cryptography.CryptographicOperations.FixedTimeEquals"/>) the class is a thin, null-safe layer over.
/// </summary>
[TestClass]
public class ConstantTimeTests
{
    [TestMethod]
    [DataRow("s3cr3t-token", "s3cr3t-token", true)]
    [DataRow("s3cr3t-token", "s3cr3t-tokeN", false)]    // last character differs
    [DataRow("s3cr3t-token", "S3cr3t-token", false)]    // first character differs, and the comparison is case sensitive
    [DataRow("s3cr3t-token", "s3cr3t-toke", false)]     // prefix of the other
    [DataRow("s3cr3t-toke", "s3cr3t-token", false)]
    [DataRow("", "", true)]
    [DataRow("", "x", false)]
    [DataRow("café", "café", true)]
    [DataRow("café", "cafe", false)]
    public void AreEqual_Strings_ComparesTheContentOrdinally(string left, string right, bool expected)
        => Assert.AreEqual(expected, ConstantTime.AreEqual(left, right));

    /// <summary>
    /// A missing secret must never authenticate a missing credential: <c>null</c> is equal to nothing, <c>null</c> included.
    /// </summary>
    [TestMethod]
    public void AreEqual_Null_IsNeverEqualToAnything()
    {
        string? missing = null;

        Assert.IsFalse(ConstantTime.AreEqual(missing, missing));
        Assert.IsFalse(ConstantTime.AreEqual(missing, ""));
        Assert.IsFalse(ConstantTime.AreEqual("", missing));
        Assert.IsFalse(ConstantTime.AreEqual(missing, "x"));
    }

    [TestMethod]
    public void AreEqual_Bytes_ComparesTheContent()
    {
        Assert.IsTrue(ConstantTime.AreEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3 }));
        Assert.IsFalse(ConstantTime.AreEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 4 }));
        Assert.IsFalse(ConstantTime.AreEqual(new byte[] { 1, 2, 3 }, new byte[] { 1, 2 }));
        Assert.IsTrue(ConstantTime.AreEqual(ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void AreEqual_Strings_AgreesWithOrdinalEqualityOnRandomValues()
    {
        var random = new Random(20260502);
        const string alphabet = "abcAB01é-_/";
        for (var i = 0; i < 3000; i++)
        {
            var left = string.Concat(Enumerable.Range(0, random.Next(0, 8)).Select(_ => alphabet[random.Next(alphabet.Length)]));
            var right = random.Next(3) == 0
                ? left
                : string.Concat(Enumerable.Range(0, random.Next(0, 8)).Select(_ => alphabet[random.Next(alphabet.Length)]));

            Assert.AreEqual(string.Equals(left, right, StringComparison.Ordinal), ConstantTime.AreEqual(left, right), $"'{left}' / '{right}'");
        }
    }
}
