using HarmonyLib;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Hides FF's unit-selection marquee while capture mode is armed.
    ///
    /// Capture uses left-click-drag, which is also how the game drag-selects
    /// units — so both boxes rendered at once (reported in testing: a green
    /// screen-space rectangle inside our world-space one). They also disagree
    /// visually by nature: the game's box is screen-aligned, ours is a true
    /// rectangle on the ground, so at any camera pitch they can't line up and
    /// the pair reads as a bug.
    ///
    /// Suppressing Activate is enough to keep it off screen; it is a plain
    /// public method with no side effects we depend on.
    /// </summary>
    [HarmonyPatch(typeof(UISelectionBox), "Activate")]
    internal static class SelectionBoxSuppressPatch
    {
        private static bool Prefix()
        {
            // false = skip the original, i.e. don't show the game's box.
            // Also suppressed over our IMGUI panel: dragging the panel otherwise
            // starts a selection box, because IMGUI doesn't register as UI with
            // the game's pointerIsOverUI.
            if (BlueprintCapture.IsArmed) return false;
            if (BlueprintPanel.PointerOverPanel) return false;
            return true;
        }
    }
}
