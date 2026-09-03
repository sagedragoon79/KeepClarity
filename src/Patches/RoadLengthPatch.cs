using HarmonyLib;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Feeds <see cref="UI.RoadLengthCounter"/> while a road is being dragged.
    ///
    /// HOOK: SplineRoadContainer.SetControlPoints — public, and called every time
    /// the road preview's curve changes (it recomputes the covered cell set right
    /// after, via SetupCells). That makes it the exact moment the tile count can
    /// change, so we don't need a per-frame poll of our own.
    ///
    /// The isBeingPlaced guard matters: SetControlPoints also runs for roads that
    /// already exist (load, rebuild), and without it the counter would flash on
    /// screen during those.
    /// </summary>
    [HarmonyPatch(typeof(SplineRoadContainer), "SetControlPoints")]
    internal static class RoadLengthPatch
    {
        private static bool _loggedError;

        private static void Postfix(SplineRoadContainer __instance)
        {
            try
            {
                if (!FFUIOverhaulMod.EnableRoadLengthCounter.Value) return;
                if (__instance == null || !__instance.isBeingPlaced) return;

                var cells = __instance.cells;
                if (cells == null) return;
                UI.RoadLengthCounter.Report(cells.Count);
                UI.RoadLengthPanelRow.SetCount(cells.Count);
            }
            catch (System.Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    FFUIOverhaulMod.Log.Warning("[RoadLength] postfix: " + e.Message);
                }
            }
        }
    }

    /// <summary>
    /// Per-frame heartbeat while road placement is active.
    ///
    /// WHY: SetControlPoints only fires when the curve CHANGES, so holding the
    /// mouse still starved the counter and it expired mid-drag (reported in
    /// testing as "shows for a brief second then goes away"). SplineRoadBuilder
    /// .UpdateSegmentPlacer runs every frame while the road tool is up, and its
    /// isPlacing property is public — so this keeps the readout alive for the
    /// whole drag and lets it disappear the moment placement ends, without
    /// guessing from report freshness.
    /// </summary>
    [HarmonyPatch(typeof(SplineRoadBuilder), "UpdateSegmentPlacer")]
    internal static class RoadPlacementHeartbeatPatch
    {
        private static void Postfix(SplineRoadBuilder __instance)
        {
            try
            {
                if (!FFUIOverhaulMod.EnableRoadLengthCounter.Value) { UI.RoadLengthCounter.Hide(); return; }
                if (__instance != null && __instance.isPlacing) UI.RoadLengthCounter.KeepAlive();
                else UI.RoadLengthCounter.Hide();
            }
            catch { /* per-frame path — stay silent */ }
        }
    }

    /// <summary>
    /// Injects the length row into FF's placement panel when a ROAD placement
    /// starts, and drops the reference when the panel is destroyed.
    ///
    /// PlaceableHUDUI.Init runs for every placeable, so the PlaceableSplineRoad
    /// type check is what keeps the row off buildings, fields and everything else.
    /// </summary>
    [HarmonyPatch(typeof(PlaceableHUDUI), "Init")]
    internal static class RoadLengthPanelAttachPatch
    {
        private static void Postfix(PlaceableHUDUI __instance, Placeable _placeable)
        {
            try
            {
                if (!FFUIOverhaulMod.EnableRoadLengthCounter.Value) return;
                if (_placeable is PlaceableSplineRoad) UI.RoadLengthPanelRow.Attach(__instance);
                else UI.RoadLengthPanelRow.Detach();
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(PlaceableHUDUI), "OnDestroy")]
    internal static class RoadLengthPanelDetachPatch
    {
        private static void Postfix() { try { UI.RoadLengthPanelRow.Detach(); } catch { } }
    }
}
