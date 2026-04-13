using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

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

public static class AutoAdvanceScheduler
{
    private static NMapScreen? _pendingScreen;
    private static bool _isPeeking;
    private static readonly Random Rng = new();

    public static void SetPeeking(bool peeking)
    {
        _isPeeking = peeking;
    }

    public static void TrySchedule(NMapScreen screen)
    {
        if (_pendingScreen == screen)
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

        _pendingScreen = screen;
        GD.Print($"[AutoPath] Scheduling auto-advance ({AutoPathConfig.SelectionDelay}s, {travelable.Count} node(s))");

        var tree = screen.GetTree();
        if (tree == null)
        {
            _pendingScreen = null;
            return;
        }

        tree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
            OnTimerFired(screen);
    }

    private static void OnTimerFired(NMapScreen screen)
    {
        _pendingScreen = null;

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
            _pendingScreen = screen;
            var retryTree = screen.GetTree();
            if (retryTree != null)
                retryTree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
                    OnTimerFired(screen);
            return;
        }

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

        GD.Print("[AutoPath] Auto-advancing to next node");
        screen.OnMapPointSelectedLocally(target);
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
