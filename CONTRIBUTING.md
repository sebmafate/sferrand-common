# Contributing

Thanks for your interest. This document explains how to build, test and document a change so that it is ready to merge.

## Prerequisites

- The **.NET 10 SDK** (it builds both target frameworks, `net8.0` and `net10.0`).
- The **.NET 8 runtime** as well if you want the `net8.0` test run to execute on .NET 8. Without it the test host rolls
  forward to the newest installed runtime (`RollForward=Major` in the test project): the `net8.0` build is then compiled
  against the .NET 8 API surface but executed on a newer runtime. CI installs both, so it is the reference.

## Everyday commands

```bash
dotnet build -c Release            # warnings are errors
dotnet test                        # both target frameworks
dotnet test -f net10.0             # one target framework
dotnet pack src/SFerrand.Common -c Release -o artifacts
```

### Coverage

```bash
dotnet test -f net10.0 --collect:"XPlat Code Coverage" --results-directory /tmp/cov
grep -o '<coverage line-rate="[^"]*" branch-rate="[^"]*"' /tmp/cov/*/coverage.cobertura.xml
```

Reference values at 1.0.0: **100 % of lines, about 98 % of branches**. The uncovered branches are defensive checks that the
public API cannot reach (a `null` string coming out of a JSON string token, an empty word coming out of the word regex). A change
should not lower either number without a reason stated in the pull request.

## What a change must contain

1. **Tests.** A bug fix starts with a test that fails for the right reason. A new behaviour is tested at its edges
   (null, empty, very short and very long values, malformed input), not only on the happy path.
   Read [tests/README.md](tests/README.md) first. Anything added to `SecretObfuscator` also joins the property tests in
   `ObfuscatorInvariantTests`.
2. **XML documentation** on every public member, including `<param>`, `<returns>`, `<exception>` and, for non-trivial
   behaviour, an `<example>` whose output you have actually run. The build enforces the minimum (`CS1591` is an error),
   reviewers enforce the quality: say *what* and *when* (defaults, what happens on malformed input), not just repeat the name.
3. **README / docs updates** when behaviour visible to users changes. Every code sample in the README must produce the
   output it claims; re-run it when you touch it.
4. **CHANGELOG entry** under `Unreleased`.
5. **A design note** in [docs/DESIGN.md](docs/DESIGN.md) if you change a decision recorded there, or make a new non-obvious one.

## Conventions

- C# 12, nullable reference types on, warnings as errors, 4-space indentation (see `.editorconfig`).
- Public methods validate their arguments (`ArgumentNullException.ThrowIfNull`), **except** the masking helpers, which must
  never throw on a bad value: a logging helper that fails turns a diagnostic into an incident.
- Keep the dependency list short (two `Microsoft.Extensions.Configuration` packages today); a new package needs a justification.
- No culture-sensitive operations: use the invariant or ordinal variants.
- Regular expressions in `SecretRedactor` must keep every repetition **bounded** before the part that anchors the match. Do not switch them to `RegexOptions.NonBacktracking`
  (see docs/DESIGN.md); the hostile-input tests will tell you if a pattern becomes quadratic.

## Mutation check (manual)

Line and branch coverage only prove that code *ran*. Before a release, or after touching the masking logic, break the code on
purpose in a scratch copy and make sure a test goes red:

- change a threshold of `GetNumberOfPreservedChars` by one;
- let `ObfuscateUri` keep the password, or `Obfuscate` keep one character too many;
- invert `leading + trailing >= input.Length`, the guard that prevents revealing a whole value;
- drop the prefix handling of `ObfuscateToken`;
- raise a ceiling of `FinancialObfuscator` (6 + 4 for cards) or accept a character class it should reject;
- change a weight or constant of a check digit (`Checksums`), the order or a guard of `SecretRedactor`, or an entry of `SensitiveKeys`;
- **classify by the exit code of `dotnet test`, never by parsing its (localized) output, and validate the harness with both kinds of control**: a mutation that does nothing
  (it must *survive*) and an obviously wrong one (it must be *killed*). A harness that parsed the French console text reported every mutant as killed, including the one that
  did nothing, and hid thirteen real gaps; and read each survivor: a mutant that cannot change behaviour (an unbounded repetition that is still linear)
  is equivalent, not a missing test.

A mutation that survives means a missing or weak test: add one. The seeded property tests in `ObfuscatorInvariantTests`
exist precisely because hand-picked examples are easy to satisfy by accident.

## Releases

Versions come from git tags (MinVer); there is nothing to bump in the project files. The procedure, the secrets and the
checks are in [RELEASING.md](RELEASING.md).

## Pull requests

Keep them focused, describe the *why*, and mention anything you could not verify (for instance behaviour that depends on a
specific runtime or platform).
