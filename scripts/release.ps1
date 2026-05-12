$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ReleaseScript = Join-Path $ScriptDir "release-sts2-mod.sh"

if (-not (Get-Command bash -ErrorAction SilentlyContinue)) {
    Write-Error "bash is required to run the reusable STS2 release helper. Use ./scripts/release.sh patch|minor|major from a shell with bash available."
    exit 1
}

& bash $ReleaseScript @args
exit $LASTEXITCODE
