using HarmonyLib;
using System.Reflection;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// New Game menu seed-reroller QoL: vanilla's OnRerollClicked forces
    /// `mapType = MapType.Random` before calling RerollMap(), which throws
    /// away the player's selected terrain type every time they hit the dice
    /// button. RerollMap itself respects mapType and pulls the correct
    /// min/max mountain/water ranges from the map type's container — so
    /// simply not nuking mapType is enough.
    ///
    /// Skip the original method via Prefix returning false; call RerollMap +
    /// UpdateMapTypeDisplay manually via reflection (private members on
    /// UIStartMenu_NewSettlement). If reflection ever fails (game patch
    /// rename), Prefix returns true so vanilla still runs as a fallback.
    /// </summary>
    [HarmonyPatch(typeof(UIStartMenu_NewSettlement), "OnRerollClicked")]
    public class PatchKeepMapTypeOnReroll
    {
        private static MethodInfo? _rerollMap;
        private static MethodInfo? _updateMapTypeDisplay;

        public static bool Prefix(UIStartMenu_NewSettlement __instance)
        {
            // User opted out — vanilla force-Random behavior.
            if (FFUIOverhaulMod.KeepMapTypeOnReroll != null && !FFUIOverhaulMod.KeepMapTypeOnReroll.Value)
                return true;

            try
            {
                if (_rerollMap == null)
                    _rerollMap = typeof(UIStartMenu_NewSettlement).GetMethod(
                        "RerollMap", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_updateMapTypeDisplay == null)
                    _updateMapTypeDisplay = typeof(UIStartMenu_NewSettlement).GetMethod(
                        "UpdateMapTypeDisplay", BindingFlags.Instance | BindingFlags.NonPublic);

                if (_rerollMap == null || _updateMapTypeDisplay == null)
                {
                    FFUIOverhaulMod.Log.Warning("[KeepMapType] Couldn't resolve RerollMap or UpdateMapTypeDisplay — falling through to vanilla.");
                    return true;
                }

                _rerollMap.Invoke(__instance, null);
                _updateMapTypeDisplay.Invoke(__instance, null);
                return false; // skip vanilla (which would force MapType.Random)
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[KeepMapType] Prefix failed, allowing vanilla: {e.Message}");
                return true;
            }
        }
    }
}
