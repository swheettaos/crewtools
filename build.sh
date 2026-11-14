#!/bin/bash
# Build Crew Tools.
#   ./build.sh               build it (src/CrewTools/bin/Release/net6.0/CrewTools.dll)
#   ./build.sh --install     game closed: into BepInEx/plugins/CrewTools/, as players install it
#   ./build.sh --hot         game running, with a mod loader that reloads plugins from BepInEx/hot/: copy it there and
#                            ask the loader on 127.0.0.1:8765 to reload it (POST /hot/reload?name=CrewTools)
# MOON_GAME_DIR is the game's folder (default: Steam's on Linux); the build references the game's own DLLs there.
set -e
cd "$(dirname "$0")"
GAME="${MOON_GAME_DIR:-$HOME/.local/share/Steam/steamapps/common/Moon Sweepers}"
DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"; [ -x "$DOTNET" ] || DOTNET=dotnet
OUT=src/CrewTools/bin/Release/net6.0
VERSION=$(grep -o 'Version = "[0-9.]*"' src/CrewTools/Plugin.cs | grep -o '[0-9.]*')
out=$("$DOTNET" build src/CrewTools -c Release "-p:GameDir=$GAME" 2>&1) || true
if ! echo "$out" | grep -q "Build succeeded" || echo "$out" | grep -q " error "; then
    echo "$out" | grep -E " error " | sed 's/ \[.*//' | sort -u; echo "build FAILED"; exit 1
fi
echo "built Crew Tools $VERSION"
case "$1" in
    --install)
        D="$GAME/BepInEx/plugins/CrewTools"; mkdir -p "$D"
        cp "$OUT/CrewTools.dll" "$D/CrewTools.dll.new" && mv -f "$D/CrewTools.dll.new" "$D/CrewTools.dll"
        echo "installed into $D (loads at the next game start)" ;;
    --hot)
        H="$GAME/BepInEx/hot"; mkdir -p "$H"
        for f in dll pdb; do cp "$OUT/CrewTools.$f" "$H/CrewTools.$f.new" && mv -f "$H/CrewTools.$f.new" "$H/CrewTools.$f"; done
        if curl -s -m 2 127.0.0.1:8765/status >/dev/null 2>&1; then curl -s -m 30 -X POST "127.0.0.1:8765/hot/reload?name=CrewTools"; echo
        else echo "copied into $H (no loader answering: it loads at the next start)"; fi ;;
esac
