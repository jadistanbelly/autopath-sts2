#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
release_script="$repo_root/scripts/release-sts2-mod.sh"

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

read_manifest_version() {
    python3 - "$1" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as handle:
    print(json.load(handle)["version"])
PY
}

assert_zip_contains() {
    local zip_path="$1"
    local entry="$2"
    python3 - "$zip_path" "$entry" <<'PY'
import sys
import zipfile
zip_path, entry = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(zip_path) as archive:
    if entry not in archive.namelist():
        raise SystemExit(f"{entry} missing from {zip_path}")
PY
}

run_case() {
    local bump="$1"
    local initial="$2"
    local expected="$3"
    local tmp
    tmp="$(mktemp -d)"

    cp "$release_script" "$tmp/release-sts2-mod.sh"
    cd "$tmp"
    git init -q
    git config user.email test@example.invalid
    git config user.name "Release Test"

    cat > DemoMod.json <<JSON
{
  "id": "DemoMod",
  "name": "Demo Mod",
  "author": "Test",
  "description": "Test mod",
  "version": "$initial",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [],
  "affects_gameplay": false
}
JSON

    cat > DemoMod.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>
XML

    cat > fake-build.sh <<'SH'
#!/usr/bin/env bash
set -euo pipefail
mkdir -p bin/Release
printf 'fake dll\n' > bin/Release/DemoMod.dll
SH
    chmod +x fake-build.sh

    cat > .sts2-release.env <<'ENV'
MOD_MANIFEST="DemoMod.json"
PROJECT_FILE="DemoMod.csproj"
ASSEMBLY_NAME="DemoMod"
BUILD_COMMAND=("./fake-build.sh")
ENV

    git add DemoMod.json DemoMod.csproj fake-build.sh .sts2-release.env release-sts2-mod.sh
    git commit -q -m "initial"

    bash ./release-sts2-mod.sh "$bump" --no-push --no-release

    [[ "$(read_manifest_version DemoMod.json)" == "$expected" ]] \
        || fail "$bump did not update manifest to $expected"
    git rev-parse "v$expected" >/dev/null \
        || fail "$bump did not create tag v$expected"
    [[ "$(git log -1 --pretty=%s)" == "chore(release): v$expected" ]] \
        || fail "$bump created unexpected release commit message"
    [[ -f "bin/Release/DemoMod.zip" ]] \
        || fail "$bump did not create release zip"
    assert_zip_contains "bin/Release/DemoMod.zip" "DemoMod/DemoMod.dll"
    assert_zip_contains "bin/Release/DemoMod.zip" "DemoMod/DemoMod.json"
}

run_case patch 1.2.3 1.2.4
run_case minor 1.2.3 1.3.0
run_case major 1.2.3 2.0.0

printf 'release-sts2-mod checks passed\n'
