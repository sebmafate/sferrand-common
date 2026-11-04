# SFerrand.Common

Small, dependency-light helpers for .NET 8+, extracted from production services and rewritten to be
generic, null-safe and fully tested.

```
dotnet add package SFerrand.Common
```

| Area | What you get |
|---|---|
| **Log-safe masking** (`SFerrand.Common.Security`) | `SecretObfuscator`: mask secrets, URIs with credentials, connection strings, e-mails, GUIDs and API tokens while keeping them recognizable. |
| **Banking identifiers** (`SFerrand.Common.Security`) | `FinancialObfuscator`: mask payment card numbers (PCI DSS display rule), IBAN and French RIB, keeping their layout. |
| **Free-text redaction** (`SFerrand.Common.Security`) | `SecretRedactor`: find and mask credentials, tokens, JWTs, `password=` pairs, cards, IBAN, RIB, NIR and e-mails inside log lines and bodies. |
| **Sensitive data in objects, headers and URLs** (`SFerrand.Common.Security`) | `[SensitiveData]` + log-safe JSON, `SensitiveKeys` for header/dictionary/query-string values and URLs. |
| **French identifiers** (`SFerrand.Common.Security`) | `FrenchIdentifierObfuscator`: mask NIR (social security number), SIREN, SIRET and VAT number. |
| **Check digits** (`SFerrand.Common.Validation`) | `Checksums`: Luhn, IBAN mod 97, RIB key, SIREN, SIRET, NIR, French VAT number. |
| **Constant-time comparison** (`SFerrand.Common.Security`) | `ConstantTime.AreEqual` for tokens and signatures. |
| **Strings** (`SFerrand.Common.Extensions`) | PascalCase / camelCase / snake_case / CONSTANT_CASE / kebab-case / COBOL-CASE / Train-Case, Base64 and Base64url helpers, diacritics removal, null/empty fallbacks. |
| **JSON** | `ToJsonString` / `ToObject` shortcuts and `EnumMemberJsonConverter<T>` (string enums honouring `[EnumMember]`). |
| **Collections** (`SFerrand.Common.Collections`) | `GenericComparer<T>` (compare by key) and `FuncComparer<T>`, bulk `IList<T>` operations. |
| **Validation** (`SFerrand.Common.Attributes`) | `[NotEmptyEnumerable]` for any `IEnumerable`. |
| **Configuration** | `PrioritizeAppSettings()` so `appsettings*.json` wins over other sources. |

## Masking secrets

Every method is fail-safe: if the input does not have the expected shape, the value is masked **as a whole**
(only the few adaptive leading/trailing characters stay visible) rather than returned in clear, and nothing throws on malformed input.
Masking is deterministic and length-preserving: it is a logging aid, not a security boundary. By default the number of visible characters adapts
to the length (none up to 4 characters, 1 up to 8, 2 up to 16, 4 above) so short secrets are never exposed.

```csharp
using SFerrand.Common.Security;

"aSuperMegaLongSecret".Obfuscate();                                  // aSup************cret
"postgres://app:s3cr3t-pass@db:5432/shop".ObfuscateUri();            // postgres://app:s3*******ss@db:5432/shop (password masked)
"AccountName=acme;AccountKey=averysecretkey==".ObfuscateConnectionString("AccountKey");
                                                                     // AccountName=acme;AccountKey=av************==
"jean-claude.van-damme@example.com".ObfuscateEmail();                // ****-c****e.***-d***e@example.com
"3f2504e0-4f89-11d3-9a0c-0305e82c3301".ObfuscateGuid();             // 3******0-****-11d3-****-03********01
"ghp_1234567890abcdefghijklmnop".ObfuscateToken();                 // ghp_1234******************mnop (prefix kept)

// Skip addresses that are not personal data (shared mailboxes, public domains...)
"team-platform@example.com".ObfuscateEmail(shouldSkip: e => e.StartsWith("team-"));  // unchanged
```

`Obfuscate` never reveals a whole value: if the requested leading/trailing counts would cover it, it falls back to
the adaptive default.

## Masking banking identifiers

`FinancialObfuscator` follows the same rules (fail-safe, deterministic, length-preserving) and keeps the **layout** of the value:
spaces and hyphens stay where they are and do not count as characters. Each method has a hard ceiling on what it can reveal,
whatever the arguments.

```csharp
using SFerrand.Common.Security;

"4111 1111 1111 1111".ObfuscateCardNumber();                  // **** **** **** 1111      (default: last 4 digits)
"5555-5555-5555-4444".ObfuscateCardNumber(nbLeading: 6);      // 5555-55**-****-4444      (ceiling: first 6 + last 4)
"FR76 3000 6000 0112 3456 7890 189".ObfuscateIban();          // FR** **** **** **** **** ***0 189   (country + last 4)
"30006 00001 12345678901 89".ObfuscateRib();                  // 30006 ***** *********** **        (bank code only)
"30006 00001 12345678901 89".ObfuscateRib(nbTrailing: 3);     // 30006 ***** **********1 89
```

