using System.Text.Json;
using SFerrand.Common.Security;

namespace SFerrand.Common.Tests;

/// <summary>
/// Tests of <see cref="SensitiveDataAttribute"/> and <see cref="SensitiveDataJson"/>: log-safe serialization of objects, one case per kind
/// of masking, the behaviour on non-string properties, inheritance, nesting, records and fields, and the guarantee that
/// normal serialization is untouched.
/// </summary>
[TestClass]
public class SensitiveDataTests
{
    public class Account
    {
        public string UserName { get; set; } = "";
        [SensitiveData] public string? Password { get; set; }
        [SensitiveData(SensitiveDataKind.Email)] public string? Email { get; set; }
        [SensitiveData(SensitiveDataKind.Token)] public string? ApiToken { get; set; }
        [SensitiveData(SensitiveDataKind.Uri)] public string? Callback { get; set; }
        [SensitiveData(SensitiveDataKind.Guid)] public string? Reference { get; set; }
        [SensitiveData(SensitiveDataKind.CardNumber)] public string? Card { get; set; }
        [SensitiveData(SensitiveDataKind.Iban)] public string? Iban { get; set; }
        [SensitiveData(SensitiveDataKind.Rib)] public string? Rib { get; set; }
        [SensitiveData(SensitiveDataKind.FrenchNir)] public string? Nir { get; set; }
    }

    public class WithOtherTypes
    {
        public string Name { get; set; } = "";
        [SensitiveData] public int Pin { get; set; }
        [SensitiveData] public int? OptionalPin { get; set; }
        [SensitiveData] public string[] Codes { get; set; } = Array.Empty<string>();
        [SensitiveData] public Account? Hidden { get; set; }
    }

    public class Parent
    {
        [SensitiveData] public string? Secret { get; set; }
    }

    public class Child : Parent
    {
        public string Plain { get; set; } = "";
    }

    public class VirtualBase
    {
        [SensitiveData] public virtual string? Secret { get; set; }
    }

    public class OverridingChild : VirtualBase
    {
        public override string? Secret { get; set; }   // does not repeat the attribute
    }

    public class Holder
    {
        public Account? Account { get; set; }
        public List<Account> Others { get; set; } = new();
        public Dictionary<string, Account> ByName { get; set; } = new();
    }

    public class WithField
    {
        [SensitiveData] public string? Key = "abcdefghijklmnop";
    }

    public record Positional([property: SensitiveData] string Password, string Name);

    private static Account Full() => new()
    {
        UserName = "ada",
        Password = "hunter2hunter2",
        Email = "ada@example.com",
        ApiToken = "ghp_abcdefghijklmnopqrstuvwxyz1",
        Callback = "https://app:pw12345678@example.com/cb",
        Reference = "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
        Card = "4111 1111 1111 1111",
        Iban = "FR76 3000 6000 0112 3456 7890 189",
        Rib = "30006 00001 12345678901 89",
        Nir = "1 84 12 76 451 089 46",
    };

    [TestMethod]
    public void EachKind_MasksItsPropertyWithTheMatchingObfuscator()
    {
        var json = JsonDocument.Parse(Full().ToSafeJsonString()).RootElement;

        Assert.AreEqual("ada", json.GetProperty("UserName").GetString());
        Assert.AreEqual("hu**********r2", json.GetProperty("Password").GetString());
        Assert.AreEqual("***@example.com", json.GetProperty("Email").GetString());
        Assert.AreEqual("ghp_abcd*******************xyz1", json.GetProperty("ApiToken").GetString());
        Assert.AreEqual("https://app:pw******78@example.com/cb", json.GetProperty("Callback").GetString());
        Assert.AreEqual("3******0-****-11d3-****-03********01", json.GetProperty("Reference").GetString());
        Assert.AreEqual("**** **** **** 1111", json.GetProperty("Card").GetString());
        Assert.AreEqual("FR** **** **** **** **** ***0 189", json.GetProperty("Iban").GetString());
        Assert.AreEqual("30006 ***** *********** **", json.GetProperty("Rib").GetString());
        Assert.AreEqual("* ** ** ** *** *** **", json.GetProperty("Nir").GetString());
    }

