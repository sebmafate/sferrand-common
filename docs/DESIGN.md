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

## `FinancialObfuscator`

A separate class from `SecretObfuscator` because these identifiers have a **structure worth preserving**, and a different risk profile:
a card number or an IBAN is both a secret and personal data, and support teams do need to see "the card ending in 1111".

- **Layout is kept.** Spaces and hyphens are accepted anywhere, stay in place and do not consume the character budget, so
  `**** **** **** 1111` looks like the number it replaces. Other separators (dots, slashes) are not accepted: the value is then not
  recognized as a card and falls back to the generic masking, which is the safe direction.
- **Hard ceilings, not suggestions.** Cards follow the PCI DSS display rule (at most the first six and last four digits), IBAN shows at most
  four characters on each side, RIB at most the bank and branch codes plus four. Counts above the ceiling are clamped silently rather than
  rejected: a logging helper must not throw. Because even the shortest accepted value has more characters than the ceiling reveals
  (12 against 10, 15 against 8, 23 against 14), **something is always masked**, for every input and every argument.
- **Conservative defaults.** Card: last four only. IBAN: country code and last four (the check digits are computable from the rest, so
  they are masked). RIB: bank code only, since it identifies an institution rather than a person; the account number is fully masked,
  and the key, a checksum of the other characters, too. Trailing characters can be requested to tell two accounts apart.
- **Shape is validated, checksums are not.** The checks (digit count, ASCII characters, positions of letters) decide *which rules apply*;
  Luhn and mod 97 are deliberately not run, because a mistyped or truncated number is still sensitive and a masking function must not
  depend on validity. Character classes are ASCII on purpose: `char.IsDigit` also accepts digits of other scripts.
- **Fail-safe fallback.** A value of the wrong shape goes through `SecretObfuscator.Obfuscate`, so it is never returned in clear. A consequence
  worth knowing: a *malformed* long number can show up to four characters on each side, as any generic value does.
- **Out of scope.** Finding card numbers inside free text (which needs context and a Luhn check to limit false positives), BIC (public),
  card verification codes and PINs (they must not be logged at all).

## `Checksums`

Luhn, IBAN mod 97, RIB key, SIREN, SIRET, NIR and French VAT number. Three decisions:

- **They validate shape and check digit, never existence.** A random number passes Luhn one time in ten. The documentation says "well-formed", and the validators are used to reject
  typos and to cut false positives in `SecretRedactor`, not to authenticate anything.
- **Independent expected values.** Each algorithm is tested against numbers computed by a separate implementation, and the tables that are easy to get subtly wrong have rows built to fail if they are
  swapped: the RIB letter conversion (A-I, J-R, S-Z are three different ranges) and the La Poste exception (SIREN 356 000 000 is validated by "the digits add up to a multiple of 5" instead of Luhn;
  one test number only satisfies that rule, another only satisfies Luhn).
- **No country table for IBAN.** The generic range (15 to 34 characters) and mod 97 are checked, not the length each country requires; that table is long, changes over time, and
  its absence only makes the validator more permissive on a number that already passed a 1-in-97 check.

## `FrenchIdentifierObfuscator`

Same contract as `FinancialObfuscator` (layout kept, hard ceilings, shape checked but not the check digit, wrong shape masked as a whole). The ceilings follow what each identifier *encodes*:
the NIR carries sex, year and month of birth, department and commune, so at most its first character and its two key digits can be shown and nothing is shown by default; SIREN and SIRET
are public registry data (but identify a person when the company is a sole trader) and get a lighter default.

## `SecretRedactor`

The typed obfuscators need to know what a value is; real leaks are in free text (exception messages, bodies, concatenated log lines). The redactor recognizes data by shape and applies the
matching obfuscator, so the result keeps **the same length** and layout.

- **Best effort, stated as such.** It cannot find a secret in a shape it does not know. The documentation, the README and the type's remarks say so, and point to the typed APIs for when
  the location of the secret is known.
- **Precision by check digit.** A card, IBAN, RIB or NIR is masked only if its check digit holds (cards must also start with 2 to 6, the range of the payment networks, which excludes
  millisecond timestamps). Without it a log full of order numbers and epoch times would be unreadable. `RequireChecksum = false` trades that precision for recall.
