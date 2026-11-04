# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

## [1.0.0] - not published yet

First version.

### Added

- `SecretObfuscator`: log-safe masking of secrets (`Obfuscate`), URIs with credentials (`ObfuscateUri`), connection strings
  (`ObfuscateConnectionString`), e-mail addresses (`ObfuscateEmail`, with a `shouldSkip` predicate), GUIDs (`ObfuscateGuid`)
  and API tokens with well-known prefixes (`ObfuscateToken`). Fail-safe: malformed input is masked, never returned in clear.
- String helpers: PascalCase, camelCase, Title Case, snake_case, CONSTANT_CASE, kebab-case, COBOL-CASE, Train-Case and aLtErNaTiNg
  conversions; null/empty fallbacks; strict Base64 validation, encoding and decoding; diacritics removal.
- JSON shortcuts `ToJsonString` / `ToObject` and `EnumMemberJsonConverter<TEnum>` (string enums honouring `[EnumMember]`).
- `IList<T>` bulk operations, `GenericComparer<T>` and `FuncComparer<T>`.
- `[NotEmptyEnumerable]` validation attribute.
- `PrioritizeAppSettings()` configuration builder extension.
- Targets `net8.0` and `net10.0`; XML documentation shipped with the package.