| Method | Accepts | Default shows | Ceiling |
|---|---|---|---|
| `ObfuscateCardNumber` | 12 to 19 digits | last 4 | first 6 + last 4 (PCI DSS) |
| `ObfuscateIban` | country + check digits + 11 to 30 letters/digits (15 to 34) | country code + last 4 | 4 + 4 |
| `ObfuscateRib` | 5 + 5 digits, 11 letters/digits, 2 digits (23) | bank code (5) | 10 + 4 |

A value of the wrong shape (a digit too many, a letter in a card number) is masked as a whole with the generic rule instead of being
treated as a card. The checksum (Luhn, mod 97) is **not** verified: a mistyped number is as sensitive as a valid one.
A card verification code (CVV/CVC) or a PIN should never reach a log; if one does, `Obfuscate()` hides a 3 or 4 digit code completely.

## Masking French identifiers

```csharp
"1 84 12 76 451 089 46".ObfuscateNir();         // * ** ** ** *** *** **     (nothing shown by default)
"732 829 320".ObfuscateSiren();                  // *** *** 320
"732 829 320 00074".ObfuscateSiret();            // *** *** *** *0074
"FR 44 732 829 320".ObfuscateFrenchVatNumber();  // FR ** *** *** 320
```

The social security number (NIR) encodes sex, birth year and month, department and commune. Its ceiling therefore stops before them: at most the
first character and the two key digits can ever be shown, whatever is asked. Same contract and layout handling as `FinancialObfuscator`.

## Check digits

```csharp
using SFerrand.Common.Validation;

"4111 1111 1111 1111".IsValidLuhn();                 // true
"FR14 2004 1010 0505 0001 3M02 606".IsValidIban();   // true
"20041 01005 0500013M026 06".IsValidRibKey();        // true (letters of the account are converted as the standard prescribes)
"732829320".IsValidSiren();                          // true
"35600000000001".IsValidSiret();                     // true (La Poste: digit sum a multiple of 5 instead of Luhn)
"1 84 12 76 451 089 46".IsValidNir();                // true (2A / 2B Corsican departments handled)
"FR44732829320".IsValidFrenchVatNumber();            // true
```

A passing check means **well-formed**, not "exists": a random number passes Luhn one time in ten. Use it to reject typos and to cut false positives,
never as proof of identity. Every method accepts `null` (answer: `false`), ignores spaces and hyphens, and never throws. The country-specific length of an IBAN is not checked.

## Masking secrets in free text

`SecretRedactor` reads text and masks what it recognizes, with the matching obfuscator, so the result has **exactly the same length**:

```csharp
"Connecting to postgres://app:s3cr3t-pass@db/shop with token=abcdef123456 for ada@example.com".Redact();
// Connecting to postgres://app:s3*******ss@db/shop with token=ab********56 for ***@example.com

"Authorization: Bearer abcdefghijklmnopqrstuv failed".Redact();   // Authorization: Bearer abcd**************stuv failed
"card 4111 1111 1111 1111 expires 12/30".Redact();                // card **** **** **** 1111 expires 12/30
"pay FR76 3000 6000 0112 3456 7890 189 AND more".Redact();        // pay FR** **** **** **** **** ***0 189 AND more
```

| Category (`RedactionKinds`) | Recognized |
|---|---|
| `UriCredentials` | the password of `scheme://user:password@host` (several URIs glued in one token are all handled) |
| `AuthorizationHeaders` | the credential after `Bearer`, `Basic`, `Digest`, `Token` (16 characters or more) |
| `Jwt` | JSON Web Tokens |
| `ApiTokens` | GitHub (`ghp_`, `github_pat_`...), AWS access key IDs, Slack tokens |
| `KeyValueSecrets` | the value of `password=`, `"secret": `, `x-api-key:`, `client_secret=`... |
| `Iban`, `Rib`, `FrenchNir`, `PaymentCards` | numbers of the right shape **and** a valid check digit (cards also start with 2 to 6) |
| `Emails` | addresses (skippable with `ShouldSkipEmail`) |

```csharp
var options = new RedactionOptions { Kinds = RedactionKinds.Emails | RedactionKinds.PaymentCards, RequireChecksum = false };
text.Redact(options);
```

- **Best effort, not a guarantee.** A secret in a shape it does not know (a random password in a sentence, a token without prefix) is not found, and a named value ends at the first space, quote, `&`, `;` or `,`.
  When you know where the secret is, use the typed obfuscators or `[SensitiveData]`; use the redactor as a safety net for text you do not control.
- **Precision or recall.** By default a number must pass its check digit, so timestamps, order numbers and dates are left alone. `RequireChecksum = false` masks anything shaped like a card, IBAN, RIB or NIR.
- **Safe on hostile input.** Every pattern is bounded; the cost grows linearly with the text (tested on 400 KB of worst-case shapes).
- Masking is idempotent: redacting an already redacted text changes nothing.

## Sensitive data in objects, headers and URLs

Mark properties once, log objects without thinking about it:

