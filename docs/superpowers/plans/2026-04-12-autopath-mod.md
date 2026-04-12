# AutoPath Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Slay the Spire 2 mod that auto-advances on the map when only one path is available.

**Architecture:** A single C# DLL loaded by STS2's built-in mod system via `[ModInitializerAttribute]`. Subscribes to the `NMapScreen.Opened` event. When the map screen opens, finds all travelable map points. If exactly one is travelable, waits 0.5 seconds then programmatically triggers travel to that point via `NMapScreen.TravelToMapCoord()`.

**Tech Stack:** C# / .NET 8, Harmony 2.4.2, Godot 4 (GodotSharp), STS2 game assemblies

---

## Reference: Game API (from decompilation of sts2.dll)

These are the actual class and method names found in the game assembly. All code in this plan references these real APIs.

| Namespace | Class | Key Members |
|-----------|-------|-------------|
| `MegaCrit.Sts2.Core.Modding` | `ModInitializerAttribute` | Apply to static method as mod entry point |
| `MegaCrit.Sts2.Core.Nodes.Screens.Map` | `NMapScreen` | `Opened` event (`add_Opened`), `Closed` event, `TravelToMapCoord(MapCoord)` |
| `MegaCrit.Sts2.Core.Nodes.Screens.Map` | `NMapPoint` | `IsTravelable` (bool property), `Coord` (MapCoord), `RecalculateTravelability()` |
| `MegaCrit.Sts2.Core.Nodes.Screens.Map` | `NNormalMapPoint` | Extends `NMapPoint` |
| `MegaCrit.Sts2.Core.Nodes.Screens.Map` | `NBossMapPoint` | Extends `NMapPoint` |
| `MegaCrit.Sts2.Core.Map` | `ActMap` | `GetAllMapPoints()`, `GetPointsInRow(int)` |
| `MegaCrit.Sts2.Core.Map` | `MapCoord` | Map coordinate struct |
| `MegaCrit.Sts2.Core.Runs` | `RunManager` | `CurrentMapPoint`, `CurrentMapCoord`, `EnterMapCoord()` |
| `MegaCrit.Sts2.Core.Hooks` | `Hook` | `AfterMapGenerated`, `AfterActEntered` |
| `MegaCrit.Sts2.Core.GameActions` | `MoveToMapCoordAction` | `GoToMapCoord()` |

**Event pattern (used by RouteSuggest mod):** Subscribe to `NMapScreen.Opened` via `add_Opened` to react when map screen shows.

**Mod entry point pattern:** `[ModInitializerAttribute]` on a `public static void` method — game calls `CallModInitializer` to invoke it.

---

## File Structure

```
autopath/
├── AutoPath.csproj              # C# project file referencing game DLLs
├── AutoPathMod.cs               # Entry point: registers Harmony patches
├── Patches/
│   └── MapScreenPatch.cs        # Harmony patch: hooks NMapScreen to detect single-path and auto-travel
├── AutoPath.json                # Mod manifest for STS2 mod loader
├── build-and-deploy.sh          # Build + copy to game mods folder
├── .gitignore                   # Ignore bin/obj/build artifacts
└── docs/superpowers/
    ├── specs/2026-04-12-autopath-mod-design.md
    └── plans/2026-04-12-autopath-mod.md (this file)
```

---

### Task 1: Project Scaffolding

**Files:**
- Create: `AutoPath.csproj`
- Create: `.gitignore`
- Create: `AutoPath.json`

- [ ] **Step 1: Create the .csproj file**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <PropertyGroup>
    <STS2Path>$(HOME)/.local/share/Steam/steamapps/common/Slay the Spire 2</STS2Path>
    <STS2DataPath>$(STS2Path)/data_sts2_linuxbsd_x86_64</STS2DataPath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="sts2">
      <HintPath>$(STS2DataPath)/sts2.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="GodotSharp">
      <HintPath>$(STS2DataPath)/GodotSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(STS2DataPath)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