    [TestMethod]
    public void TheAttributeItself_MasksLikeTheObfuscatorOfItsKind()
    {
        Assert.AreEqual("hu**********r2", new SensitiveDataAttribute().Mask("hunter2hunter2"));
        Assert.AreEqual("**** **** **** 1111", new SensitiveDataAttribute(SensitiveDataKind.CardNumber).Mask("4111 1111 1111 1111"));
        Assert.IsNull(new SensitiveDataAttribute().Mask(null));
        Assert.AreEqual(string.Empty, new SensitiveDataAttribute(SensitiveDataKind.Email).Mask(string.Empty));
    }

    /// <summary>The secret must not appear anywhere in the log text, not even in another property or in the key names.</summary>
    [TestMethod]
    public void TheSecretValues_NeverAppearInTheSafeOutput()
    {
        var text = Full().ToSafeJsonString();

        foreach (var secret in new[] { "hunter2hunter2", "ada@example.com", "pw12345678", "4111 1111 1111 1111", "12345678901", "3000 6000 0112", "84 12 76 451" })
            Assert.DoesNotContain(secret, text);
    }

    [TestMethod]
    public void NullAndEmptyValues_StayNullAndEmpty()
    {
        var json = JsonDocument.Parse(new Account { UserName = "x", Password = null, Email = "" }.ToSafeJsonString()).RootElement;

        Assert.AreEqual(JsonValueKind.Null, json.GetProperty("Password").ValueKind);
        Assert.AreEqual(string.Empty, json.GetProperty("Email").GetString());
    }

    [TestMethod]
    public void NormalSerialization_IsNotAffected()
    {
        var json = JsonSerializer.Serialize(Full());

        StringAssert.Contains(json, "hunter2hunter2");
        StringAssert.Contains(json, "4111 1111 1111 1111");
    }

    /// <summary>
    /// A number, an array or a nested object has no meaningful partial masking: it becomes the constant string <c>"***"</c>,
    /// and a <c>null</c> stays <c>null</c> (the converter is not called for it).
    /// </summary>
    [TestMethod]
    public void SensitivePropertiesOfOtherTypes_BecomeAConstant()
    {
        var json = JsonDocument.Parse(new WithOtherTypes
        {
            Name = "n",
            Pin = 1234,
            OptionalPin = 5678,
            Codes = new[] { "a", "b" },
            Hidden = Full(),
        }.ToSafeJsonString()).RootElement;

        Assert.AreEqual("n", json.GetProperty("Name").GetString());
        Assert.AreEqual("***", json.GetProperty("Pin").GetString());
        Assert.AreEqual("***", json.GetProperty("OptionalPin").GetString());
        Assert.AreEqual("***", json.GetProperty("Codes").GetString());
        Assert.AreEqual("***", json.GetProperty("Hidden").GetString());
    }

    [TestMethod]
    public void ANullSensitiveNullable_StaysNull()
    {
        var json = JsonDocument.Parse(new WithOtherTypes { OptionalPin = null, Hidden = null }.ToSafeJsonString()).RootElement;

        Assert.AreEqual(JsonValueKind.Null, json.GetProperty("OptionalPin").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, json.GetProperty("Hidden").ValueKind);
    }

    [TestMethod]
    public void SensitiveProperties_AreHonouredThroughInheritance()
    {
        var json = JsonDocument.Parse(new Child { Plain = "visible", Secret = "abcdefghijklmnop" }.ToSafeJsonString()).RootElement;

        Assert.AreEqual("visible", json.GetProperty("Plain").GetString());
        Assert.AreEqual("ab************op", json.GetProperty("Secret").GetString());
    }

    /// <summary>
    /// A property that is overridden without repeating the attribute is still the same sensitive property. <c>MemberInfo.GetCustomAttributes</c>
    /// ignores <c>inherit</c> for properties, so the first version of the modifier would have written it in clear.
    /// </summary>
    [TestMethod]
    public void AnOverriddenSensitiveProperty_StaysMasked()
    {
        var json = JsonDocument.Parse(new OverridingChild { Secret = "abcdefghijklmnop" }.ToSafeJsonString()).RootElement;

        Assert.AreEqual("ab************op", json.GetProperty("Secret").GetString());
    }

