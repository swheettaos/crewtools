#!/bin/bash
# Release Crew Tools:  tools/release.sh <x.y.z>
# Checks that main is clean and matches GitHub, that src/CrewTools/Plugin.cs is that version and CHANGELOG.md has its
# section, then tags v<x.y.z> and pushes the tag. GitHub builds the zip and publishes the release (.github/workflows).
#   tools/release.sh <x.y.z> --dry-run   build and zip into dist/ here instead: no tag, nothing published
set -e
cd "$(dirname "$0")/.."
VER="$1"; TAG="v$VER"
[[ "$VER" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: tools/release.sh <x.y.z> [--dry-run]"; exit 1; }
if [ "$2" = "--dry-run" ]; then ./build.sh && tools/package.sh "$VER"; exit 0; fi
[ -z "$(git status --porcelain)" ] || { echo "commit your changes first"; exit 1; }
[ "$(git rev-parse --abbrev-ref HEAD)" = main ] || { echo "release from main"; exit 1; }
git fetch -q origin main
[ "$(git rev-parse HEAD)" = "$(git rev-parse origin/main)" ] || { echo "main differs from GitHub's: push or pull first"; exit 1; }
! git rev-parse -q --verify "refs/tags/$TAG" >/dev/null || { echo "$TAG already exists"; exit 1; }
grep -q "Version = \"$VER\"" src/CrewTools/Plugin.cs || { echo "set Version = \"$VER\" in src/CrewTools/Plugin.cs first"; exit 1; }
awk -v v="## $VER" '$0 == v { f = 1 } END { exit !f }' CHANGELOG.md || { echo "CHANGELOG.md needs a \"## $VER\" section"; exit 1; }
git tag "$TAG" && git push -q origin "$TAG"
echo "pushed $TAG: GitHub builds and publishes the release (gh run watch, then gh release view $TAG)"
