using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace AutoPath.Patches;

[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapScreenPatch
{
    private static NMapScreen? _pendingScreen;

    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        if (_pendingScreen == __instance)
            return;

        if (__instance.IsTraveling || !__instance.IsTravelEnabled)
            return;

        var runManager = RunManager.Instance;
        if (runManager != null && !runManager.IsSinglePlayerOrFakeMultiplayer)
            return;

        if (CountTravelable(__instance, out _) != 1)
            return;

        _pendingScreen = __instance;

        var tree = __instance.GetTree();
        if (tree == null)
        {
            _pendingScreen = null;
            return;
        }

        tree.CreateTimer(0.5).Timeout += () =>
        {
            _pendingScreen = null;

            if (!GodotObject.IsInstanceValid(__instance))
                return;
            if (!__instance.IsInsideTree() || !__instance.IsOpen)
                return;
            if (__instance.IsTraveling || !__instance.IsTravelEnabled)
                return;

            // Re-scan: must still be exactly one travelable
            if (CountTravelable(__instance, out var target) != 1 || target == null)
                return;
            if (!GodotObject.IsInstanceValid(target))
                return;

            __instance.OnMapPointSelectedLocally(target);
        };
    }

    private static int CountTravelable(NMapScreen screen, out NMapPoint? single)
    {
        single = null;
        var pointsControl = Traverse.Create(screen).Field("_points").GetValue<Control>();
        if (pointsControl == null)
            return 0;

        int count = 0;
        CollectTravelable(pointsControl, ref count, ref single);
        return count;
    }

    private static void CollectTravelable(Node parent, ref int count, ref NMapPoint? single)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is NMapPoint mapPoint && mapPoint.State == MapPointState.Travelable)
            {
                count++;
                single = mapPoint;
                if (count > 1)
                    return;
            }
            else if (child.GetChildCount() > 0)
            {
                CollectTravelable(child, ref count, ref single);
                if (count > 1)
                    return;
            }
        }
    }
}
