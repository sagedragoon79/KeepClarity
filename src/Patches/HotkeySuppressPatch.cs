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
    ///   4. When a forageable is selected — prevents R from also firing FF's bind
    ///
    /// Exception: a small allowlist of "global" shortcuts passes through in
    /// every non-modal context — Pause and F2-F5 — so the player can pause /
    /// camera-save / etc. without dismissing the info window first. Modal
    /// suppression stays absolute (modals need full attention).
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

            // Modals trump the allowlist — Y/N/Esc/Enter must hit the dialog only.
            if (!modal && IsGlobalAllowThrough(keyCombo))
                return true;

            __result = false;
            return false;
        }

        /// <summary>
        /// Pause + F2-F5 are non-conflicting with any of our info-panel
        /// hotkeys (R/U/E/Del/T/P/O/-/=) so they pass through whenever a
        /// non-modal info panel has focus.
        /// </summary>
        private static bool IsGlobalAllowThrough(KeyCombo combo)
        {
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
