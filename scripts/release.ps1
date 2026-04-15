$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
Set-Location $ProjectDir

$ModName = "AutoPath"

# Extract version from mod manifest
$Manifest = Get-Content "$ModName.json" | ConvertFrom-Json
$Version = $Manifest.version
if (-not $Version) {
    Write-Error "Could not read version from $ModName.json"
    exit 1
}
$Tag = "v$Version"

Write-Host "=== Releasing $ModName $Tag ==="

# Pre-flight checks
$GitStatus = git diff --quiet HEAD 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "You have uncommitted changes. Commit or stash them first."
    exit 1
}

$TagExists = git rev-parse $Tag 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Error "Tag $Tag already exists. Update version in $ModName.json first."
    exit 1
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Error "GitHub CLI (gh) is required. Install: https://cli.github.com"
    exit 1
}

# Build (MSBuild target creates the zip automatically)
Write-Host "=== Building ==="
dotnet build -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$Zip = "bin\Release\$ModName.zip"
if (-not (Test-Path $Zip)) {
    Write-Error "Expected $Zip not found after build"
    exit 1
}

# Tag and push
Write-Host "=== Tagging $Tag ==="
git tag -a $Tag -m "Release $Tag"
git push origin $Tag

# Create GitHub Release with the zip attached
Write-Host "=== Creating GitHub Release ==="
gh release create $Tag $Zip --title "$ModName $Tag" --generate-notes

$RepoUrl = gh repo view --json url -q ".url"
Write-Host ""
Write-Host "=== Released $ModName $Tag ==="
Write-Host "  $RepoUrl/releases/tag/$Tag"
