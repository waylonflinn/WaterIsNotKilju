#!/usr/bin/env bash
# Build and install WaterIsNotKilju mod
# Usage: ./install.sh [game-dir]
#
# If game-dir is not provided, tries common Steam/Proton paths.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
BUILD_OUT="$SCRIPT_DIR/bin/Debug/net46/WaterIsNotKilju.dll"

# Find game directory
if [ -n "${1:-}" ]; then
    GAME_DIR="$1"
else
    # Try common paths
    CANDIDATES=(
        "$HOME/.steam/steam/steamapps/common/My Summer Car"
        "$HOME/.local/share/Steam/steamapps/common/My Summer Car"
    )
    GAME_DIR=""
    for c in "${CANDIDATES[@]}"; do
        if [ -d "$c" ]; then
            GAME_DIR="$c"
            break
        fi
    done
    if [ -z "$GAME_DIR" ]; then
        echo "ERROR: Could not auto-detect game directory."
        echo "Pass it as an argument: $0 /path/to/My Summer Car"
        exit 1
    fi
fi

MOD_DIR="$GAME_DIR/Mods/WaterIsNotKilju"

echo "Building..."
dotnet build "$SCRIPT_DIR/WaterIsNotKilju.csproj" -c Debug

echo "Installing to: $MOD_DIR"
mkdir -p "$MOD_DIR"
cp "$BUILD_OUT" "$MOD_DIR/WaterIsNotKilju.dll"

echo "Done! Launch MSC and check the MSCLoader console for [WaterIsNotKilju] messages."