#!/usr/bin/env bash
# Prepares a release: checks everything that can be checked, runs the tests, creates the version tag.
# It does NOT push: pushing the tag is what triggers publication, so that step stays yours.
#
# Usage: scripts/release.sh 1.2.3        (or 1.2.3-rc.1)
set -euo pipefail

fail() { echo "error: $*" >&2; exit 1; }

version="${1:-}"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$ ]] || fail "usage: $0 MAJOR.MINOR.PATCH[-prerelease]"
tag="v$version"

cd "$(git rev-parse --show-toplevel)"

[[ "$(git rev-parse --abbrev-ref HEAD)" == "main" ]] || fail "releases are made from the main branch"
[[ -z "$(git status --porcelain)" ]] || fail "the working tree is not clean; commit or stash your changes"

git fetch --quiet origin main --tags
[[ "$(git rev-parse HEAD)" == "$(git rev-parse origin/main)" ]] || fail "main is not in sync with origin/main (push or pull first)"
git rev-parse -q --verify "refs/tags/$tag" >/dev/null && fail "tag $tag already exists"

escaped="${version//./\\.}"
grep -Eq "^## \[$escaped\] - [0-9]{4}-[0-9]{2}-[0-9]{2}" CHANGELOG.md \
  || fail "CHANGELOG.md has no '## [$version] - YYYY-MM-DD' section (rename 'Unreleased' and date it)"

echo "Running the tests on all target frameworks..."
dotnet test --configuration Release

git tag -a "$tag" -m "Release $tag"
echo
echo "Tag $tag created on $(git rev-parse --short HEAD). To publish:"
echo "  git push origin $tag"
echo "To undo before pushing:"
echo "  git tag -d $tag"
