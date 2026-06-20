using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul
{
    /// <summary>
    /// Hotkey support for the Crop Field info window.
    ///
    /// Copy / Paste / Salvage fire through Button.onClick, so we click them by
    /// GameObject name (this also skips the custom EP Planting Almanac buttons and
    /// the AddClay/AddSand buttons). Expand and Clear can't be clicked:
    ///   - ExpandButton's GameObject is inactive when expand isn't the current
    ///     mode, so we call UICropInfoWindow.ExpandClicked() directly.
    ///   - ClearSelectedCrop is wired to a manual rect-hit handler
    ///     (UICropInfoScheduleBar.OnLMBClick), not Button.onClick, so a simulated
    ///     click does nothing; we call UICropInfoWindow.ClearDragItem() directly,
    ///     passing the currently-selected schedule crop (UIHorizontalDragItem.selected).
    /// </summary>
    internal static class CropfieldActions
    {
        // Copy / Paste / Salvage — fire the native button by name.
        public static void TryClickButton(string buttonName)
        {
            try
            {
                var win = FindActiveWindow();
                if (win == null) return;

                foreach (var s in win.GetComponentsInChildren<Selectable>(includeInactive: true))
                {
                    if (s == null || s.gameObject.name != buttonName) continue;
                    if (!s.gameObject.activeInHierarchy || !s.interactable)
                    {
                        FFUIOverhaulMod.Log.Msg($"[Cropfield] '{buttonName}' not available");
                        return;
                    }
                    var ped = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(s.gameObject, ped, ExecuteEvents.pointerClickHandler);
                    return;
                }
                FFUIOverhaulMod.Log.Msg($"[Cropfield] button '{buttonName}' not found");
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[Cropfield] {buttonName} failed: {e.Message}"); }
        }

        // Expand — direct call (the ExpandButton GameObject is inactive). This
        // mirrors the button: closes the window and begins resize-placement mode.
        public static void TryExpand()
        {
            try
            {
                var w = FindCropWindow();
                if (w == null) { FFUIOverhaulMod.Log.Msg("[Cropfield] no crop window for Expand"); return; }
                w.ExpandClicked();
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[Cropfield] Expand failed: {e.Message}"); }
        }

        // Clear — direct call with the currently-selected schedule crop.
        public static void TryClearSelectedCrop()
        {
            try
            {
                var w = FindCropWindow();
                if (w == null) { FFUIOverhaulMod.Log.Msg("[Cropfield] no crop window for Clear"); return; }
                var sel = UIHorizontalDragItem.selected;
                if (sel == null)
                {
                    FFUIOverhaulMod.Log.Msg("[Cropfield] Clear: no crop selected — click a crop in the schedule first");
                    return;
                }
                w.ClearDragItem(sel);
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[Cropfield] Clear failed: {e.Message}"); }
        }

        // Resolve fresh per press (keypress only) and pick the active instance.
        private static UICropInfoWindowGameLayer? FindActiveWindow()
        {
            foreach (var w in UnityEngine.Object.FindObjectsOfType<UICropInfoWindowGameLayer>(includeInactive: true))
                if (w != null && w.gameObject.activeInHierarchy) return w;
            return null;
        }

        private static UICropInfoWindow? FindCropWindow()
        {
            foreach (var w in UnityEngine.Object.FindObjectsOfType<UICropInfoWindow>(includeInactive: true))
                if (w != null && w.gameObject.activeInHierarchy) return w;
            return null;
        }
    }
}
