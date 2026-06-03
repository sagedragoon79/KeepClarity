using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Lifts vanilla's two restrictive bridge placement rules so you can span
    /// dry ravines and high-bank rivers without terraforming.
    ///
    /// Vanilla bridges have three water-flavored gates:
    ///   1. <c>PeformBridgeStartCellValidityChecks</c> (sic — typo in source) —
    ///      the start cell must have at least one neighbor that
    ///      <c>IsCellConsideredWaterForBridgePlacement</c> returns true for.
    ///      Sets <c>Overlap_Water</c> in <c>failedRequiredFlags</c> if not.
    ///   2. <c>PlaceableBridge.TryToSnapToValidPosition</c> — while dragging the
    ///      end cursor, the cursor SNAPS to the first non-water cell encountered
    ///      walking from start toward cursor. Bridges over dry land collapse to
    ///      a one-cell stub because the first non-water cell is the bank itself.
    ///   3. <c>PlacementValidityHelper.UpdateBridgeValidity</c> — final pass
    ///      that requires inner non-water cells to be adjacent to start/end and
    ///      requires the end cell to be water-adjacent. Sets
    ///      <c>Overlap_Water</c> in <c>failedRequiredFlags</c> on failure.
    ///
    /// We bypass each only when <c>BridgeAnywhere</c> is on:
    ///   1+3. Postfix clears the <c>Overlap_Water</c> bit from
    ///        <c>failedRequiredFlags</c> (keeps it in invalidation so you can't
    ///        plop a bridge ON water, which would be nonsense anyway).
    ///   2. Prefix short-circuits to <c>__result = false</c> so the
    ///      <c>for (int i = 0; i &lt; maxSnapIterations; i++)</c> loop in
    ///      <c>OnPositionChanged</c> exits immediately, leaving the end cell at
    ///      the cursor's actual position. The bridge then spans cursor-to-start.
    ///
    /// Bridge height: <c>BridgeContainer.AssignStartAndEndCells</c> uses each
    /// cell's <c>worldCenter.y</c>, which is the terrain mesh elevation at that
    /// cell. Land cells return the terrain Y, so high-bank starts/ends naturally
    /// position the bridge at land height — no extra Y patching needed.
    /// </summary>

    /// <summary>
    /// Patch 1 — Start-cell validity: drop the "must be adjacent to water"
    /// requirement so you can begin a bridge on the rim of a dry ravine.
    /// </summary>
    [HarmonyPatch(typeof(PlaceableBridge), "PeformBridgeStartCellValidityChecks")]
    public static class PatchBridgeStartCellValidity
    {
        public static void Postfix(ref PlacementGridValidityCheckFlags failedRequiredFlags)
        {
            if (FFUIOverhaulMod.BridgeAnywhere == null || !FFUIOverhaulMod.BridgeAnywhere.Value) return;

            // Clear the water-neighbor requirement. Keep failedInvalidationFlags
            // (set when startCell IS water) untouched — starting on water is
            // still nonsense.
            failedRequiredFlags &= ~PlacementGridValidityCheckFlags.Overlap_Water;
        }
    }

    /// <summary>
    /// Patch 2 — Snap loop: skip the "snap end cell to the first non-water
    /// cell from start" pass so the bridge end stays under the cursor. With
    /// the snap suppressed, <c>OnPositionChanged</c>'s loop exits on the
    /// first iteration and the placeable sits at the cursor's actual cell.
    /// </summary>
    [HarmonyPatch(typeof(PlaceableBridge), "TryToSnapToValidPosition")]
    public static class PatchBridgeTryToSnap
    {
        public static bool Prefix(ref bool __result)
        {
            if (FFUIOverhaulMod.BridgeAnywhere == null || !FFUIOverhaulMod.BridgeAnywhere.Value) return true;

            // Returning false from this method tells the caller "no further
            // snap to try" — the for-loop in OnPositionChanged breaks. The
            // end cell remains wherever the cursor put it.
            __result = false;
            return false; // skip the vanilla body
        }
    }

    /// <summary>
    /// Patch 3 — Full validity pass: clear the <c>Overlap_Water</c> required-
    /// flag bit so spans whose inner cells aren't water (and whose end cell
    /// isn't water-adjacent) still validate. Required for the placement to
    /// actually be confirmable on dry land.
    /// </summary>
    [HarmonyPatch(typeof(PlacementValidityHelper), "UpdateBridgeValidity")]
    public static class PatchBridgeUpdateValidity
    {
        public static void Postfix(ref PlacementGridValidityCheckFlags failedRequiredFlags)
        {
            if (FFUIOverhaulMod.BridgeAnywhere == null || !FFUIOverhaulMod.BridgeAnywhere.Value) return;

            failedRequiredFlags &= ~PlacementGridValidityCheckFlags.Overlap_Water;
        }
    }

    /// <summary>
    /// Patch 4 — Hold the arch/pillar/mid-pathway section at bank height.
    ///
    /// Vanilla hard-codes the bridge's mid-section parent to
    /// <c>seaLevel + bridgeHeightAboveWater</c> (line 54586 in the decompile),
    /// so even when start/end cells sit on a high ravine bank the middle of the
    /// bridge sags down to ~water level. The start/end ramps are positioned
    /// separately and look correct, but every arch and pillar in between hangs
    /// at sea+6.
    ///
    /// Postfix override: re-anchor <c>meshObjsParent.position.y</c> to
    /// <c>max(seaLevel + bridgeHeightAboveWater, max(startCell.y, endCell.y))</c>.
    /// For normal river bridges the sea-level term wins and behavior is
    /// identical to vanilla. For high-bank/ravine spans the bank height wins
    /// and the bridge stays at bank level. Also re-anchors startSection/
    /// endSection world Y to their respective bank heights so the ramps don't
    /// drift when the parent moves.
    /// </summary>
    [HarmonyPatch(typeof(BridgeContainer), "AssignStartAndEndCells")]
    public static class PatchBridgeContainerHeight
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo? _meshObjsParent;
        private static FieldInfo? _bridgeHeightAboveWater;
        private static FieldInfo? _startSection;
        private static FieldInfo? _endSection;
        private static bool _resolved;

        private static void ResolveFields()
        {
            if (_resolved) return;
            _resolved = true;
            var t = typeof(BridgeContainer);
            _meshObjsParent         = t.GetField("meshObjsParent", Flags);
            _bridgeHeightAboveWater = t.GetField("bridgeHeightAboveWater", Flags);
            _startSection           = t.GetField("startSection", Flags);
            _endSection             = t.GetField("endSection", Flags);
        }

        public static void Postfix(BridgeContainer __instance)
        {
            if (FFUIOverhaulMod.BridgeAnywhere == null || !FFUIOverhaulMod.BridgeAnywhere.Value) return;

            try
            {
                ResolveFields();
                if (_meshObjsParent == null) return;
                var parent = _meshObjsParent.GetValue(__instance) as Transform;
                if (parent == null) return;

                float clearance = (_bridgeHeightAboveWater != null)
                    ? (float)_bridgeHeightAboveWater.GetValue(__instance) : 6f;

                var gm = UnitySingleton<GameManager>.Instance;
                float seaLevel = (gm != null && gm.terrainManager != null) ? gm.terrainManager.seaLevel : 0f;

                float startY  = __instance.startCell.worldCenter.y;
                float endY    = __instance.endCell.worldCenter.y;
                float bankY   = Mathf.Max(startY, endY);
                float vanilla = seaLevel + clearance;
                float desired = Mathf.Max(vanilla, bankY);

                // Override the mid-section parent height. Cheap conditional so
                // a normal river bridge (bankY < vanilla) is a no-op.
                var pos = parent.position;
                if (!Mathf.Approximately(pos.y, desired))
                {
                    parent.position = new Vector3(pos.x, desired, pos.z);
                }

                // Re-anchor the start/end sections to their actual bank Ys so
                // the ramps don't follow the parent up and float off the cliff.
                if (_startSection != null && _startSection.GetValue(__instance) is Transform ss)
                {
                    var sp = ss.position;
                    ss.position = new Vector3(sp.x, startY, sp.z);
                }
                if (_endSection != null && _endSection.GetValue(__instance) is Transform es)
                {
                    var ep = es.position;
                    es.position = new Vector3(ep.x, endY, ep.z);
                }
            }
            catch (System.Exception ex)
            {
                FFUIOverhaulMod.Log.Warning(
                    $"[BridgeAnywhere] Height postfix failed: {ex.Message}");
            }
        }
    }
}