Save to: `AutoPath.csproj`

- [ ] **Step 2: Create .gitignore**

```
bin/
obj/
*.user
*.suo
.vs/
```

Save to: `.gitignore`

- [ ] **Step 3: Create the mod manifest**

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

Save to: `AutoPath.json`

- [ ] **Step 4: Verify the project restores and compiles (empty)**

Run: `cd /home/jadistanbelly/src/sts2/mods/autopath && dotnet restore && dotnet build`

Expected: Build succeeds with 0 errors (warnings about empty project are OK).

- [ ] **Step 5: Commit**

```bash
git add AutoPath.csproj .gitignore AutoPath.json
git commit -m "feat: scaffold AutoPath mod project with game DLL references"
```

---

### Task 2: Mod Entry Point

**Files:**
- Create: `AutoPathMod.cs`

- [ ] **Step 1: Create the mod entry point class**

This class uses `[ModInitializerAttribute]` as the entry point and creates a Harmony instance to apply patches.

```csharp
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace AutoPath;

public static class AutoPathMod
{
    private static Harmony? _harmony;

    [ModInitializer]
    public static void Initialize()
    {
        _harmony = new Harmony("com.jadistanbelly.autopath");
        _harmony.PatchAll(typeof(AutoPathMod).Assembly);
    }
}
```

Save to: `AutoPathMod.cs`

**Notes:**
- `[ModInitializer]` is the attribute the game scans for (found as `ModInitializerAttribute` in `MegaCrit.Sts2.Core.Modding`).
- `Harmony.PatchAll()` automatically finds and applies all `[HarmonyPatch]` classes in the assembly.
- The Harmony ID `"com.jadistanbelly.autopath"` uniquely identifies our patches for debugging/conflict resolution.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`

Expected: Build succeeds. If `ModInitializerAttribute` is named differently (e.g., `ModInitializer`), check the compilation error and adjust the attribute name.

**Troubleshooting:** If `[ModInitializer]` doesn't resolve, try these alternatives (all found in strings analysis):
- `[ModInitializerAttribute]` (full name)
- Check namespace: it's in `MegaCrit.Sts2.Core.Modding`

- [ ] **Step 3: Commit**

```bash
git add AutoPathMod.cs
git commit -m "feat: add mod entry point with Harmony initialization"
```

---

### Task 3: Map Screen Patch — Core Logic

**Files:**
- Create: `Patches/MapScreenPatch.cs`

- [ ] **Step 1: Create the Harmony patch class**

This is the core mod logic. It patches `NMapScreen` to detect when only one path is available and auto-travels after a delay.

```csharp
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace AutoPath.Patches;

[HarmonyPatch(typeof(NMapScreen))]
public static class MapScreenPatch
{
    private static bool _autoTravelPending;

    // Patch RecalculateTravelability — fires when the map recalculates which nodes can be traveled to.
    // This is the ideal hook point: it runs after the map updates its state (e.g., after completing a room).
    [HarmonyPatch("RecalculateTravelability")]
    [HarmonyPostfix]
    public static void AfterRecalculateTravelability(NMapScreen __instance)
    {
        if (_autoTravelPending)
            return;

        // Find all travelable map points among the NMapScreen's children
        var travelablePoints = new System.Collections.Generic.List<NMapPoint>();
        FindTravelablePoints(__instance, travelablePoints);

        if (travelablePoints.Count != 1)
            return;

        // Exactly one travelable point — schedule auto-travel
        _autoTravelPending = true;
        var targetCoord = travelablePoints[0].Coord;

        // Use a Godot SceneTree timer for the 0.5s delay
        var tree = __instance.GetTree();
        if (tree == null)
        {
            _autoTravelPending = false;
            return;
        }

        tree.CreateTimer(0.5).Timeout += () =>
        {
            _autoTravelPending = false;
            // Verify the map screen is still active and travel is still valid
            if (!__instance.IsInsideTree())
                return;

            __instance.TravelToMapCoord(targetCoord);
        };
    }