- **Order matters, and is fixed**: URI credentials, authorization credentials, JWT, prefixed tokens, named secrets, then the numbers from most to least specific (an IBAN contains a
  RIB; a NIR could pass for a card), then e-mails.
- **Greedy matches are trimmed.** An IBAN followed by an upper-case word ("... 189 AND") is matched with the word; shorter candidates are tried until one is valid, and what follows is
  left untouched. A URI token ends at white space only, so several URIs glued by a comma form one token: it is split at every `scheme://` and each URI is masked on its own. (Masking
  only the first was a real leak, found by the random tests.)
- **Idempotent.** Redacting an already redacted text changes nothing. This needed one rule: an address glued to a mask character is the visible tail of an address that was already masked,
  and must not be masked again. (Also found by the random tests.)
- **Bounded patterns, standard engine.** Every pattern avoids an unbounded repetition in front of the part that anchors the match (a keyword, `@`, `://`), which is what makes a backtracking
  engine quadratic on hostile text. Two tests check it: the absolute time on 100 KB of worst-case shapes, and the scaling (4 times the text must cost about 4 times the time, not 16).
  Both were verified to fail when an unbounded repetition is put back.
  **`RegexOptions.NonBacktracking` was tried first and dropped.** It guarantees linear time, but a shared instance started returning *truncated replacements* after very large inputs processed
  concurrently (observed on the .NET 10 SDK used here; reproducible with the test suite, not with a stand-alone stress program, and a freshly built regex with the same pattern was correct).
  For a masking library silent wrong output is worse than a slow call, so it is not used; the bounded patterns give the same protection without the shared automaton. As a side effect the
  whole suite became five times faster.

## Sensitive data in objects and by name

- **`[SensitiveData]` is a declaration, read by the log-safe serializer.** It changes nothing for normal serialization. A sensitive `string` keeps its shape (masked by kind); any other type
  becomes the constant `"***"`, since there is no meaningful partial masking of a number or an array.
- **It is refused on constructor parameters, on purpose.** On a positional record it would compile, stay on the parameter, and leave the property serialized in clear while everyone believes
  it is masked. With the attribute limited to properties and fields the compiler stops the mistake and the fix is `[property: SensitiveData]`. A test pins the rule.
- **Reflection metadata only.** The modifier relies on the attributes of the reflection-based resolver; metadata built by hand has none and is left alone (tested).
- **By name, broad on purpose.** `SensitiveKeys` normalizes a name (case, `- _ .` removed) and looks for fragments (`password`, `token`, `authorization`...). It over-matches rather than under-matches
  (`tokenizer` is masked): hiding too much is the safe direction. `sig` is matched exactly, because as a fragment it would catch `design` and `signal`. Query parameter names are compared after URL decoding,
  since that is how the server reads them.
- **`ObfuscateUrl` is not `ObfuscateUri`.** `ObfuscateUri` masks a whole value that has no `scheme://` (fail-safe), which would destroy a relative URL; `ObfuscateUrl` only calls it when there is a scheme,
  and ignores a `?` that sits inside the fragment, which is never sent to a server and is not a query. (Both found by tests.)
- **Authorization keeps its scheme, other names do not.** `Bearer abcd...` keeps `Bearer` because it helps diagnosing and is not secret; a password containing a space must not leak its first word.

## `ConstantTime` and Base64url

`ConstantTime` is a null-safe layer over `CryptographicOperations.FixedTimeEquals`. It protects the content, not the length (it returns early when lengths differ), which is stated in the remarks.
Two `null` values are **not** equal: a missing secret must never authenticate a missing credential. Its timing property is not tested here (a timing assertion on a shared machine is flaky);
the tests check what it reports as equal.

Base64url is implemented on all targets because `System.Buffers.Text.Base64Url` only exists from .NET 9, and so behaves identically on `net8.0`. Encoding never pads; decoding accepts correct padding
and rejects whitespace, the standard alphabet and everything else, because lenient decoders are a classic source of validation bypasses. On .NET 9+ a test compares it with the framework.

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
