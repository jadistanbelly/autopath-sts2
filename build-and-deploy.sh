#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_NAME="AutoPath"
STS2_MODS="$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/mods"
DEPLOY_DIR="$STS2_MODS/$MOD_NAME"

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
