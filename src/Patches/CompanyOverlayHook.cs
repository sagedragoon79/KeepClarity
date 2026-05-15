using FFUIOverhaul.UI;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Hook into the bottom-of-screen banner button (UIMilitaryCompanyButton).
    /// Normal left-click keeps vanilla "select all units" behavior; holding
    /// Shift while clicking opens (or closes) Keep Clarity's company roster
    /// overlay for that company.
    ///
    /// We Postfix OnToggleClicked rather than adding a new pointer handler:
    /// it fires on the same input frame as the toggle, gives us
    /// __instance.militaryCompany directly, and inherits vanilla's debounce
    /// for double-clicks. We only act when val==true (the toggle just turned
    /// on) so a Shift-click that DESELECTS doesn't also re-open the panel.
    /// </summary>
    [HarmonyPatch(typeof(UIMilitaryCompanyButton), "OnToggleClicked")]
    public class PatchCompanyBannerShiftClick
    {
        public static void Postfix(UIMilitaryCompanyButton __instance, bool val)
        {
            if (FFUIOverhaulMod.EnableCompanyOverlay == null || !FFUIOverhaulMod.EnableCompanyOverlay.Value) return;
            if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
            if (__instance == null || __instance.militaryCompany == null) return;
            // val==true is the "just selected" transition. Shift-click on an
            // already-selected banner sometimes fires with val==false in the
            // toggle-off path — still useful to toggle our overlay there.
            CompanyOverlayManager.Toggle(__instance.militaryCompany);
        }
    }
}
