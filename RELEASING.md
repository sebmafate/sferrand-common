# Releasing

How a version of `SFerrand.Common` is numbered, built and published. Everything is driven by **git tags**: there is no version number
to edit in a project file.

## How versions work

- The version comes from the latest tag `vMAJOR.MINOR.PATCH` reachable from the commit being built, computed by
  [MinVer](https://github.com/adamralph/minver). `v1.2.0` produces package `SFerrand.Common` **1.2.0**.
- Any commit after a tag builds as a **pre-release** of the next patch version: five commits after `v1.2.0` give
  `1.2.1-preview.0.5`. Pre-releases are therefore never confused with, nor sorted after, the real release.
- Before the first tag the version is `0.0.0-preview.0.N`.
- A pre-release you want to publish deliberately is tagged explicitly: `v1.3.0-rc.1`.
- Inside the DLL the *file* version is `MAJOR.MINOR.PATCH.0`, the *informational* version is the full number, and the *assembly*
  version is `MAJOR.0.0.0` (so a minor or patch update never forces a binding redirect). The package also embeds the commit hash
  (`<repository commit="...">`) and SourceLink data pointing at the source on the Forgejo server, so a debugger can fetch
  the exact code of the version in use.

### Choosing the number ([Semantic Versioning](https://semver.org/))

- **Major**: removing or changing a public member.
- **Minor**: new members; **also a change to what a masking helper outputs** (callers may compare or index masked values
  in logs), which must be called out in the changelog.
- **Patch**: bug fixes that make a method match its documentation, and documentation.

### Source links

The symbols carry links to the source of the exact commit, for both hosts: `Microsoft.SourceLink.GitHub` for github.com, and `Microsoft.SourceLink.Gitea` for the Forgejo server,
whose host is declared in the project (`SourceLinkGiteaHost`). The Gitea provider is generic: while it is the only provider it accepts any host, but with two providers it must be
told which host is its own. If you move the Forgejo server, change that line. The links are built from the `origin` remote of the checkout, which is why the Forgejo release workflow
resets it to the public URL first.

## One-time setup

1. **A runner.** Workflows need a registered `forgejo-runner`. Set `runs-on` in `.forgejo/workflows/*.yml` to one of its labels
   (`docker` is a placeholder).
2. **Forgejo secrets** (repository settings, Actions, Secrets):

   | Secret | Value |
   |---|---|
   | `PACKAGES_USER` | your Forgejo user name (`sebastien`) |
   | `PACKAGES_TOKEN` | an access token with the **write:package** scope (Settings, Applications) |
   | `NUGET_API_KEY` | *optional*: a nuget.org API key; when present, the release is also pushed to nuget.org |

3. **nuget.org (optional).** From Forgejo, only with an API key (`NUGET_API_KEY` above), which is a long-lived secret. The GitHub workflow publishes with
   Trusted Publishing and needs no key: see "Setting up nuget.org" below.

## Releasing a version

1. Move the entries of `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) under a new heading **with the date**:
   `## [1.2.0] - 2026-12-01`.
2. Commit and push to `main`; wait for CI to be green.
3. Create the tag with the helper, which checks everything first (on `main`, clean tree, in sync with `origin`,
   tag unused, changelog section present and dated) and runs the tests on both target frameworks:

   ```bash
   scripts/release.sh 1.2.0
   ```

4. Push the tag. This is the step that publishes:

   ```bash
   git push origin v1.2.0
   ```

5. The **Release** workflow then: checks the tag format, builds, runs the tests, packs, verifies that the package version
   equals the tag, uploads the `.nupkg` as a workflow artifact, and pushes to the Forgejo package registry
   (and to nuget.org if `NUGET_API_KEY` is set). `--skip-duplicate` makes a re-run harmless.
6. Check the package appears under the user's *Packages* page.

On GitHub, the same tag runs `.github/workflows/release.yml`. The **build** job builds, tests and packs, then creates a **GitHub release** named after the tag, whose notes are the
matching section of the changelog and which carries the `.nupkg` and `.snupkg` files. The **nuget** job then pushes to nuget.org with
[Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing): no API key is stored, GitHub proves the identity of the workflow and nuget.org
answers with a key valid for one hour.

### Setting up nuget.org (once per repository)

1. Reserve the `SFerrand.*` prefix on nuget.org.
2. nuget.org, your user name, **Trusted Publishing**, add a policy: repository owner `sebmafate`, this repository, workflow file `release.yml` (the name only),
   environment `nuget`, and a scope that allows **new packages** (pattern `SFerrand.*`). A policy on a private repository is only *temporarily* active for 7 days, until the first
   successful publication fixes the repository identity; it can be restarted.
3. GitHub, Settings, Environments: create the environment **`nuget`** and add yourself as a *required reviewer*, so that every publication waits for your approval
   (a package version on nuget.org can be hidden but never deleted).
4. GitHub, Settings, Secrets and variables, Actions, **Variables**: define `NUGET_USER` with your nuget.org *profile name* (not your e-mail address).
   Without this variable the `nuget` job is skipped, so a tag never fails because nuget.org is not set up yet.

### Publishing a tag that already has a GitHub release

Actions, **Release**, *Run workflow*, and give the tag (`v1.0.0`). The build runs again from that tag, the existing GitHub release is left alone, and the `nuget` job
asks for your approval before pushing. `--skip-duplicate` makes a second run harmless.

### The Forgejo registry

Forgejo Actions has no equivalent of Trusted Publishing, and without a runner `.forgejo/workflows/release.yml` does not run. The `.nupkg` files of a GitHub release can be pushed to the
Forgejo registry by hand:
`dotnet nuget push <file> --source https://git.vbmaf.net/api/packages/sebastien/nuget/index.json --api-key <token with write:package>`.

## Using the packages

From the Forgejo registry (private: it needs credentials):

```bash
dotnet nuget add source https://git.vbmaf.net/api/packages/sebastien/nuget/index.json \
  --name forgejo --username sebastien --password <token with read:package>
dotnet add package SFerrand.Common
```

## Trying a package locally before releasing

```bash
dotnet pack src/SFerrand.Common -c Release -o artifacts          # artifacts/SFerrand.Common.<version>.nupkg
dotnet nuget add source "$PWD/artifacts" --name local-sferrand
# in a test project:  dotnet add package SFerrand.Common --version <version> --source local-sferrand
dotnet nuget remove source local-sferrand              # when done
```

The version of a local pack is the pre-release one (see above). Remove the package from `~/.nuget/packages/sferrand.common`
afterwards, otherwise a later restore of the *same version number* silently reuses your local build.

## After the first release

Set the published version as the API-compatibility baseline in `src/SFerrand.Common/SFerrand.Common.csproj`:

```xml
<PackageValidationBaselineVersion>1.0.0</PackageValidationBaselineVersion>
```

From then on `dotnet pack` fails if a public API change breaks compatibility with that version, so a breaking change cannot ship
under a minor or patch number by accident. Bump the baseline at each major release.
(`EnablePackageValidation` is already on: it checks that the package is coherent across its target frameworks.)

## When something goes wrong

| Situation | What to do |
|---|---|
| Tag created locally by mistake | `git tag -d v1.2.0` (nothing is published until the tag is pushed). |
| Wrong tag pushed, workflow failed before publishing | Delete the remote tag (`git push origin :refs/tags/v1.2.0`) and the local one, fix, tag again. |
| Published a broken version | **A version number can never be reused.** Delete that version from the Forgejo registry (package settings; on nuget.org you can only *unlist* it), fix, and release the next patch. |
| Workflow fails on "package version matches the tag" | The checkout lacks the tags (`fetch-depth: 0` is required) or the tag is not on the built commit. |
| Package restores as an old build | A stale copy in `~/.nuget/packages` (see "Trying a package locally"). |

## Status of this automation

Verified locally: version computation (no tag, tag, commits after a tag), the release script guards and nominal path, the
tag-format and version checks, SourceLink data in the symbols, and a consumer project restoring and using the package
from a feed. **Not yet run on a real runner**: the two workflows, the Forgejo registry push (the `nuget add source` and `push`
commands follow the Forgejo documentation) and the optional nuget.org push. Expect to adjust `runs-on` and secrets on the first release.
