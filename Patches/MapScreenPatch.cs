using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;

namespace AutoPath.Patches;

/// <summary>
/// Primary hook: fires after the game recalculates which map nodes are travelable.
/// </summary>
[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapScreenPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance) => AutoAdvanceScheduler.TrySchedule(__instance);
}

/// <summary>
/// Secondary hook: fires when travel becomes enabled on the map (catches act start).
/// </summary>
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.SetTravelEnabled))]
public static class TravelEnabledPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance, bool enabled)
    {
        if (enabled)
            AutoAdvanceScheduler.TrySchedule(__instance);
    }
}

/// <summary>
/// Shared auto-advance logic used by all hooks.
/// Uses a generation counter to safely handle overlapping timers from multiple hooks.
/// </summary>
public static class AutoAdvanceScheduler
{
    private static int _pendingGeneration;
    private static readonly Random Rng = new();

    public static void TrySchedule(NMapScreen screen)
    {
        if (screen.IsTraveling || !screen.IsTravelEnabled)
            return;

        var travelable = CollectTravelable(screen);
        if (travelable.Count == 0)
            return;

        if (travelable.Count > 1 && !AutoPathConfig.YoloMode)
            return;

        // Bump generation to invalidate any previously scheduled timer
        var generation = ++_pendingGeneration;

        var tree = screen.GetTree();
        if (tree == null)
            return;

        tree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
            OnTimerFired(screen, generation);
    }

    private static void OnTimerFired(NMapScreen screen, int generation)
    {
        // Stale timer — a newer schedule superseded this one
        if (generation != _pendingGeneration)
            return;

        if (!GodotObject.IsInstanceValid(screen))
            return;
        if (!screen.IsInsideTree() || !screen.IsOpen)
            return;
        if (screen.IsTraveling || !screen.IsTravelEnabled)
            return;

        if (!IsMapActive())
            return;

        var fresh = CollectTravelable(screen);
        if (fresh.Count == 0)
            return;
        if (fresh.Count > 1 && !AutoPathConfig.YoloMode)
            return;

        var target = fresh.Count == 1
            ? fresh[0]
            : fresh[Rng.Next(fresh.Count)];

        if (!GodotObject.IsInstanceValid(target))
            return;

        screen.OnMapPointSelectedLocally(target);
    }

    /// <summary>
    /// Returns true only when the player is on the map with no overlays blocking it.
    /// Prevents auto-advancing while in a shop, event, rewards screen, etc.
    /// </summary>
    private static bool IsMapActive()
    {
        try
        {
            // Guard 1: No overlay screens (shop inventory, event choices, rewards, etc.)
            var overlayStack = Traverse.Create(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack))
                .Property("Instance").GetValue();
            if (overlayStack != null)
            {
                var screenCount = Traverse.Create(overlayStack).Property("ScreenCount").GetValue<int>();
                if (screenCount > 0)
                    return false;
            }
        }
        catch
        {
            // NOverlayStack unavailable — fail closed
            return false;
        }

        try
        {
            // Guard 2: Current room is the map (not shop, event, combat, etc.)
            var runManager = Traverse.Create(typeof(MegaCrit.Sts2.Core.Runs.RunManager))
                .Property("Instance").GetValue();
            if (runManager == null)
                return false;

            var state = Traverse.Create(runManager).Property("State").GetValue();
            if (state == null)
                return false;

            var currentRoom = Traverse.Create(state).Property("CurrentRoom").GetValue<AbstractRoom>();
            if (currentRoom == null)
                return false;

            if (currentRoom.RoomType != RoomType.Map)
                return false;
        }
        catch
        {
            // RunManager state unavailable — fail closed
            return false;
        }

        return true;
    }

    private static List<NMapPoint> CollectTravelable(NMapScreen screen)
    {
        var results = new List<NMapPoint>();
        var pointsControl = Traverse.Create(screen).Field("_points").GetValue<Control>();
        if (pointsControl != null)
            CollectTravelableRecursive(pointsControl, results);
        return results;
    }

    private static void CollectTravelableRecursive(Node parent, List<NMapPoint> results)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is NMapPoint mapPoint && mapPoint.State == MapPointState.Travelable)
            {
                results.Add(mapPoint);
            }
            else if (child.GetChildCount() > 0)
            {
                CollectTravelableRecursive(child, results);
            }
        }
    }
}
