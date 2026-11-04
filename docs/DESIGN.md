# Design notes

Why `SFerrand.Common` is shaped the way it is. Each section states the decision, the reason, and what was rejected.

## Goals and non-goals

**Goals**: a handful of helpers that are safe by default, null-aware, documented and fully tested; no dependency beyond
`Microsoft.Extensions.Configuration`.

**Non-goals**: a general-purpose utility belt. Anything the BCL already does well is not wrapped; a helper is here because it
removes a recurring trap (see below).

## `SecretObfuscator`

### Fail-safe: a bad value is masked, never returned in clear

Logging code runs on the error path; a masking helper that throws, or silently returns the input when it does not understand
it, defeats its purpose. Every method therefore treats an unexpected shape as "hide everything" (the value is masked as a
whole) and none throws on malformed input. Examples: a URI without `://`, a connection string without the requested key, an
e-mail without exactly one `@`, a GUID that is not in canonical form.

### Never reveal a whole value

`Obfuscate(leading, trailing)` keeps the first and last characters visible so a value stays recognizable in logs. If the requested
counts would cover the whole value, the adaptive default applies instead. The visible portion also adapts to the length so
short secrets are not exposed by a fixed count:

| Length | Characters kept on each side |
|---|---|
| up to 4 | 0 |
| 5 to 8 | 1 |
| 9 to 16 | 2 |
| more than 16 | 4 |

The rule is stated by `GetNumberOfPreservedChars`; with the defaults at most a quarter of a value is visible on each side, and a value is
never fully visible (explicit counts can show more, but never everything).

### Length-preserving and deterministic

The mask has the same length as the original and the same input always gives the same output. This keeps log lines aligned and
lets two masked occurrences of one secret be recognized as equal. The trade-off is that length leaks and short secrets can be
brute-forced from what remains: this is a **logging aid, not a security boundary**.

### Structure-aware helpers

- **URI**: only the password is masked, the user name stays (useful for diagnostics). The *last* `@` of the authority splits user
  info from host, because a password may itself contain `@`. User info without `:` (a token used as user name) is masked entirely.
- **Connection string**: only the value of the requested key (case-insensitive, every occurrence) is masked; everything else is
  preserved verbatim. Only the first `=` of a segment separates key and value, since Base64 keys end with `=`.
  Quoted values containing `;` are not supported (documented).
- **E-mail**: each alphanumeric run of the local part is masked separately, so the name structure and the domain remain readable
  for support. A `shouldSkip` predicate lets the caller keep non-personal addresses (shared mailboxes) readable; the library has
  no built-in notion of which those are, because that is organization-specific.
- **GUID**: the third group (which holds the version) is kept so the kind of identifier stays visible.
- **Token**: a known prefix such as `ghp_` stays visible (it tells which kind of credential leaked) and only the body is masked;
  prefixes are configurable.

## `EnumMemberJsonConverter<TEnum>`

`System.Text.Json` writes enums as numbers by default, and does not honour `[EnumMember]` on .NET 8. The converter reads the member
name or the `EnumMember` value (case-insensitive) and defined numbers, and writes the `EnumMember` value or the name.
Unknown strings and undefined numbers on read throw `JsonException` rather than silently producing the default value, which would hide
corrupted data. Writing an undefined value emits its number rather than failing, so that logging or responding never breaks on
an unexpected enum value. The type is named `EnumMemberJsonConverter` to avoid colliding with the BCL's
`JsonStringEnumConverter<TEnum>` on .NET 8+. `[Flags]` combinations are out of scope.

## String conversions

Case conversions share one word-splitting regex (separators, lower-to-upper humps, acronym boundaries, digits, Unicode letters), so the
conventions are consistent with each other: any conversion can be derived by joining the same words differently. Acronyms are
normalized (`XMLHttp` becomes `XmlHttp` in PascalCase). `IsBase64String` is deliberately strict (padded, standard alphabet, no whitespace)
so that "is this Base64?" has one unambiguous answer. `RemoveDiacritics` relies on Unicode decomposition, so letters that are not "letter + accent"
(`ø`, `ł`) are unchanged.

## Collections

`GenericComparer<T>` compares by a key and derives a hash code from it, so it is correct in `HashSet`, `Dictionary` and LINQ set operations.
`FuncComparer<T>` takes any predicate; without a hash function it stays correct but degrades to O(n) lookups, which is documented rather
than forbidden. `AddRange` materializes its source first so `list.AddRange(list)` works.

## Configuration

`PrioritizeAppSettings` moves JSON file sources after the others so `appsettings*.json` wins over environment variables or other
sources registered earlier. Sources are matched by exact type to avoid moving subclasses with their own semantics.
