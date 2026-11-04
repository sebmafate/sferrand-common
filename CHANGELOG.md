# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - 2026-11-04

First version.

### Added

- `SecretObfuscator`: log-safe masking of secrets (`Obfuscate`), URIs with credentials (`ObfuscateUri`), connection strings
  (`ObfuscateConnectionString`), e-mail addresses (`ObfuscateEmail`, with a `shouldSkip` predicate), GUIDs (`ObfuscateGuid`)
  and API tokens with well-known prefixes (`ObfuscateToken`). Fail-safe: malformed input is masked, never returned in clear.
- `FinancialObfuscator`: masking of payment card numbers (PCI DSS display ceiling: first 6 and last 4), IBAN and French RIB,
  preserving the grouping separators, with a hard ceiling on what can be revealed.
- `SecretRedactor`: finds and masks credentials in URIs, `Bearer`/`Basic` credentials, JWTs, prefixed API tokens, `password=`-style pairs, IBAN, RIB, NIR, payment cards and
  e-mails inside free text; length-preserving, idempotent, with bounded patterns (no catastrophic backtracking) and check-digit validation to limit false positives.
- `[SensitiveData]` and `SensitiveDataJson`: log-safe JSON serialization of objects; `SensitiveKeys`: masking by header/field/query-parameter name, `ObfuscateUrl`, `ObfuscateQuery`.
- `FrenchIdentifierObfuscator`: NIR, SIREN, SIRET and French VAT number masking, with ceilings (the NIR can never reveal birth data).
- `Checksums`: Luhn, IBAN mod 97, RIB key, SIREN, SIRET (La Poste rule), NIR (Corsican departments) and French VAT number validation.
- `ConstantTime.AreEqual`: constant-time comparison of strings and bytes (a `null` never equals anything).
- `Base64UrlExtensions`: Base64url encoding, strict decoding and validation, identical on `net8.0` and `net10.0`.
- String helpers: PascalCase, camelCase, Title Case, snake_case, CONSTANT_CASE, kebab-case, COBOL-CASE, Train-Case and aLtErNaTiNg
  conversions; null/empty fallbacks; strict Base64 validation, encoding and decoding; diacritics removal.
- JSON shortcuts `ToJsonString` / `ToObject` and `EnumMemberJsonConverter<TEnum>` (string enums honouring `[EnumMember]`).
- `IList<T>` bulk operations, `GenericComparer<T>` and `FuncComparer<T>`.
- `[NotEmptyEnumerable]` validation attribute.
- `PrioritizeAppSettings()` configuration builder extension.
- Targets `net8.0` and `net10.0`; XML documentation shipped with the package.
