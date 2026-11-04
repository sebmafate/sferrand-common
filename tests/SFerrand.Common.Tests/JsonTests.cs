using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SFerrand.Common.Extensions;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of the <see cref="System.Text.Json.JsonSerializer"/> shortcuts and of <see cref="Extensions.EnumMemberJsonConverter{TEnum}"/>
/// (names, <c>EnumMember</c> values, numbers, dictionary keys, nullable and nested usage).
/// </summary>
[TestClass]
public class JsonTests
{
    private const string Compact = """{"Name":"Name","Value":100}""";

    private sealed class Sample
    {
        public string? Name { get; set; }
        public int Value { get; set; }
    }

    [TestMethod]
    public void ToJsonString_OnNull_ReturnsBraces()
        => Assert.AreEqual("{}", ((object?)null).ToJsonString());

    [TestMethod]
    public void ToJsonString_DefaultsToIndentedOutput()
    {
        var json = new Sample { Name = "Name", Value = 100 }.ToJsonString();

        Assert.Contains(Environment.NewLine, json);
        Assert.AreEqual(Compact, json.Replace(Environment.NewLine, "").Replace(" ", ""));
    }

    [TestMethod]
    public void ToJsonString_HonoursOptions_AndAnonymousTypes()
        => Assert.AreEqual(Compact, new { Name = "Name", Value = 100 }.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));

    [TestMethod]
    public void ToObject_OnNull_ReturnsTheDefaultValue()
    {
        var fallback = new Sample();
        Assert.AreSame(fallback, ((string?)null).ToObject(fallback));
    }

    [TestMethod]
    public void ToObject_Deserializes()
    {
        var sample = Compact.ToObject<Sample>();

        Assert.IsNotNull(sample);
        Assert.AreEqual("Name", sample.Name);
        Assert.AreEqual(100, sample.Value);
    }

    /// <summary>
    /// The default-value parameter exists so the compiler can infer an anonymous type from a template instance.
    /// </summary>
    [TestMethod]
    public void ToObject_InfersAnonymousTypes()
    {
        var shape = new { Name = "", Value = 0 };
        var result = Compact.ToObject(shape);

        Assert.IsNotNull(result);
        Assert.AreEqual(100, result.Value);
    }

    [TestMethod]
    public void ToObject_HonoursOptions()
    {
        var sample = """{"name":"n","value":1}""".ToObject<Sample>(options: new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.AreEqual("n", sample?.Name);
    }

    [TestMethod]
    public void ToObject_OnInvalidJson_Throws()
        => Assert.ThrowsExactly<JsonException>(() => "not json".ToObject<Sample>());

    #region EnumMemberJsonConverter

    [JsonConverter(typeof(EnumMemberJsonConverter<Status>))]
    public enum Status
    {
        Unknown = 0,
        Open = 1,
        [EnumMember(Value = "in_progress")] InProgress = 2,
        [EnumMember(Value = "done")] Closed = 3,
    }

    public sealed record Ticket(Status Status, Status? Previous = null);

    [TestMethod]
    [DataRow(Status.Open, "\"Open\"")]
    [DataRow(Status.InProgress, "\"in_progress\"")]
    [DataRow(Status.Closed, "\"done\"")]
    public void Enum_Write_UsesEnumMemberValueWhenPresent(Status value, string expected)
        => Assert.AreEqual(expected, JsonSerializer.Serialize(value));

    [TestMethod]
    [DataRow("\"Open\"", Status.Open)]
    [DataRow("\"open\"", Status.Open)]
    [DataRow("\"in_progress\"", Status.InProgress)]
    [DataRow("\"InProgress\"", Status.InProgress)]
    [DataRow("\"done\"", Status.Closed)]
    [DataRow("\"Closed\"", Status.Closed)]
    [DataRow("2", Status.InProgress)]
    public void Enum_Read_AcceptsNameEnumMemberValueAndNumber(string json, Status expected)
        => Assert.AreEqual(expected, JsonSerializer.Deserialize<Status>(json));

    [TestMethod]
    [DataRow("\"bogus\"")]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("null")]
    public void Enum_Read_RejectsUnknownValues(string json)
        => Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<Status>(json));

    [TestMethod]
    public void Enum_Write_UndefinedValue_FallsBackToNumber()
        => Assert.AreEqual("42", JsonSerializer.Serialize((Status)42));

    /// <summary>
    /// Documents an asymmetry on purpose: writing never fails (a log or a response must not break on an unexpected value), but reading
    /// refuses numbers that are not defined members, so a corrupted value is never silently accepted as valid.
    /// </summary>
    [TestMethod]
    public void Enum_UndefinedValue_IsWrittenAsAKeyNumber_ButRejectedWhenReadBack()
    {
        var json = JsonSerializer.Serialize(new Dictionary<Status, int> { [(Status)42] = 1 });

        Assert.AreEqual("""{"42":1}""", json);
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<Dictionary<Status, int>>(json));
    }

    [TestMethod]
    public void Enum_RoundTrips_InsideObjects_AndNullables()
    {
        var json = JsonSerializer.Serialize(new Ticket(Status.InProgress, Status.Closed));
        Assert.AreEqual("""{"Status":"in_progress","Previous":"done"}""", json);
        Assert.AreEqual(new Ticket(Status.InProgress, Status.Closed), JsonSerializer.Deserialize<Ticket>(json));
        Assert.AreEqual(new Ticket(Status.Open), JsonSerializer.Deserialize<Ticket>("""{"Status":"Open","Previous":null}"""));
    }

    [TestMethod]
    public void Enum_WorksAsDictionaryKey()
    {
        var json = JsonSerializer.Serialize(new Dictionary<Status, int> { [Status.InProgress] = 1, [Status.Closed] = 2 });
        Assert.AreEqual("""{"in_progress":1,"done":2}""", json);

        var back = JsonSerializer.Deserialize<Dictionary<Status, int>>(json);
        Assert.AreEqual(2, back![Status.Closed]);
    }

    #endregion
}
