#!/bin/bash
# Refresh the private build references after a game or BepInEx update: copies every DLL src/CrewTools/CrewTools.csproj
# references from the game into a checkout of the references repo and pushes it. GitHub Actions builds against those
# (.github/workflows/build.yml); they come from the game, so that repo is private. It only adds and replaces files.
#   tools/refs.sh [checkout]      default ../moonsweepers-refs
# A fork that wants its own CI makes its own private repo the same way and sets REFS_REPO and REFS_DEPLOY_KEY.
set -e
cd "$(dirname "$0")/.."
GAME="${MOON_GAME_DIR:-$HOME/.local/share/Steam/steamapps/common/Moon Sweepers}"
R="${1:-../moonsweepers-refs}"
[ -d "$R/.git" ] || { echo "no checkout at $R (clone the references repo there first)"; exit 1; }
grep -o 'GameDir)/[^"]*' src/CrewTools/CrewTools.csproj | sed 's/GameDir)\///' | while read -r f; do
    mkdir -p "$R/$(dirname "$f")"; cp "$GAME/$f" "$R/$f"
done
cd "$R"; git add -A
if git diff --cached --quiet; then echo "the references are already up to date"; exit 0; fi
git commit -q -m "Crew Tools' references refreshed from the game ($(date +%F))"
git push -q
echo "pushed: $(git log --oneline -1)"
