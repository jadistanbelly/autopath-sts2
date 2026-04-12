# AutoPath — Slay the Spire 2 Mod Design

**Date:** 2026-04-12
**Author:** jadistanbelly
**Status:** Approved

## Summary

A quality-of-life mod for Slay the Spire 2 that automatically advances the player on the map when only a single path is available. Eliminates unnecessary clicks when there are no meaningful choices to make.

## Behavior

- **Trigger condition:** Exactly one next node is reachable from the player's current position on the map
- **Action:** After a ~0.5 second delay, the mod programmatically selects and advances to that single node
- **Stop condition:** When multiple paths are available (a fork), the mod does nothing — the player makes their own choice
- **Safety guards:**
  - Does not fire during battle, events, shops, or other non-map screens
  - Does not fire if the map screen is still loading or mid-animation
  - Does not fire if the player has already clicked a node manually

## Technical Architecture

### Platform

- **Language:** C# (.NET 8)
- **Game engine:** Godot 4 with C# bindings
- **Patching framework:** Harmony (0Harmony.dll, shipped with the game)
- **Game assembly:** `sts2.dll` (contains all game logic)

### Mod Structure

```
AutoPath/
├── AutoPath.dll       # Compiled C# mod assembly
└── AutoPath.json      # Mod manifest
```

No `.pck` file needed — the mod has no custom scenes, textures, or UI elements.

### Manifest (AutoPath.json)

```json
{
  "id": "AutoPath",
  "name": "AutoPath",
  "author": "jadistanbelly",
  "description": "Auto-advances on the map when only one path is available. No unnecessary clicks.",
  "version": "1.0.0",
  "has_pck": false,
  "has_dll": true,
  "dependencies": [],
  "affects_gameplay": false
}
```

### How It Works

1. **Entry point:** A static constructor or `[HarmonyPatch]` attribute class that runs when the game loads the DLL. Registers all Harmony patches.
2. **Map screen hook:** A Harmony Postfix (or Prefix) patch on the game's map node selection/rendering method. This fires whenever the map screen updates available paths.
3. **Path count check:** Reads the list of reachable next nodes from the game's map data structure. If `count == 1`, proceeds to step 4. If `count != 1`, does nothing.
4. **Delayed auto-advance:** Waits ~0.5 seconds (using `SceneTreeTimer`, `Task.Delay`, or Godot's `CreateTimer`), then calls the same method the game uses when the player clicks a map node — passing the single available node as the target.

### Key Unknown: Game API

The exact class names, method signatures, and data structures for map navigation in `sts2.dll` are unknown until we decompile it. The implementation plan includes a decompilation step to discover:

- The map screen class (likely something like `MapScreen`, `MapView`, or `RunMap`)
- The method that handles node selection/click
- The data structure for available next nodes
- The method/property that returns reachable nodes from the current position

### Dependencies

- **Build-time:** .NET 8 SDK, references to `sts2.dll`, `GodotSharp.dll`, `0Harmony.dll`
- **Runtime:** None beyond what the base game provides
- **No dependency on other mods**

## Project Setup

### Prerequisites

1. .NET 8 SDK
2. `ilspycmd` or equivalent C# decompiler (to inspect `sts2.dll`)

### C# Project

A `.csproj` file referencing the game's DLLs:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="sts2">
      <HintPath>$(STS2_PATH)/data_sts2_linuxbsd_x86_64/sts2.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="GodotSharp">
      <HintPath>$(STS2_PATH)/data_sts2_linuxbsd_x86_64/GodotSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(STS2_PATH)/data_sts2_linuxbsd_x86_64/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### Build & Deploy Script

A `build-and-deploy.sh` script that:
1. Runs `dotnet build -c Release`
2. Copies `AutoPath.dll` + `AutoPath.json` to the game's `mods/AutoPath/` directory

## Testing

### Manual Testing

1. Build and deploy the mod
2. Launch STS2
3. Start a new run
4. Navigate to a map point with a single forward path → verify auto-advance after ~0.5s
5. Navigate to a fork → verify no auto-advance, player must click
6. Verify no interference during battles, events, shops, rest sites

### Edge Cases

- Map screen opened but player hasn't completed the current node yet
- Boss node (always single path to boss) — should auto-advance
- Final boss / act transitions — verify behavior is correct
- Player clicks manually before the 0.5s delay fires — should cancel the auto-advance
- Multiple rapid auto-advances in a row (chain of single-path nodes)

## Deployment

1. Zip the `AutoPath/` folder (containing `AutoPath.dll` + `AutoPath.json`)
2. Upload to NexusMods under Slay the Spire 2
3. Installation instructions: "Extract into your STS2 `mods/` folder"

## Future Enhancements (v2, not in scope)

- ModConfig integration for in-game toggle and configurable delay
- Visual indicator showing auto-advance is about to trigger
- Skip animation option for even faster advancement
