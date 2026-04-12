#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_NAME="AutoPath"

# Auto-detect STS2 mods path per platform
case "$(uname -s)" in
    Linux*)  STS2_MODS="$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/mods" ;;
    Darwin*) STS2_MODS="$HOME/Library/Application Support/Steam/steamapps/common/Slay the Spire 2/mods" ;;
    MINGW*|MSYS*|CYGWIN*) STS2_MODS="C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2/mods" ;;
    *) echo "Unknown OS: $(uname -s). Set STS2_MODS manually." && exit 1 ;;
esac

# Allow override: STS2_MODS=/custom/path ./build-and-deploy.sh
DEPLOY_DIR="${STS2_MODS}/$MOD_NAME"

echo "=== Building $MOD_NAME ==="
cd "$SCRIPT_DIR"
dotnet build -c Release --nologo -v quiet

echo "=== Deploying to $DEPLOY_DIR ==="
mkdir -p "$DEPLOY_DIR"
cp "bin/Release/$MOD_NAME.dll" "$DEPLOY_DIR/"
cp "$MOD_NAME.json" "$DEPLOY_DIR/"

echo "=== Done ==="
echo "  $DEPLOY_DIR/$MOD_NAME.dll"
echo "  $DEPLOY_DIR/$MOD_NAME.json"
echo ""
echo "Launch STS2 and enable '$MOD_NAME' in the mod menu."