```csharp
public class Login
{
    public string UserName { get; set; } = "";
    [SensitiveData] public string? Password { get; set; }
    [SensitiveData(SensitiveDataKind.Email)] public string? Email { get; set; }
    [SensitiveData] public int Pin { get; set; }
}

logger.LogInformation("Login: {Login}", login.ToSafeJsonString());
// Login: {"UserName":"ada","Password":"hu**********r2","Email":"***@example.com","Pin":"***"}
```

A sensitive `string` is masked according to its `SensitiveDataKind` (secret, token, e-mail, URI, GUID, card, IBAN, RIB, NIR); any other type becomes `"***"`.
Normal serialization is not affected. On a positional record write `[property: SensitiveData]`: the attribute is deliberately refused on constructor parameters, where it would compile and silently
leave the value in clear. Build options once with `SensitiveDataJson.CreateOptions(template)` to combine with your own settings.

For data that has no class (headers, dictionaries, URLs, form bodies), `SensitiveKeys` decides by **name**:

```csharp
SensitiveKeys.IsSensitive("X-Api-Key");                                        // true
SensitiveKeys.MaskValue("Authorization", "Bearer abcdefghijklmnop");           // Bearer ab************op
"https://u:pw12345678@api.example.com/v1/items?page=2&access_token=abcdefghijklmnop#frag".ObfuscateUrl();
// https://u:pw******78@api.example.com/v1/items?page=2&access_token=ab************op#frag
"a=1&password=hunter2hunter2&sig=abcdefgh".ObfuscateQuery();                   // a=1&password=hu**********r2&sig=a******h
```

A name is sensitive when, ignoring case and `- _ .`, it contains `password`, `secret`, `token`, `apikey`, `authorization`, `cookie`, `credential`, `session`... (or is exactly `sig`).
Parameter names are compared after URL decoding. Matching is broad on purpose: masking too much is the safe direction.

## Constant-time comparison and Base64url

```csharp
if (!ConstantTime.AreEqual(request.Headers["X-Token"], expectedToken))   // null is never equal to anything, null included
    return Unauthorized();

new byte[] { 0xFB, 0xFF }.ToBase64UrlString();   // -_8   (URL-safe alphabet, no padding)
"-_8".FromBase64UrlBytes();                      // { 0xFB, 0xFF }
"+/8=".IsBase64UrlString();                      // false (standard alphabet)
```

`ConstantTime` protects the content, not the length (it returns early when lengths differ): use it for fixed-size secrets such as hashes and signatures.

## String conversions

Words are detected from spaces, separators, `camelCase` humps and acronyms, including accented letters:

```csharp
using SFerrand.Common.Extensions;

"XMLHttpRequest2".ToSnakeCase();   // xml_http_request2
"hello big world".ToPascalCase();  // HelloBigWorld
"café crème".ToKebabCase();        // café-crème
"Sébastien".RemoveDiacritics();    // Sebastien
"café".ToBase64String();           // Y2Fmw6k=   (UTF-8 by default, pass an Encoding to change it)
"AAAA".IsBase64String();           // true       (strict: padded, standard alphabet, no whitespace)
```

## Enums as strings with `[EnumMember]`

```csharp
[JsonConverter(typeof(EnumMemberJsonConverter<State>))]
public enum State
{
    Open,
    [EnumMember(Value = "in_progress")] InProgress,
}

JsonSerializer.Serialize(State.InProgress);          // "in_progress"
JsonSerializer.Deserialize<State>("\"in_progress\""); // State.InProgress
JsonSerializer.Deserialize<State>("\"bogus\"");       // throws JsonException (no silent default)
```

Reading accepts the member name or the `EnumMember` value (case-insensitive) and defined numeric values; keys of
dictionaries are supported. `[Flags]` combinations are not. On .NET 9+ the BCL offers `[JsonStringEnumMemberName]`;
this converter gives the same result on .NET 8 and with the `System.Runtime.Serialization` attribute.

## Collections

```csharp
using SFerrand.Common.Collections;

var uniqueByName = users.Distinct(new GenericComparer<User>(u => u.Name));
IList<int> list = ...;
list.RemoveAll(i => i % 2 == 0);   // single pass, returns the number of removed items
```

`FuncComparer<T>` takes an optional hash function; without one it stays correct in hash sets and LINQ set
operations but degrades to O(n) lookups.

## Configuration

```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .PrioritizeAppSettings()   // appsettings*.json now overrides the environment
    .Build();
```

## Documentation

- **API reference**: every public member is documented with XML comments (shipped in the package, so IntelliSense shows them).
- [Design notes](docs/DESIGN.md): why it works this way, what was rejected, known limits.
- [Test suite](tests/README.md): strategy, property tests, conventions.
- [Contributing](CONTRIBUTING.md), [Changelog](CHANGELOG.md) and [Releasing](RELEASING.md) (versioning and publication of the NuGet package).

## Build and test

```
dotnet test
```

The library and its tests target `net8.0` and `net10.0`. The tests run on both when both runtimes are installed (as in CI);
otherwise the `net8.0` run rolls forward to the newest runtime. Coverage and mutation-check procedures are in [CONTRIBUTING](CONTRIBUTING.md).

## License

[MIT](LICENSE)
