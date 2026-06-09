using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using FFUIOverhaul.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Injects a per-tile count + camera-cycle widget into the buildings menu.
    ///
    /// We attach on BOTH UIBuildingWindow.Awake (initial layout) AND ShowInternal
    /// (every open). The second hook matters because DLC and mod buildings are
    /// added dynamically via FFModAddBuilding AFTER Awake — so an Awake-only pass
    /// misses the pet-DLC kennels (and any modded buildings). Re-running on show
    /// is idempotent: buttons that already carry the widget are skipped.
    /// </summary>
    static class BuildingsWindowAttach
    {
        // Vertical pixels needed for our counter widget (16 tall + a small gap).
        private const float MinGridYSpacing = 22f;

        public static void AttachAll(UIBuildingWindow window)
        {
            try
            {
                var buttons = window.GetComponentsInChildren<UIToolbarBuildingButton>(includeInactive: true);
                if (buttons == null) return;

                // Bump vertical spacing on every grid so each row has breathing
                // room for the counter widget above its tiles.
                var allGrids = window.GetComponentsInChildren<GridLayoutGroup>(includeInactive: true);
                foreach (var grid in allGrids)
                {
                    if (grid == null) continue;
                    if (grid.spacing.y < MinGridYSpacing)
                        grid.spacing = new Vector2(grid.spacing.x, MinGridYSpacing);
                }

                int failed = 0;
                foreach (var btn in buttons)
                {
                    if (btn == null) continue;
                    if (btn.buildingPrefab == null) continue;
                    if (btn.transform.Find("FFUI_BuildingCounter") != null) continue; // already done

                    try { BuildingCounterWidget.Attach(btn); }
                    catch (System.Exception inner)
                    {
                        failed++;
                        if (failed <= 3)
                            FFUIOverhaulMod.Log.Warning(
                                $"[BuildingsWindow] Attach failed on '{btn.name}' (prefab={btn.buildingPrefab.name}): {inner.GetType().Name}: {inner.Message}");
                    }
                }
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[BuildingsWindow] Outer failure: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(UIBuildingWindow), "Awake")]
    static class BuildingsWindowAwakePatch
    {
        static void Postfix(UIBuildingWindow __instance) => BuildingsWindowAttach.AttachAll(__instance);
    }

    // Catches DLC / mod buildings added after Awake (FFModAddBuilding) — e.g. the
    // pet-DLC Dog/Cat Kennels — by re-running the (idempotent) attach pass on open.
    [HarmonyPatch(typeof(UIBuildingWindow), "ShowInternal")]
    static class BuildingsWindowShowPatch
    {
        static void Postfix(UIBuildingWindow __instance) => BuildingsWindowAttach.AttachAll(__instance);
    }
}
