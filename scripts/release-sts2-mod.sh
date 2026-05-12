#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'EOF'
Usage: release-sts2-mod.sh patch|minor|major [--no-push] [--no-release]

Creates a local STS2 mod release from a repo with a .sts2-release.env file.

Config variables:
  MOD_MANIFEST       Manifest JSON path. Default: AutoPath.json
  PROJECT_FILE       C# project file. Default: <manifest id>.csproj
  ASSEMBLY_NAME      Built DLL basename. Default: <manifest id>
  BUILD_CONFIGURATION Build configuration. Default: Release
  BUILD_COMMAND      Optional bash array command override.
  OUTPUT_DIR         Build output directory. Default: bin/<configuration>
  RELEASE_ASSET_PATH Release zip path. Default: <output>/<manifest id>.zip
  PACKAGE_EXTRA_FILES Optional bash array of extra files/directories copied into the mod folder.
EOF
}

fail() {
    printf 'Error: %s\n' "$1" >&2
    exit 1
}

require_command() {
    command -v "$1" >/dev/null 2>&1 || fail "$1 is required"
}

repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" \
    || fail "must be run inside a git repository"
cd "$repo_root"

bump="${1:-}"
if [[ -z "$bump" || "$bump" == "-h" || "$bump" == "--help" ]]; then
    usage
    exit 0
fi
shift

case "$bump" in
    patch|minor|major) ;;
    *) fail "release type must be patch, minor, or major" ;;
esac

push_enabled=1
release_enabled=1
while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-push) push_enabled=0 ;;
        --no-release) release_enabled=0 ;;
        *) fail "unknown argument: $1" ;;
    esac
    shift
done

require_command git
require_command python3
if [[ "$release_enabled" -eq 1 ]]; then
    require_command gh
fi

if [[ -n "$(git status --porcelain)" ]]; then
    fail "working tree is dirty; commit or stash changes first"
fi

config_file="${STS2_RELEASE_CONFIG:-.sts2-release.env}"
if [[ -f "$config_file" ]]; then
    # shellcheck source=/dev/null
    source "$config_file"
fi

MOD_MANIFEST="${MOD_MANIFEST:-AutoPath.json}"
[[ -f "$MOD_MANIFEST" ]] || fail "manifest not found: $MOD_MANIFEST"

manifest_info="$(python3 - "$MOD_MANIFEST" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    data = json.load(handle)

for key in ("id", "name", "version"):
    value = data.get(key)
    if not isinstance(value, str) or not value:
        raise SystemExit(f"manifest missing non-empty string: {key}")

print(data["id"])
print(data["name"])
print(data["version"])
PY
)"

mapfile -t manifest_lines <<< "$manifest_info"
mod_id="${manifest_lines[0]}"
mod_name="${manifest_lines[1]}"
current_version="${manifest_lines[2]}"

new_version="$(python3 - "$current_version" "$bump" <<'PY'
import re
import sys

version, bump = sys.argv[1], sys.argv[2]
match = re.fullmatch(r"(\d+)\.(\d+)\.(\d+)", version)
if not match:
    raise SystemExit(f"version must be semantic X.Y.Z, got: {version}")

major, minor, patch = map(int, match.groups())
if bump == "major":
    major, minor, patch = major + 1, 0, 0
elif bump == "minor":
    minor, patch = minor + 1, 0
elif bump == "patch":
    patch += 1
else:
    raise SystemExit(f"unknown bump: {bump}")

print(f"{major}.{minor}.{patch}")
PY
)"
tag="v$new_version"

if git rev-parse "$tag" >/dev/null 2>&1; then
    fail "tag already exists: $tag"
fi

PROJECT_FILE="${PROJECT_FILE:-$mod_id.csproj}"
ASSEMBLY_NAME="${ASSEMBLY_NAME:-$mod_id}"
BUILD_CONFIGURATION="${BUILD_CONFIGURATION:-Release}"
OUTPUT_DIR="${OUTPUT_DIR:-bin/$BUILD_CONFIGURATION}"
RELEASE_ASSET_PATH="${RELEASE_ASSET_PATH:-$OUTPUT_DIR/$mod_id.zip}"

printf '=== Releasing %s %s -> %s (%s) ===\n' "$mod_name" "$current_version" "$new_version" "$bump"

python3 - "$MOD_MANIFEST" "$new_version" <<'PY'
import json
import sys

manifest_path, version = sys.argv[1], sys.argv[2]
with open(manifest_path, encoding="utf-8") as handle:
    data = json.load(handle)
data["version"] = version
with open(manifest_path, "w", encoding="utf-8") as handle:
    json.dump(data, handle, indent=2)
    handle.write("\n")
PY

printf '=== Building ===\n'
if declare -p BUILD_COMMAND >/dev/null 2>&1; then
    "${BUILD_COMMAND[@]}"
else
    dotnet build "$PROJECT_FILE" -c "$BUILD_CONFIGURATION" --nologo
fi

dll_path="$OUTPUT_DIR/$ASSEMBLY_NAME.dll"
[[ -f "$dll_path" ]] || fail "expected built DLL not found: $dll_path"

printf '=== Packaging %s ===\n' "$RELEASE_ASSET_PATH"
rm -f "$RELEASE_ASSET_PATH"
package_root="$(mktemp -d)"
trap 'rm -rf "$package_root"' EXIT
mod_package_dir="$package_root/$mod_id"
mkdir -p "$mod_package_dir"
cp "$dll_path" "$mod_package_dir/"
cp "$MOD_MANIFEST" "$mod_package_dir/"

if declare -p PACKAGE_EXTRA_FILES >/dev/null 2>&1; then
    for extra in "${PACKAGE_EXTRA_FILES[@]}"; do
        [[ -e "$extra" ]] || fail "extra package path not found: $extra"
        cp -R "$extra" "$mod_package_dir/"
    done
fi

mkdir -p "$(dirname "$RELEASE_ASSET_PATH")"
python3 - "$mod_package_dir" "$RELEASE_ASSET_PATH" <<'PY'
from pathlib import Path
import sys
import zipfile

mod_dir = Path(sys.argv[1])
zip_path = Path(sys.argv[2])
base = mod_dir.parent

with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(mod_dir.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(base))
PY

git add "$MOD_MANIFEST"
git commit -m "chore(release): $tag"
git tag -a "$tag" -m "Release $tag"

if [[ "$push_enabled" -eq 1 ]]; then
    branch="$(git branch --show-current)"
    [[ -n "$branch" ]] || fail "cannot push from detached HEAD"
    git push origin "$branch"
    git push origin "$tag"
fi

if [[ "$release_enabled" -eq 1 ]]; then
    previous_tag="$(git describe --tags --abbrev=0 HEAD^ 2>/dev/null || true)"
    if [[ -n "$previous_tag" ]]; then
        notes="$(git --no-pager log --pretty=format:"- %s" "$previous_tag..HEAD" --no-merges)"
    else
        notes="$(git --no-pager log --pretty=format:"- %s" --no-merges)"
    fi

    gh release create "$tag" "$RELEASE_ASSET_PATH" \
        --title "$mod_name $tag" \
        --notes "$notes"
fi

printf '=== Released %s ===\n' "$tag"
printf 'Asset: %s\n' "$RELEASE_ASSET_PATH"
