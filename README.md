# AutoPath - Slay the Spire 2 Mod

Auto-advances your character on the map when only one path is available. No more clicking through obvious choices.

## Features

- **Auto-advance** - moves to the only available map node after a short delay.
- **Fork safety** - waits for your input when multiple paths are available.
- **YOLO Mode** - optionally picks a random path at forks.
- **Configurable delay** - adjust the delay from 0.5 to 10 seconds with ModConfig.
- **Multiplayer compatible** - auto-votes for you in co-op; other players still need to agree.

## Installation

1. Download `AutoPath.zip` from [Releases](../../releases)
2. Extract it into `<Slay the Spire 2 install>/mods/`
   - The final layout should be `mods/AutoPath/AutoPath.dll` and `mods/AutoPath/AutoPath.json`
3. Launch STS2 and enable AutoPath in the mod menu

## Settings

AutoPath works without any extra mods using the defaults: 0.5 second delay and YOLO Mode off.

Install [ModConfig](https://www.nexusmods.com/slaythespire2/mods/27) to change settings in-game:

- Selection Delay slider (0.5 to 10 seconds)
- YOLO Mode toggle

## Behavior Notes

- At one available path, AutoPath selects it automatically.
- At a fork, AutoPath waits for you unless YOLO Mode is enabled.
- When the map is opened from the top bar during rewards or events, AutoPath waits until normal map navigation resumes.
- In multiplayer, AutoPath submits your vote only. It does not force other players to choose.

## Building from Source

Requirements:

- .NET 9.0+ SDK
- Slay the Spire 2 installed via Steam

```bash
git clone https://github.com/jadistanbelly/autopath-sts2.git
cd autopath-sts2

# Linux / macOS
./scripts/build-and-deploy.sh

# Windows PowerShell
.\scripts\build-and-deploy.ps1
```

If Steam is installed in a custom location, set `STS2_MODS` to your game's `mods` folder before running the deploy script.

A release build creates `bin/Release/AutoPath.zip`:

```bash
dotnet build -c Release
```

## Maintainer Release

```bash
./scripts/release.sh patch
./scripts/release.sh minor
./scripts/release.sh major
```

The release script requires STS2 installed locally, a clean git working tree, and an authenticated GitHub CLI session.

## How It Works

AutoPath uses [Harmony](https://github.com/pardeike/Harmony) to hook into the map screen. After the game recalculates which map nodes are travelable, AutoPath counts them:

- **1 travelable node** - auto-select after the configured delay
- **Multiple nodes** - wait for player input unless YOLO Mode is on
- **0 nodes** - do nothing

## License

[Unlicense](LICENSE) - public domain.
