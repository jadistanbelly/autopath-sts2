# AutoPath — Slay the Spire 2 Mod

Auto-advances your character on the map when only one path is available. No more clicking through obvious choices.

## Features

- **Auto-advance** — when there's only one travelable node on the map, your character moves there automatically after a short delay
- **YOLO Mode** — enable to auto-advance through ALL paths, picking randomly at forks (toggle in ModConfig settings)
- **Configurable delay** — adjust the selection delay from 0.5s to 10s via ModConfig settings
- **Multiplayer compatible** — auto-votes for you in co-op; other players still need to agree
- **Lightweight** — small Harmony patches, no performance impact

## Installation

1. Download `AutoPath.zip` from [Releases](../../releases)
2. Extract into `<STS2 install>/mods/` — this creates a `mods/AutoPath/` folder with everything inside
3. Launch STS2 and enable AutoPath in the mod menu

### Optional: ModConfig Integration

Install the [ModConfig](https://www.nexusmods.com/slaythespire2/mods/27) mod to get an in-game settings panel for AutoPath with:
- Selection Delay slider (0.5–10 seconds)
- YOLO Mode toggle

Without ModConfig, AutoPath works with default settings (0.5s delay, YOLO off).

## Building from Source

**Requirements:** .NET 9.0+ SDK, Slay the Spire 2 installed via Steam

```bash
# Clone the repo
git clone https://github.com/jadistanbelly/autopath-sts2.git
cd autopath-sts2

# Linux / macOS — build and deploy (auto-detects OS)
./scripts/build-and-deploy.sh

# Windows (PowerShell)
.\scripts\build-and-deploy.ps1
```

Release builds also produce `bin/Release/AutoPath.zip` ready for distribution:

```bash
dotnet build -c Release
```

The build auto-detects your platform and STS2 install location. If Steam is installed in a non-default location, override with:

```bash
# Linux/macOS — custom Steam path
STS2_MODS="/path/to/STS2/mods" ./scripts/build-and-deploy.sh

# Windows PowerShell — custom Steam path
$env:STS2_MODS="D:\Steam\steamapps\common\Slay the Spire 2\mods"; .\scripts\build-and-deploy.ps1

# Or override in the build directly (any OS)
dotnet build -c Release -p:STS2Path="/path/to/Slay the Spire 2"
```

## Releasing

GitHub-hosted runners do not have the STS2 game assemblies required to compile mods, so releases are built locally on a machine with STS2 installed. The release helper is reusable across STS2 mod repos and reads `.sts2-release.env` for per-mod settings.

```bash
# Bump 1.3.2 -> 1.3.3
./scripts/release.sh patch

# Bump 1.3.2 -> 1.4.0
./scripts/release.sh minor

# Bump 1.3.2 -> 2.0.0
./scripts/release.sh major
```

The script updates `AutoPath.json`, builds and packages `bin/Release/AutoPath.zip`, commits `chore(release): vX.Y.Z`, tags `vX.Y.Z`, pushes the branch and tag, and creates the GitHub Release.

**Requirements:** STS2 installed locally, [GitHub CLI](https://cli.github.com) (`gh`) installed and authenticated, and a clean git working tree.

### Reusing the release script for other STS2 mods

Copy `scripts/release-sts2-mod.sh` into the mod repo and add a `.sts2-release.env` file:

```bash
MOD_MANIFEST="MyMod.json"
PROJECT_FILE="MyMod.csproj"
ASSEMBLY_NAME="MyMod"
```

Optional settings:

```bash
BUILD_CONFIGURATION="Release"
OUTPUT_DIR="bin/Release"
RELEASE_ASSET_PATH="bin/Release/MyMod.zip"
PACKAGE_EXTRA_FILES=("MyMod.pck" "assets")
BUILD_COMMAND=(dotnet build "MyMod.csproj" -c Release --nologo)
```

## How It Works

AutoPath uses [Harmony](https://github.com/pardeike/Harmony) to hook into the map screen. After the game recalculates which map nodes are travelable, AutoPath counts them:

- **1 travelable node** → auto-select after the configured delay
- **Multiple nodes** → do nothing (unless YOLO mode is on, then pick randomly)
- **0 nodes** → do nothing

It also detects when you're peeking at the map (e.g. during rewards) and won't auto-advance until you're back to normal map navigation.

## About

This mod was vibe-coded and designed purely for fun. Built with AI assistance — expect rough edges and chaotic energy.

## License

[Unlicense](LICENSE) — public domain. Do whatever you want with it.
