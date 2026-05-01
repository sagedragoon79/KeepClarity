using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using FFUIOverhaul.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Injects a per-tile building-count widget into the buildings menu.
    ///
    /// UIBuildingWindow.Awake() runs once when the window first activates. By the
    /// time the postfix fires, the toolbar buttons exist as children (some on
    /// inactive tabs) and have their `buildingPrefab` references set, so we can
    /// walk the tree and attach to all of them in one pass.
    ///
    /// We also bump the parent GridLayoutGroup's vertical spacing on each tier
    /// grid so the counter row has somewhere to live without overlapping the
    /// row above.
    /// </summary>
    [HarmonyPatch(typeof(UIBuildingWindow), "Awake")]
    static class BuildingsWindowPatch
    {
        // Vertical pixels needed for our counter widget (16 tall + a small gap).
        private const float MinGridYSpacing = 22f;

        static void Postfix(UIBuildingWindow __instance)
        {
            try
            {
                var buttons = __instance.GetComponentsInChildren<UIToolbarBuildingButton>(includeInactive: true);
                if (buttons == null) return;

                // Bump vertical spacing on every grid in the window so each row has
                // breathing room for the counter widget above its tiles. We scan
                // directly because GetComponentInParent on a button doesn't see
                // inactive ancestor tabs (only one tab is active at a time).
                int gridsExpanded = 0;
                var allGrids = __instance.GetComponentsInChildren<GridLayoutGroup>(includeInactive: true);
                foreach (var grid in allGrids)
                {
                    if (grid == null) continue;
                    if (grid.spacing.y < MinGridYSpacing)
                    {
                        grid.spacing = new Vector2(grid.spacing.x, MinGridYSpacing);
                        gridsExpanded++;
                    }
                }

                int attached = 0;
                int skippedNull = 0, skippedNoPrefab = 0, skippedExisting = 0;
                int failed = 0;

                foreach (var btn in buttons)
                {
                    if (btn == null) { skippedNull++; continue; }
                    if (btn.buildingPrefab == null) { skippedNoPrefab++; continue; }

                    if (btn.transform.Find("FFUI_BuildingCounter") != null) { skippedExisting++; continue; }

                    try
                    {
                        BuildingCounterWidget.Attach(btn);
                        attached++;
                    }
                    catch (System.Exception inner)
                    {
                        failed++;
                        if (failed <= 3)
                        {
                            FFUIOverhaulMod.Log.Warning(
                                $"[BuildingsWindow] Attach failed on '{btn.name}' (prefab={btn.buildingPrefab.name}): {inner.GetType().Name}: {inner.Message}\n{inner.StackTrace}");
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[BuildingsWindow] Outer failure: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
        }
    }
}
