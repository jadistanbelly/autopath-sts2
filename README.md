# AutoPath — Slay the Spire 2 Mod

Auto-advances your character on the map when only one path is available. No more clicking through obvious choices.

## Features

- **Auto-advance** — when there's only one travelable node on the map, your character moves there automatically after a short delay
- **YOLO Mode** — enable to auto-advance through ALL paths, picking randomly at forks (toggle in ModConfig settings)
- **Configurable delay** — adjust the selection delay from 0.5s to 10s via ModConfig settings
- **Multiplayer compatible** — auto-votes for you in co-op; other players still need to agree
- **Lightweight** — single Harmony patch, no performance impact

## Installation

1. Download `AutoPath.dll` and `AutoPath.json` from [Releases](../../releases)
2. Create a folder: `<STS2 install>/mods/AutoPath/`
3. Place both files in that folder
4. Launch STS2 → Settings → Mods → Enable **AutoPath**

### Optional: ModConfig Integration

Install the [ModConfig](https://www.nexusmods.com/slaytheSpire2/mods/56) mod to get an in-game settings panel for AutoPath with:
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
./build-and-deploy.sh

# Windows (PowerShell)
.\build-and-deploy.ps1
```

The build auto-detects your platform and STS2 install location. If Steam is installed in a non-default location, override with:

```bash
# Linux/macOS — custom Steam path
STS2_MODS="/path/to/STS2/mods" ./build-and-deploy.sh

# Windows PowerShell — custom Steam path
$env:STS2_MODS="D:\Steam\steamapps\common\Slay the Spire 2\mods"; .\build-and-deploy.ps1

# Or override in the build directly (any OS)
dotnet build -c Release -p:STS2Path="/path/to/Slay the Spire 2"
```

## How It Works

AutoPath uses [Harmony](https://github.com/pardeike/Harmony) to patch `NMapScreen.RecalculateTravelability`. After the game recalculates which map nodes are travelable, AutoPath counts them:

- **1 travelable node** → auto-select after the configured delay
- **Multiple nodes** → do nothing (unless YOLO mode is on, then pick randomly)
- **0 nodes** → do nothing

## License

[Unlicense](LICENSE) — public domain. Do whatever you want with it.