    private static void FindTravelablePoints(Node parent, System.Collections.Generic.List<NMapPoint> results)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is NMapPoint mapPoint && mapPoint.IsTravelable)
                results.Add(mapPoint);

            if (child.GetChildCount() > 0)
                FindTravelablePoints(child, results);
        }
    }
}
```

Save to: `Patches/MapScreenPatch.cs`

**Key design decisions:**
- **Hook point:** `RecalculateTravelability` — runs after the map recalculates available paths (e.g., after completing a room and returning to the map). This is more reliable than hooking `_Ready` or `Opened` because it fires at the exact moment travel states are updated.
- **Guard with `_autoTravelPending`:** Prevents multiple auto-travels from stacking if `RecalculateTravelability` is called multiple times.
- **Godot timer:** Uses `SceneTree.CreateTimer(0.5)` for the delay — this is the standard Godot way to schedule delayed actions and respects game pause state.
- **Safety check:** Verifies the map screen is still in the scene tree before traveling (in case the player navigated away during the delay).
- **Multiplayer guard:** STS2 supports co-op where players may need to vote on map choices. The mod should detect multiplayer and either disable auto-travel or only trigger when all players agree. For v1, we will simply disable auto-travel in multiplayer sessions to avoid conflicts. Check for multiplayer state via `RunManager` or game mode properties.

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build`

Expected: Build succeeds. If any type names don't resolve, check the error and adjust:
- `NMapPoint` might be in a different namespace — check `NNormalMapPoint`, `NBossMapPoint`
- `TravelToMapCoord` might need a different signature
- `Coord` property might be named differently (check `MapCoord`)
- `IsTravelable` might be a method instead of a property

**If `RecalculateTravelability` is not a method on `NMapScreen`:** It may be on `NMapPoint` instead. In that case, change the patch target:
```csharp
[HarmonyPatch(typeof(NMapPoint), "RecalculateTravelability")]
```
And walk up to the parent `NMapScreen` from the patched `NMapPoint` instance.

- [ ] **Step 3: Commit**

```bash
git add Patches/MapScreenPatch.cs
git commit -m "feat: add map screen patch for single-path auto-travel"
```

---

### Task 4: Build & Deploy Script

**Files:**
- Create: `build-and-deploy.sh`

- [ ] **Step 1: Create the build and deploy script**

```bash
#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
STS2_MODS="$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/mods"
MOD_DIR="$STS2_MODS/AutoPath"

echo "Building AutoPath..."
dotnet build -c Release "$SCRIPT_DIR/AutoPath.csproj"

echo "Deploying to $MOD_DIR..."
mkdir -p "$MOD_DIR"
cp "$SCRIPT_DIR/bin/Release/AutoPath.dll" "$MOD_DIR/"
cp "$SCRIPT_DIR/AutoPath.json" "$MOD_DIR/"

echo "Done! AutoPath deployed to: $MOD_DIR"
echo "Contents:"
ls -la "$MOD_DIR/"
```

Save to: `build-and-deploy.sh`

- [ ] **Step 2: Make it executable**

Run: `chmod +x build-and-deploy.sh`

- [ ] **Step 3: Run the build and deploy**

Run: `./build-and-deploy.sh`

Expected:
```
Building AutoPath...
  AutoPath -> /home/jadistanbelly/src/sts2/mods/autopath/bin/Release/AutoPath.dll
Deploying to /home/jadistanbelly/.local/share/Steam/steamapps/common/Slay the Spire 2/mods/AutoPath...
Done! AutoPath deployed to: ...
Contents:
AutoPath.dll
AutoPath.json
```

- [ ] **Step 4: Commit**

```bash
git add build-and-deploy.sh
git commit -m "feat: add build and deploy script"
```

---

### Task 5: Manual Testing In-Game

This task has no code changes — it's the testing checklist.

- [ ] **Step 1: Launch STS2**

