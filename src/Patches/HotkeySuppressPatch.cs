using HarmonyLib;
using HotkeyManager;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Suppresses the game's HotkeyManager when our mod owns the input context:
    ///   1. When a modal (Y/N confirmation) is visible — prevents Y/N bleeding to achievements/roads
    ///   2. When the building info window is active — prevents R/U/E/Del from also firing game bindings
    ///   3. When the build/deconstruction site info window is active — prevents Del/P bleeding
    ///
    /// Exception (building window only): a small allowlist of "global" shortcuts
    /// passes through — Pause and F2-F5 — so the player can pause / save-camera /
    /// etc. without having to close the building first. Modal and build-site
    /// suppression stays absolute (modals need full attention; -/= are our
    /// builder controls and would conflict with vanilla speed up/down).
    /// </summary>
    [HarmonyPatch(typeof(global::HotkeyManager.HotkeyManager), "GetKeyComboDown")]
    static class HotkeySuppressPatch
    {
        static bool Prefix(KeyCombo keyCombo, ref bool __result)
        {
            bool modal = FFUIOverhaulMod.FrameModalVisible;
            bool building = FFUIOverhaulMod.FrameBuildingWindowActive;
            bool buildSite = FFUIOverhaulMod.FrameBuildSiteWindowActive;
            bool forageable = FFUIOverhaulMod.FrameForageableActive;

            if (!modal && !building && !buildSite && !forageable) return true; // no suppression context

            // Allowlist applies only when ONLY the building info window is active.
            if (building && !modal && !buildSite && !forageable && IsAllowThroughDuringBuilding(keyCombo))
                return true;

            __result = false;
            return false;
        }

        private static bool IsAllowThroughDuringBuilding(KeyCombo combo)
        {
            // F2-F5: always allowed, regardless of binding (they're function keys
            // typically used for camera saves and the player wants them globally).
            switch (combo.key)
            {
                case KeyCode.F2:
                case KeyCode.F3:
                case KeyCode.F4:
                case KeyCode.F5:
                    return true;
            }

            // Pause game — match whatever the player has bound it to.
            var hkm = global::HotkeyManager.HotkeyManager.Instance;
            if (hkm == null) return false;
            var p = hkm.hotkeys.pauseGame;
            return combo.key == p.key
                && combo.modifier == p.modifier
                && combo.modifier2 == p.modifier2;
        }
    }
}
