namespace SFerrand.Common.Security;

/// <summary>The kind of value a <see cref="SensitiveDataAttribute"/> protects; it selects how the value is masked.</summary>
public enum SensitiveDataKind
{
    /// <summary>Any secret (password, key...): <see cref="SecretObfuscator.Obfuscate"/>. The default.</summary>
    Secret,

    /// <summary>An API token: <see cref="SecretObfuscator.ObfuscateToken"/> (a well-known prefix stays visible).</summary>
    Token,

    /// <summary>An e-mail address: <see cref="SecretObfuscator.ObfuscateEmail"/>.</summary>
    Email,

    /// <summary>A URI that may carry credentials: <see cref="SecretObfuscator.ObfuscateUri(string?)"/>.</summary>
    Uri,

    /// <summary>A GUID: <see cref="SecretObfuscator.ObfuscateGuid(string?, int?, int?, char)"/>.</summary>
    Guid,

    /// <summary>A payment card number: <see cref="FinancialObfuscator.ObfuscateCardNumber"/>.</summary>
    CardNumber,

    /// <summary>An IBAN: <see cref="FinancialObfuscator.ObfuscateIban"/>.</summary>
    Iban,

    /// <summary>A French RIB: <see cref="FinancialObfuscator.ObfuscateRib"/>.</summary>
    Rib,

    /// <summary>A French social security number: <see cref="FrenchIdentifierObfuscator.ObfuscateNir"/>.</summary>
    FrenchNir,
}

/// <summary>
/// Marks a property or field whose value must never appear in clear in logs. It is honoured by
/// <see cref="SensitiveDataJson"/> when an object is serialized for logging, and exposes <see cref="Mask"/> for your own code.
/// </summary>
/// <remarks>
/// The attribute does nothing by itself: it is a declaration read by the log-safe serializer. It does not affect normal
/// serialization, <c>ToString</c> or any other code path.
/// <para>
/// On a positional record, write <c>[property: SensitiveData] string Password</c>. The attribute is deliberately <b>not</b> allowed on a
/// constructor parameter: it would compile and stay on the parameter, the property would be serialized in clear, and nothing would tell
/// you. Refusing it makes the compiler point at the mistake.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class Login
/// {
///     public string UserName { get; set; } = "";
///     [SensitiveData] public string? Password { get; set; }
///     [SensitiveData(SensitiveDataKind.Email)] public string? Email { get; set; }
/// }
///
/// var login = new Login { UserName = "ada", Password = "hunter2hunter2", Email = "ada@example.com" };
/// logger.LogInformation("Login: {Login}", login.ToSafeJsonString());
/// // Login: {"UserName":"ada","Password":"hu**********r2","Email":"***@example.com"}
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveDataAttribute : Attribute
{
    /// <summary>Creates the attribute.</summary>
    /// <param name="kind">How the value is masked. Default: <see cref="SensitiveDataKind.Secret"/>.</param>
    public SensitiveDataAttribute(SensitiveDataKind kind = SensitiveDataKind.Secret) => Kind = kind;

    /// <summary>How the value is masked.</summary>
    public SensitiveDataKind Kind { get; }

    /// <summary>Masks a value according to <see cref="Kind"/>.</summary>
    /// <param name="value">The value. <see langword="null"/> and empty values are returned unchanged.</param>
    /// <returns>The masked value, with the same length as <paramref name="value"/>.</returns>
    public string? Mask(string? value) => Kind switch
    {
        SensitiveDataKind.Token => value.ObfuscateToken(),
        SensitiveDataKind.Email => value.ObfuscateEmail(),
        SensitiveDataKind.Uri => value.ObfuscateUri(),
        SensitiveDataKind.Guid => value.ObfuscateGuid(),
        SensitiveDataKind.CardNumber => value.ObfuscateCardNumber(),
        SensitiveDataKind.Iban => value.ObfuscateIban(),
        SensitiveDataKind.Rib => value.ObfuscateRib(),
        SensitiveDataKind.FrenchNir => value.ObfuscateNir(),
        _ => value.Obfuscate(),
    };
}
