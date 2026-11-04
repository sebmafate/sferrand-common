# SFerrand.Common

Small, dependency-light helpers for .NET 8+, extracted from production services and rewritten to be
generic, null-safe and fully tested.

```
dotnet add package SFerrand.Common
```

| Area | What you get |
|---|---|
| **Log-safe masking** (`SFerrand.Common.Security`) | `SecretObfuscator`: mask secrets, URIs with credentials, connection strings, e-mails, GUIDs and API tokens while keeping them recognizable. |
| **Strings** (`SFerrand.Common.Extensions`) | PascalCase / camelCase / snake_case / CONSTANT_CASE / kebab-case / COBOL-CASE / Train-Case, Base64 helpers, diacritics removal, null/empty fallbacks. |
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
