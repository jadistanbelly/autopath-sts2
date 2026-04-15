#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

MOD_NAME="AutoPath"

# Extract version from mod manifest
VERSION=$(sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$MOD_NAME.json")
if [[ -z "$VERSION" ]]; then
    echo "Error: Could not read version from $MOD_NAME.json"
    exit 1
fi
TAG="v$VERSION"

echo "=== Releasing $MOD_NAME $TAG ==="

# Pre-flight checks
if ! git diff --quiet HEAD 2>/dev/null; then
    echo "Error: You have uncommitted changes. Commit or stash them first."
    exit 1
fi

if git rev-parse "$TAG" >/dev/null 2>&1; then
    echo "Error: Tag $TAG already exists. Update version in $MOD_NAME.json first."
    exit 1
fi

if ! command -v gh &>/dev/null; then
    echo "Error: GitHub CLI (gh) is required. Install: https://cli.github.com"
    exit 1
fi

# Build (MSBuild target creates the zip automatically)
echo "=== Building ==="
dotnet build -c Release --nologo

ZIP="bin/Release/$MOD_NAME.zip"
if [[ ! -f "$ZIP" ]]; then
    echo "Error: Expected $ZIP not found after build"
    exit 1
fi

# Tag and push
echo "=== Tagging $TAG ==="
git tag -a "$TAG" -m "Release $TAG"
git push origin "$TAG"

# Create GitHub Release with the zip attached
echo "=== Creating GitHub Release ==="
gh release create "$TAG" "$ZIP" \
    --title "$MOD_NAME $TAG" \
    --generate-notes

REPO_URL=$(gh repo view --json url -q .url)
echo ""
echo "=== Released $MOD_NAME $TAG ==="
echo "  ${REPO_URL}/releases/tag/$TAG"
