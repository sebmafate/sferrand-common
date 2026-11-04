# Test suite

622 tests, MSTest 4, run on `net8.0` and `net10.0`. No test needs the Internet, a network or a fixed path. This page explains
how the suite is organized and how to add to it; how to run it is in [CONTRIBUTING.md](../CONTRIBUTING.md).

## Strategy

| Class | Kind | What it protects |
|---|---|---|
| `SecretObfuscatorTests` | Examples | Exact expected output for every masking method, the adaptive thresholds, and each fail-safe path (null, empty, malformed). The examples double as living documentation of the exact output formats. |
| `ObfuscatorInvariantTests` | Properties | Guarantees that must hold for *any* input, checked on thousands of random inputs per property: length preserved, never a whole value revealed, a password/secret never leaks through a URI, connection string, e-mail, GUID or token. |
| `FinancialObfuscatorTests` | Examples | Cards, IBAN and RIB in their usual layouts (spaces, hyphens, none, lower case), the requested counts, the ceilings, the wrong-shape fallback. Numbers are public test or registry examples. |
| `FinancialObfuscatorInvariantTests` | Properties | For thousands of random numbers, layouts and counts: layout preserved, only a prefix and a suffix shown, ceilings respected, a minimum number of characters always masked; fail-safe on arbitrary text. |
| `ChecksumsTests` | Examples | Valid and invalid numbers for each algorithm. Every expected result was computed by an **independent implementation** (not the code under test); the RIB letter table and the La Poste rule have rows that fail if swapped. |
| `FrenchIdentifierObfuscatorTests` | Examples + properties | NIR, SIREN, SIRET and VAT masking: exact outputs, ceilings (the NIR can never show birth data), wrong-shape fallback, random layouts. |
| `SecretRedactorTests` | Examples | One group per category, the false positives it must **not** produce (timestamps, dates, order numbers), the options, glued URIs, and hostile input (shape and scaling). |
| `SecretRedactorInvariantTests` | Properties | On random text: never throws, keeps the length, idempotent, secret-free text returned as the same instance; secrets **planted in noise** are masked. The valid cards and IBANs come from generators written in the test file. |
| `SensitiveDataTests` | Examples | One case per masking kind, non-string properties, inheritance, nesting, records, fields, templates, and the guarantee that normal serialization is untouched. |
| `SensitiveKeysTests` | Examples | Which names are sensitive, header and dictionary values, URLs (credentials, fragment, relative) and query strings (URL-decoded names). |
| `ConstantTimeTests`, `Base64UrlTests` | Examples | Functional behaviour (the constant-time *property* itself is delegated to the framework primitive; a timing test would be flaky). Base64url uses the RFC 4648 vectors and, on .NET 9+, a comparison with the framework. |
| `StringExtensionsTests` | Examples + tables | The case conventions on identifiers of every shape (acronyms, digits, separators, accents), fallbacks, strict Base64, diacritics. |
| `JsonTests` | Examples | The JSON shortcuts and `EnumMemberJsonConverter<T>` (names, `EnumMember` values, numbers, dictionary keys, nullable/nested use). |
| `CollectionsTests` | Examples | `IList<T>` operations and the key-based comparers (hash/equality consistency, nulls, behaviour in hash sets and LINQ). |
| `ValidationAndConfigurationTests` | Examples | `[NotEmptyEnumerable]` through the real `Validator`; `PrioritizeAppSettings` with real temporary JSON files. |

## Property tests

`ObfuscatorInvariantTests`, `FinancialObfuscatorInvariantTests` and `SecretRedactorInvariantTests` generate inputs with `System.Random` and a **fixed seed**, so every run is identical and a failure
is reproducible. The assertion messages include the offending input and the leading/trailing counts used. To explore more
widely, raise `Iterations` or change a seed locally; if that finds a bug, keep the failing input as a regular example in
`SecretObfuscatorTests`.

Why properties for this library: a masking helper that works on the examples a developer thought of but leaks on an
unusual length is worse than none. The properties state the contract ("never reveals a whole value") rather than outputs.

## Timing assertions

A test that compares durations is flaky by nature unless it is protected from the other tests. Both hostile-input tests below are marked `[DoNotParallelize]` (MSTest runs them alone, after the parallel
ones), compare the **best** of several measurements (a pause can only make a run slower), and ignore differences below a noise floor. The first version ran in parallel with the rest of the suite and failed about
one run in ten; after the change, 40 consecutive full runs on both target frameworks passed.

## Hostile-input tests

`SecretRedactorTests` includes two tests on text built to make a regular expression struggle (long runs that almost match: key characters with no keyword,
addresses without a domain, a keyword without a value...). One bounds the absolute time on 100 KB, the other checks that a text **4 times larger costs about 4 times more,
not 16** (the signature of a quadratic pattern). Both were verified to fail when an unbounded repetition is put back in front of an anchor. They are the reason the patterns
are bounded; keep them bounded.

## Conventions

- **Name = behaviour**: `Subject_Condition_ExpectedOutcome` (`ObfuscateToken_PrefixOnly_IsFullyMasked`). A failing test name should tell
  you what broke without opening the file.
- **One reason to fail** per test. `[DataRow]` tables are used when only the data varies.
- **Document the why**: every class has a summary, and tests whose purpose is not obvious from the name carry an XML summary
  explaining which trap they guard against. If you add a test for a subtle case, do the same.
- Tests are independent and run **in parallel** at method level (`MSTestSettings.cs`).
- A **30 s timeout** per test (`test.runsettings`) turns a hang into a failure instead of a blocked pipeline.
- Banking, social-security and company numbers in tests are public examples or arithmetic constructions, never real people, accounts or companies.
- Banking test data is public: payment-scheme test cards and IBAN-registry examples, never real accounts.
- Test secrets are obviously fake. Keep them shorter than real token formats so secret scanners do not flag the repository.
- The Azure-storage-like key used in one test is the publicly documented development key of the Azurite emulator, not a credential.

## Coverage

100 % of lines and about 98 % of branches at 1.0.0 (command in CONTRIBUTING). Coverage tells you code ran, not that it is
checked; the property tests and the manual mutation procedure in CONTRIBUTING cover that gap.

## Known limits

- The redactor tests assert the documented behaviour on the shapes they contain; they cannot show that *no* secret escapes (it is a best-effort tool, and says so).

- The `net8.0` run uses the .NET 8 runtime only when it is installed (CI does). Locally it may roll forward.
- The properties are statistical: they sample the input space, they do not prove the contract.
- Behaviour with culture-specific text (for example Turkish dotted/dotless `i`) is only covered through the invariant-culture code paths the library uses.
