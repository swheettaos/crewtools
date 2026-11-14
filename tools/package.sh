#!/bin/bash
# Zip a built Crew Tools in the game's folder layout:  tools/package.sh <x.y.z> [--publish]
# The zip holds BepInEx/plugins/CrewTools/ (CrewTools.dll, README.md, CHANGELOG.md, LICENSE.txt) and lands in dist/.
# --publish (CI, on a version tag): publish the GitHub release v<x.y.z> with the zip and that version's CHANGELOG.md
# section as its notes.
set -e
cd "$(dirname "$0")/.."
VER="$1"
[[ "$VER" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "usage: tools/package.sh <x.y.z> [--publish]"; exit 1; }
grep -q "Version = \"$VER\"" src/CrewTools/Plugin.cs || { echo "src/CrewTools/Plugin.cs isn't version $VER"; exit 1; }
NOTES=$(awk -v v="## $VER" '$0 == v { on = 1; next } /^## / { if (on) exit } on' CHANGELOG.md)
[ -n "$(echo "$NOTES" | tr -d '[:space:]')" ] || { echo "CHANGELOG.md has no \"## $VER\" section"; exit 1; }
OUT=$(mktemp -d); trap 'rm -rf "$OUT"' EXIT
P="$OUT/BepInEx/plugins/CrewTools"; mkdir -p "$P"
cp src/CrewTools/bin/Release/net6.0/CrewTools.dll README.md CHANGELOG.md "$P/"; cp LICENSE "$P/LICENSE.txt"
mkdir -p dist; ZIP="$PWD/dist/CrewTools-$VER.zip"; rm -f "$ZIP"
(cd "$OUT" && python3 -m zipfile -c "$ZIP" BepInEx)
echo "packaged dist/CrewTools-$VER.zip"
[ "$2" = "--publish" ] || exit 0
gh release create "v$VER" "$ZIP" --title "Crew Tools $VER" --verify-tag --notes "$NOTES

### Install

Close the game and unzip into the game's folder (the one that holds \`BepInEx/\`), so you get
\`BepInEx/plugins/CrewTools/CrewTools.dll\`. Remove a separate \`CrewPermissions.dll\` if you have one: Crew Tools
includes it. Needs BepInEx 6 (IL2CPP). Updating: the same, over the old files; your players and settings are kept."
