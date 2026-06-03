using HarmonyLib;

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
}
