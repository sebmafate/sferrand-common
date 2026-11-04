using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using SFerrand.Common.Attributes;
using SFerrand.Common.Extensions;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="Attributes.NotEmptyEnumerableAttribute"/> through the real <see cref="System.ComponentModel.DataAnnotations.Validator"/>,
/// and of the configuration source ordering helper (with real temporary JSON files, not mocks).
/// </summary>
[TestClass]
public class ValidationAndConfigurationTests
{
    private sealed class Model
    {
        [NotEmptyEnumerable]
        public IEnumerable<string>? Items { get; set; }
    }

    private sealed class CustomMessageModel
    {
        [NotEmptyEnumerable(ErrorMessage = "pick at least one")]
        public IEnumerable<int>? Numbers { get; set; }
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    #region NotEmptyEnumerable

    [TestMethod]
    public void NotEmptyEnumerable_WithElements_IsValid()
        => Assert.IsEmpty(Validate(new Model { Items = new[] { "a" } }));

    /// <summary>
    /// The attribute only needs to know that one element exists: an infinite or expensive sequence must be accepted after reading a single item.
    /// </summary>
    [TestMethod]
    public void NotEmptyEnumerable_WithLazySequence_IsValidWithoutFullEnumeration()
    {
        var enumerated = 0;
        IEnumerable<string> Lazy()
        {
            while (true) { enumerated++; yield return "x"; }
        }

        Assert.IsEmpty(Validate(new Model { Items = Lazy() }));
        Assert.AreEqual(1, enumerated);
    }

    [TestMethod]
    public void NotEmptyEnumerable_Empty_IsInvalid()
    {
        var results = Validate(new Model { Items = Array.Empty<string>() });

        Assert.HasCount(1, results);
        Assert.AreEqual("Items can not be null or empty", results[0].ErrorMessage);
        CollectionAssert.AreEqual(new[] { nameof(Model.Items) }, results[0].MemberNames.ToArray());
    }

    [TestMethod]
    public void NotEmptyEnumerable_Null_IsInvalid()
        => Assert.HasCount(1, Validate(new Model { Items = null }));

    [TestMethod]
    public void NotEmptyEnumerable_WithoutMemberName_ReportsNoMembers()
    {
        var context = new ValidationContext(new object()) { DisplayName = "Thing" };

        var result = new NotEmptyEnumerableAttribute().GetValidationResult(Array.Empty<int>(), context);

        Assert.IsNotNull(result);
        Assert.AreEqual("Thing can not be null or empty", result.ErrorMessage);
        Assert.IsEmpty(result.MemberNames);
    }

    [TestMethod]
    public void NotEmptyEnumerable_OnANonEnumerable_IsInvalid()
        => Assert.IsNotNull(new NotEmptyEnumerableAttribute().GetValidationResult(42, new ValidationContext(new object()) { DisplayName = "Thing" }));

    [TestMethod]
    public void NotEmptyEnumerable_CustomErrorMessage_IsUsed()
        => Assert.AreEqual("pick at least one", Validate(new CustomMessageModel { Numbers = Array.Empty<int>() })[0].ErrorMessage);

    #endregion

    #region PrioritizeAppSettings

    [TestMethod]
    public void PrioritizeAppSettings_MovesJsonFileSourcesLast_KeepingTheirOrder()
    {
        var builder = new ConfigurationBuilder()
            .AddJsonFile("a.json", optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["k"] = "v" })
            .AddJsonFile("b.json", optional: true)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["k2"] = "v2" });

        builder.PrioritizeAppSettings();

        var kinds = builder.Sources.Select(s => s.GetType()).ToArray();
        Assert.AreEqual(typeof(JsonConfigurationSource), kinds[^1]);
        Assert.AreEqual(typeof(JsonConfigurationSource), kinds[^2]);
        CollectionAssert.AreEqual(
            new[] { "a.json", "b.json" },
            builder.Sources.OfType<JsonConfigurationSource>().Select(s => s.Path).ToArray());
    }

    /// <summary>
    /// End-to-end proof, not just a source-order check: the same key is defined by the JSON file and by a source registered after it;
    /// after reordering, the JSON value is the one the built configuration returns.
    /// </summary>
    [TestMethod]
    public void PrioritizeAppSettings_LetsTheJsonFileWinOverEarlierSources()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "appsettings.json"), """{"Setting":"from-json"}""");

            var configuration = new ConfigurationBuilder()
                .SetBasePath(dir.FullName)
                .AddJsonFile("appsettings.json", optional: false)
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Setting"] = "from-memory" })
                .PrioritizeAppSettings()
                .Build();

            Assert.AreEqual("from-json", configuration["Setting"]);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void PrioritizeAppSettings_WithoutJsonSources_IsANoOp()
    {
        var builder = new ConfigurationBuilder().AddInMemoryCollection();
        Assert.AreSame(builder, builder.PrioritizeAppSettings());
        Assert.HasCount(1, builder.Sources);
    }

    [TestMethod]
    public void PrioritizeAppSettings_OnNull_Throws()
        => Assert.ThrowsExactly<ArgumentNullException>(() => ((IConfigurationBuilder)null!).PrioritizeAppSettings());

    #endregion
}
