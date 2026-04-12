using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace AutoPath.Patches;

[HarmonyPatch(typeof(NMapScreen), "RecalculateTravelability")]
public static class MapScreenPatch
{
    private static bool _autoTravelPending;

    [HarmonyPostfix]
    public static void Postfix(NMapScreen __instance)
    {
        if (_autoTravelPending)
            return;

        if (__instance.IsTraveling || !__instance.IsTravelEnabled)
            return;

        // Skip auto-travel in multiplayer to avoid conflicts with vote system
        var runManager = RunManager.Instance;
        if (runManager != null && !runManager.IsSinglePlayerOrFakeMultiplayer)
            return;

        // Find the _points container and count travelable map points
        var pointsField = Traverse.Create(__instance).Field("_points");
        var pointsControl = pointsField.GetValue<Control>();
        if (pointsControl == null)
            return;

        NMapPoint? singleTravelable = null;
        int travelableCount = 0;

        foreach (var child in pointsControl.GetChildren())
        {
            if (child is NMapPoint mapPoint && mapPoint.State == MapPointState.Travelable)
            {
                travelableCount++;
                singleTravelable = mapPoint;
                if (travelableCount > 1)
                    break;
            }
        }

        if (travelableCount != 1 || singleTravelable == null)
            return;

        _autoTravelPending = true;
        var target = singleTravelable;

        var tree = __instance.GetTree();
        if (tree == null)
        {
            _autoTravelPending = false;
            return;
        }

        tree.CreateTimer(0.5).Timeout += () =>
        {
            _autoTravelPending = false;

            if (!__instance.IsInsideTree() || !__instance.IsOpen)
                return;
            if (__instance.IsTraveling || !__instance.IsTravelEnabled)
                return;

            // Verify the target is still travelable
            if (target.State != MapPointState.Travelable)
                return;

            __instance.OnMapPointSelectedLocally(target);
        };
    }
}
