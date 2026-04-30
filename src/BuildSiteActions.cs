using FFUIOverhaul.Utils;
using UnityEngine.UI;

namespace FFUIOverhaul
{
    public static class BuildSiteActions
    {
        public static void TryCancel(BuildSiteResource buildSite, GameManager gm)
        {
            try
            {
                buildSite.Salvage(showConfirmation: true);
                FFUIOverhaulMod.Log.Msg("[Action] Build site cancel (Salvage with confirmation) triggered");
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Build site cancel failed: {e.Message}");
            }
        }

        public static void TryTogglePriority(BuildSiteResource buildSite, GameManager gm)
            => FlipBuildSiteToggle(buildSite, "prioritizedToggle", "Prioritize",
                () => buildSite.buildSite.prioritizeConstruction,
                v => buildSite.buildSite.prioritizeConstruction = v);

        public static void TryToggleConstructionEnabled(BuildSiteResource buildSite, GameManager gm)
            => FlipBuildSiteToggle(buildSite, "constructionEnabledToggle", "Construction Enabled",
                () => buildSite.buildSite.constructionEnabled,
                v => buildSite.buildSite.constructionEnabled = v);

        public static void TryIncrementBuilders(BuildSiteResource buildSite, GameManager gm)
        {
            try
            {
                var infoWindow = FFUIOverhaulMod.GetBuildSiteWindow();
                if (infoWindow != null)
                {
                    // Public method on UIBuildsiteWindow_New. It guards against
                    // exceeding maxBuilders and raises UserDefinedBuildersChangedEvent
                    // for UI refresh — same path the + button takes.
                    infoWindow.IncrementBuilders();
                    FFUIOverhaulMod.Log.Msg($"[Action] Increment builders → {buildSite.userDefinedBuilders}/{buildSite.maxBuilders}");
                    return;
                }
                FFUIOverhaulMod.Log.Warning("Increment builders: no info window cached");
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Increment builders failed: {e.Message}");
            }
        }

        public static void TryDecrementBuilders(BuildSiteResource buildSite, GameManager gm)
        {
            try
            {
                var infoWindow = FFUIOverhaulMod.GetBuildSiteWindow();
                if (infoWindow != null)
                {
                    infoWindow.DecrementBuilders();
                    FFUIOverhaulMod.Log.Msg($"[Action] Decrement builders → {buildSite.userDefinedBuilders}/{buildSite.maxBuilders}");
                    return;
                }
                FFUIOverhaulMod.Log.Warning("Decrement builders: no info window cached");
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Decrement builders failed: {e.Message}");
            }
        }

        /// <summary>
        /// Flip a private Toggle on the build site info window. Setting isOn fires
        /// onValueChanged, which routes to the InfoWindow's matching Toggle*(value)
        /// method — same path the real checkbox click takes, so visuals stay in sync.
        /// Falls back to flipping the underlying buildSite flag if the InfoWindow or
        /// Toggle isn't reachable (e.g. the constructionEnabled toggle is hidden out
        /// of season — its GameObject is inactive but the field is still set).
        /// </summary>
        private static void FlipBuildSiteToggle(
            BuildSiteResource buildSite,
            string toggleFieldName,
            string label,
            System.Func<bool> getCurrent,
            System.Action<bool> setCurrent)
        {
            try
            {
                var infoWindow = FFUIOverhaulMod.GetBuildSiteWindow();
                if (infoWindow != null)
                {
                    var toggle = ReflectionHelper.GetPrivateField(toggleFieldName, infoWindow) as Toggle;
                    if (toggle != null && toggle.gameObject.activeInHierarchy && toggle.interactable)
                    {
                        toggle.isOn = !toggle.isOn;
                        FFUIOverhaulMod.Log.Msg($"[Action] {label} toggle flipped — isOn={toggle.isOn}");
                        return;
                    }
                    if (toggle != null && !toggle.gameObject.activeInHierarchy)
                    {
                        FFUIOverhaulMod.Log.Msg($"[Toggle] {label} toggle is hidden (likely out of season)");
                        return;
                    }
                    if (toggle != null && !toggle.interactable)
                    {
                        FFUIOverhaulMod.Log.Msg($"[Toggle] {label} toggle is not interactable");
                        return;
                    }
                }

                bool current = getCurrent();
                setCurrent(!current);
                FFUIOverhaulMod.Log.Msg($"[Action] {label} flipped to {!current} (no info window)");
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"Toggle {label} failed: {e.Message}");
            }
        }
    }
}
