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
    private static NMapScreen? _pendingScreen;
    private static readonly Random Rng = new();

    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        if (_pendingScreen == __instance)
            return;

        if (__instance.IsTraveling || !__instance.IsTravelEnabled)
            return;

        var travelable = CollectTravelable(__instance);
        if (travelable.Count == 0)
            return;

        // Normal mode: only auto-advance when exactly one choice
        // YOLO mode: auto-advance regardless, picking randomly
        if (travelable.Count > 1 && !AutoPathConfig.YoloMode)
            return;

        _pendingScreen = __instance;

        var tree = __instance.GetTree();
        if (tree == null)
        {
            _pendingScreen = null;
            return;
        }

        tree.CreateTimer(AutoPathConfig.SelectionDelay).Timeout += () =>
        {
            _pendingScreen = null;

            if (!GodotObject.IsInstanceValid(__instance))
                return;
            if (!__instance.IsInsideTree() || !__instance.IsOpen)
                return;
            if (__instance.IsTraveling || !__instance.IsTravelEnabled)
                return;

            var fresh = CollectTravelable(__instance);
            if (fresh.Count == 0)
                return;
            if (fresh.Count > 1 && !AutoPathConfig.YoloMode)
                return;

            var target = fresh.Count == 1
                ? fresh[0]
                : fresh[Rng.Next(fresh.Count)];

            if (!GodotObject.IsInstanceValid(target))
                return;

            __instance.OnMapPointSelectedLocally(target);
        };
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
