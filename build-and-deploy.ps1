$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ModName = "AutoPath"
$STS2Mods = if ($env:STS2_MODS) {
    $env:STS2_MODS
} else {
    "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\mods"
}
$DeployDir = Join-Path $STS2Mods $ModName

Write-Host "=== Building $ModName ==="
Set-Location $ScriptDir
dotnet build -c Release --nologo -v quiet
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "=== Deploying to $DeployDir ==="
New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null
Copy-Item "bin\Release\$ModName.dll" $DeployDir -Force
Copy-Item "$ModName.json" $DeployDir -Force

Write-Host "=== Done ==="
Write-Host "  $DeployDir\$ModName.dll"
Write-Host "  $DeployDir\$ModName.json"
Write-Host ""
Write-Host "Launch STS2 and enable '$ModName' in the mod menu."
