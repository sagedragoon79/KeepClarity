namespace FFUIOverhaul
{
    public static class BuildingActions
    {
        public static void TryUpgrade(Building building, GameManager gm)
        {
            try
            {
                if (building.buildingUpgradeInfo == null)
                {
                    FFUIOverhaulMod.Log.Msg("[Upgrade] Building has no upgrade path");
                    return;
                }
                if (building.isOnFire)
                {
                    FFUIOverhaulMod.Log.Msg("[Upgrade] Building is on fire — cannot upgrade");
                    return;
                }
                if (building.isRelocating)
                {
                    FFUIOverhaulMod.Log.Msg("[Upgrade] Building is relocating — cannot upgrade");
                    return;
                }
                if (!building.buildingUpgradeInfo.canUpgradeNow)
                {
                    FFUIOverhaulMod.Log.Msg("[Upgrade] Upgrade not available right now (resources, prereqs, or already upgrading)");
                    return;
                }

                building.UpgradeBuilding();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Upgrade failed: {e.Message}");
            }
        }

        public static void TryRelocate(Building building, GameManager gm)
        {
            try
            {
                building.Relocate();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Relocate failed: {e.Message}");
            }
        }

        public static void TryToggleEmployment(Building building, GameManager gm)
        {
            try
            {
                // Flip the InfoWindow's productionToggle directly. Setting isOn fires
                // onValueChanged → ToggleProduction → SetWorkEnabled + UpdateRecurringCosts,
                // which refreshes the X visual and the rest of the panel.
                var infoWindow = FFUIOverhaulMod.GetBuildingInfoWindow() as UIBuildingInfoWindow_New;
                if (infoWindow != null && infoWindow.productionToggle != null)
                {
                    if (!infoWindow.productionToggle.interactable)
                    {
                        FFUIOverhaulMod.Log.Msg("[Toggle] Production toggle is not interactable for this building");
                        return;
                    }
                    infoWindow.productionToggle.isOn = !infoWindow.productionToggle.isOn;
                    return;
                }

                // Fallback — no info window or no toggle (e.g., residence). Just flip
                // the underlying state; the UI panel may not exist for this building.
                bool current = building.playerDesiresWorkEnabled;
                building.SetWorkEnabled(!current, isPlayerDecision: true);
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Toggle employment failed: {e.Message}");
            }
        }

        public static void TryDemolish(Building building, GameManager gm)
        {
            try
            {
                building.Salvage();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Demolish failed: {e.Message}");
            }
        }

        /// <summary>
        /// Fire the InfoWindow's "move work area" (retarget) button — same path as
        /// a real click, so the game enters work-area-retarget mode. The button is
        /// only shown for buildings that have a work area (hunters, foresters,
        /// herbalists, etc.), so respect its active state.
        /// </summary>
        public static void TryMoveWorkArea(Building building, GameManager gm)
        {
            try
            {
                var infoWindow = FFUIOverhaulMod.GetBuildingInfoWindow() as UIBuildingInfoWindow_New;
                if (infoWindow == null) return;

                var btn = infoWindow.retargetButton;
                if (btn == null) return;
                if (!btn.gameObject.activeInHierarchy)
                {
                    FFUIOverhaulMod.Log.Msg("[WorkArea] Retarget button hidden — this building has no movable work area");
                    return;
                }
                btn.onClick.Invoke();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Move work area failed: {e.Message}");
            }
        }

        public static void TryCycleLeft(Building building, GameManager gm)
            => InvokeCycleButton(left: true);

        public static void TryCycleRight(Building building, GameManager gm)
            => InvokeCycleButton(left: false);

        /// <summary>
        /// Invoke the InfoWindow's cycle button onClick — same path as a real click,
        /// so the game's CycleLeft/CycleRight handler runs and the panel rebinds to
        /// the next building in `allBuildingsOfSelectedType`.
        ///
        /// The button's GameObject is hidden (SetActive(false)) when the selected
        /// building is the only one of its type, so respect that — no point firing.
        /// </summary>
        private static void InvokeCycleButton(bool left)
        {
            try
            {
                var infoWindow = FFUIOverhaulMod.GetBuildingInfoWindow() as UIBuildingInfoWindow_New;
                if (infoWindow == null) return;

                var btn = left ? infoWindow.cycleBuildingsLeft : infoWindow.cycleBuildingsRight;
                if (btn == null) return;
                if (!btn.gameObject.activeInHierarchy)
                {
                    FFUIOverhaulMod.Log.Msg($"[Cycle] {(left ? "Left" : "Right")} button hidden — only one building of this type");
                    return;
                }
                btn.onClick.Invoke();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Cycle building failed: {e.Message}");
            }
        }
    }
}
