using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace AutoPath.Patches;

[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapScreenPatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        AutoAdvanceScheduler.TrySchedule(__instance);
    }
}

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

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class MapOpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(bool isOpenedFromTopBar)
    {
        AutoAdvanceScheduler.SetPeeking(isOpenedFromTopBar);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
public static class MapClosePatch
{
    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        AutoAdvanceScheduler.CancelPending(__instance);
    }
}

public static class AutoAdvanceScheduler
{
    private readonly record struct PendingAutoAdvance(NMapScreen Screen, int Generation, MapLocation Location);

    private static PendingAutoAdvance? _pending;
    private static bool _isPeeking;
    private static int _pendingGeneration;
    private static readonly Random Rng = new();

    public static void SetPeeking(bool peeking)
    {
        _isPeeking = peeking;
    }

    public static void CancelPending(NMapScreen screen)
    {
        if (_pending?.Screen == screen)
            _pending = null;

        _pendingGeneration++;
        _isPeeking = false;
    }

    public static void TrySchedule(NMapScreen screen)
    {
        if (_pending?.Screen == screen)
            return;
        if (screen.IsTraveling)
            return;
        if (!screen.IsTravelEnabled)
            return;

        var travelable = CollectTravelable(screen);

        if (travelable.Count == 0)
            return;
        if (travelable.Count > 1 && !AutoPathConfig.YoloMode)
            return;

        var scheduledLocation = GetCurrentMapLocation();
        if (!scheduledLocation.HasValue)
            return;

        var generation = ++_pendingGeneration;
        var pending = new PendingAutoAdvance(screen, generation, scheduledLocation.Value);
        _pending = pending;
        GD.Print($"[AutoPath] Scheduling auto-advance ({AutoPathConfig.SelectionDelay}s, {travelable.Count} node(s))");

        var tree = screen.GetTree();
        if (tree == null)
        {
            _pending = null;
            return;
        }

        tree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
            OnTimerFired(pending);
    }

    private static void OnTimerFired(PendingAutoAdvance pending)
    {
        if (!IsPendingCurrent(pending))
            return;

        _pending = null;

        var screen = pending.Screen;
        if (!GodotObject.IsInstanceValid(screen))
            return;
        if (!screen.IsInsideTree())
            return;
        if (!screen.IsOpen)
            return;
        if (screen.IsTraveling)
            return;
        if (!screen.IsTravelEnabled)
            return;

        // Map opened from top bar (peeking during rewards/room) — retry until
        // the game transitions to normal map view via Open(false)
        if (_isPeeking)
        {
            GD.Print("[AutoPath] Peeking — will retry");
            _pending = pending;
            var retryTree = screen.GetTree();
            if (retryTree != null)
                retryTree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
                    OnTimerFired(pending);
            return;
        }

        var fresh = CollectTravelable(screen);
        if (fresh.Count == 0)
            return;
        if (fresh.Count > 1 && !AutoPathConfig.YoloMode)
            return;
        if (!IsPendingCurrent(pending))
            return;

        var target = fresh.Count == 1
            ? fresh[0]
            : fresh[Rng.Next(fresh.Count)];

        if (!GodotObject.IsInstanceValid(target))
            return;

        GD.Print("[AutoPath] Auto-advancing to next node");
        // Selection queues travel asynchronously; keep schedules suppressed until close.
        _pending = pending;
        _pendingGeneration++;
        DisableCurrentRoomProceedButton();
        screen.OnMapPointSelectedLocally(target);
    }

    private static void DisableCurrentRoomProceedButton()
    {
        try
        {
            var run = NRun.Instance;
            var currentRoom = run?.CombatRoom as IRoomWithProceedButton
                ?? run?.TreasureRoom as IRoomWithProceedButton
                ?? run?.RestSiteRoom as IRoomWithProceedButton
                ?? run?.MerchantRoom as IRoomWithProceedButton;
            var proceedButton = currentRoom?.ProceedButton;
            if (proceedButton != null && GodotObject.IsInstanceValid(proceedButton))
                proceedButton.Disable();
        }
        catch
        {
            // The map vote is the important action; never block travel for UI cleanup.
        }
    }

    private static MapLocation? GetCurrentMapLocation()
    {
        try
        {
            return RunManager.Instance.DebugOnlyGetState()?.MapLocation;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPendingCurrent(PendingAutoAdvance pending)
    {
        return pending.Generation == _pendingGeneration && IsCurrentMapLocation(pending.Location);
    }

    private static bool IsCurrentMapLocation(MapLocation scheduledLocation)
    {
        var currentLocation = GetCurrentMapLocation();
        return currentLocation.HasValue && currentLocation.Value == scheduledLocation;
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