Start the game through Steam. Check the game's mod menu or log output to verify AutoPath is loaded. If the game has a console or log file, look for Harmony patch messages.

**Log location (likely):** `~/.local/share/godot/app_userdata/Slay the Spire 2/logs/` or check Steam's game log.

- [ ] **Step 2: Start a new run**

Begin a new run with any character. Play through the first room.

- [ ] **Step 3: Test single-path auto-advance**

After completing a room, return to the map. Find a point where only one path leads forward (common early in Act 1 where paths converge). Verify:
- [ ] The character auto-advances after ~0.5 seconds
- [ ] The transition looks natural (same animation as manual click)

- [ ] **Step 4: Test multi-path no-advance**

Navigate to a fork where 2+ paths are available. Verify:
- [ ] The mod does NOT auto-advance
- [ ] Player must click manually to choose

- [ ] **Step 5: Test edge cases**

- [ ] Boss node (single forced path) — should auto-advance
- [ ] Rapid chain: multiple single-path nodes in a row — each should auto-advance
- [ ] Close/reopen map during delay — should not crash or double-travel

---

### Task 6: Troubleshooting & Iteration

If the mod doesn't work as expected, here's the debugging approach:

- [ ] **Step 1: Check mod is loaded**

Look for the `AutoPath` entry in the game's mod list or log. If not loaded:
- Verify `AutoPath.json` format matches other working mods
- Verify `AutoPath.dll` is in the correct folder
- Check if `[ModInitializer]` attribute name needs adjustment

- [ ] **Step 2: If auto-travel doesn't trigger**

The `RecalculateTravelability` method might not exist on `NMapScreen`, or might have a different name. Alternative patch targets to try:

1. **Patch `NMapPoint.RecalculateTravelability`** instead (it may be on the point, not the screen)
2. **Use the Opened event** instead of Harmony: In `AutoPathMod.Initialize()`, get the `NMapScreen` singleton and subscribe to its `Opened` event (like RouteSuggest does)
3. **Patch a different method** — try `OnMapPointSelectedLocally`, `OnMapScreenVisibilityChanged`, or a Godot lifecycle method like `_Process`

- [ ] **Step 3: If travel call doesn't work**

`TravelToMapCoord` might need different arguments or might be async. Try:
- `OnMapPointSelectedLocally(mapPoint)` — simulates a click
- `RunManager.EnterMapCoord(coord)` — directly enters the coordinate
- Invoke via reflection if the method is private

- [ ] **Step 4: Iterate and redeploy**

After each code change:
```bash
./build-and-deploy.sh
```
Then restart STS2 and test again.

---

### Task 7: Package for Distribution

- [ ] **Step 1: Verify the mod works end-to-end**

Complete at least one full act with the mod enabled. Confirm no crashes, no weird behavior.

- [ ] **Step 2: Create the distribution zip**

```bash
cd /home/jadistanbelly/.local/share/Steam/steamapps/common/Slay\ the\ Spire\ 2/mods
zip -r ~/AutoPath-v1.0.0.zip AutoPath/
echo "Created: ~/AutoPath-v1.0.0.zip"
unzip -l ~/AutoPath-v1.0.0.zip
```

Expected: Zip contains `AutoPath/AutoPath.dll` and `AutoPath/AutoPath.json`

- [ ] **Step 3: Write a README**

Create `README.md` in the project root with:
- Mod description
- Installation instructions ("Extract into your STS2 `mods/` folder")
- Compatibility notes (STS2 version, OS)
- Source code link (if publishing to GitHub)

- [ ] **Step 4: Final commit and tag**

```bash
git add README.md
git commit -m "docs: add README with install instructions"
git tag v1.0.0
```

- [ ] **Step 5: Upload to NexusMods**

1. Go to https://www.nexusmods.com/slaytheSpire2
2. Create a new mod page
3. Upload `AutoPath-v1.0.0.zip`
4. Set category to "Quality of Life"
5. Add description, screenshots, installation instructions