    [TestMethod]
    public void SensitiveProperties_AreMaskedWhenNestedInObjectsListsAndDictionaries()
    {
        var holder = new Holder
        {
            Account = Full(),
            Others = new() { Full(), new Account { UserName = "bob", Password = "secret-secret-1" } },
            ByName = new() { ["ada"] = Full() },
        };

        var text = holder.ToSafeJsonString();

        Assert.DoesNotContain("hunter2hunter2", text);
        Assert.DoesNotContain("secret-secret-1", text);
        Assert.DoesNotContain("ada@example.com", text);
        StringAssert.Contains(text, "\"UserName\":\"bob\"");
    }

    [TestMethod]
    public void Fields_AreMasked_WhenTheOptionsIncludeThem()
    {
        var options = SensitiveDataJson.CreateOptions(new JsonSerializerOptions { IncludeFields = true });

        Assert.AreEqual("""{"Key":"ab************op"}""", new WithField().ToSafeJsonString(options));
    }

    [TestMethod]
    public void PositionalRecords_AreMasked_WhenTheAttributeTargetsTheProperty()
        => Assert.AreEqual("""{"Password":"hu**********r2","Name":"ada"}""", new Positional("hunter2hunter2", "ada").ToSafeJsonString());

    /// <summary>
    /// The attribute is not allowed on a constructor parameter, on purpose: on a positional record it would stay on the parameter and the
    /// property would be serialized in clear. Writing <c>[SensitiveData] string Password</c> in a record header must not compile, so the
    /// mistake is caught at build time. (A negative compile test is not possible here: this documents the rule through the attribute metadata.)
    /// </summary>
    [TestMethod]
    public void TheAttribute_IsNotAllowedOnParameters()
    {
        var usage = (AttributeUsageAttribute)Attribute.GetCustomAttribute(typeof(SensitiveDataAttribute), typeof(AttributeUsageAttribute))!;

        Assert.AreEqual(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
    }

    [TestMethod]
    public void CreateOptions_CopiesTheTemplate_WithoutChangingIt()
    {
        var template = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

        var safe = new Positional("hunter2hunter2", "ada").ToSafeJsonString(SensitiveDataJson.CreateOptions(template));
        var plain = JsonSerializer.Serialize(new Positional("hunter2hunter2", "ada"), template);

        Assert.AreEqual("""{"password":"hu**********r2","name":"ada"}""", safe);
        Assert.AreEqual("""{"password":"hunter2hunter2","name":"ada"}""", plain);   // the template still serializes in clear
    }

    [TestMethod]
    public void NullObject_IsWrittenAsNull()
        => Assert.AreEqual("null", ((Account?)null).ToSafeJsonString());

    /// <summary>Metadata built by hand (a custom resolver) has no reflection attributes to read: the modifier must leave such properties alone.</summary>
    [TestMethod]
    public void Modifier_LeavesPropertiesWithoutAttributeMetadataAlone()
    {
        var options = new JsonSerializerOptions();
        var typeInfo = System.Text.Json.Serialization.Metadata.JsonTypeInfo.CreateJsonTypeInfo<Account>(options);
        var property = typeInfo.CreateJsonPropertyInfo(typeof(string), "Password");
        property.Get = _ => "hunter2hunter2";
        typeInfo.Properties.Add(property);

        SensitiveDataJson.Modifier(typeInfo);

        Assert.AreEqual("hunter2hunter2", property.Get!(new Account()));
    }

    [TestMethod]
    public void Modifier_RejectsANullTypeInfo()
        => Assert.ThrowsExactly<ArgumentNullException>(() => SensitiveDataJson.Modifier(null!));

    /// <summary>
    /// The output is for reading, not for round trips: a masked string reads back as its masked text, and any other sensitive
    /// property refuses to be read.
    /// </summary>
    [TestMethod]
    public void ReadingBack_IsNotMeantToWork()
    {
        var options = SensitiveDataJson.CreateOptions();

        var account = JsonSerializer.Deserialize<Account>("""{"Password":"hu**********r2"}""", options);
        Assert.AreEqual("hu**********r2", account!.Password);

        Assert.ThrowsExactly<NotSupportedException>(() => JsonSerializer.Deserialize<WithOtherTypes>("""{"Pin":1234}""", options));
    }
}
