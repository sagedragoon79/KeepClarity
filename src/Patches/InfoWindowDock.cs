using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Keeps the native building INFO window from hiding behind the native BUILD menu.
    /// While the build menu (UIBuildingWindow, docked right) is open and the info window
    /// (UISelectedObjectInfoWindow) overlaps it, slide the info window LEFT by exactly the
    /// overlap so its whole frame clears the menu; restore it when the menu closes. The
    /// build menu is never touched — we only read its open-state and rect.
    ///
    /// Driven entirely from the per-frame Tick (OnUpdate). No Harmony patches: the menu's
    /// open-state IS the precise trigger, so a frame-rate reconciler covers open /
    /// building-reselect / close without patching base UIWindow methods that fire for every
    /// window. Recomputed from live rects each frame → idempotent and self-healing (handles
    /// re-targets and dynamic menu re-layout). Geometry goes through the window's own
    /// mainPivot and its SetPosition(...) move API (which screen-clamps for us).
    /// </summary>
    internal static class InfoWindowDock
    {
        private const float Gap = 6f;   // px of breathing room left of the menu

        private static bool _moved;
        private static Vector3 _originalPivotPos;
        private static Vector3 _lastAppliedPos;
        private static bool _prevMenuOpen;
        private static UIBuildingWindow? _menuCache;

        // mainPivot is protected on UIWindow — read it once, reflectively, and cache.
        private static readonly FieldInfo? PivotField =
            typeof(UIWindow).GetField("mainPivot", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly Vector3[] _corners = new Vector3[4];

        public static void ResetState()
        {
            _moved = false;
            _prevMenuOpen = false;
            _menuCache = null;
        }

        public static void Tick()
        {
            var on = FFUIOverhaulMod.EnableInfoWindowDock;
            if (on == null || !on.Value)
            {
                if (_moved) Restore();
                _prevMenuOpen = false;
                return;
            }

            var menu = GetBuildMenu();
            bool open = menu != null && menu.isOpen;
            if (open) Reconcile(menu!);
            else if (_prevMenuOpen) Restore();   // menu just closed
            _prevMenuOpen = open;
        }

        // Slide the info window left so its right edge clears the menu's left edge. The target
        // is computed from the menu's fixed left edge and the window's constant width, NOT from
        // the window's current (already-shifted) position — so re-running every frame lands on
        // the same spot instead of oscillating (shift → "no overlap" → restore → overlap → …).
        // Only restores when there's no overlap even at the window's HOME position.
        private static void Reconcile(UIBuildingWindow menu)
        {
            var info = FFUIOverhaulMod.GetBuildingInfoWindow();
            if (info == null || !info.isOpen) { Restore(); return; }

            var menuRt = menu.transform as RectTransform;
            var pivot = Pivot(info);
            if (menuRt == null || pivot == null) return;

            float menuLeft = ScreenRect(menuRt).xMin;
            Rect infoRect = ScreenRect(pivot);
            float rightOffset = infoRect.xMax - pivot.position.x;   // pivot→right-edge (rigid window, frame-stable)
            float targetPivotX = (menuLeft - Gap) - rightOffset;    // pivot X that lands the right edge at menuLeft-Gap

            // "Home" = where FF placed the window (its unshifted pivot): the captured original
            // once we've moved it, otherwise the current (unshifted) position.
            Vector3 homePos = _moved ? _originalPivotPos : pivot.position;

            if (targetPivotX >= homePos.x)
            {
                Restore();   // no overlap even at home → make sure we're home
                return;
            }

            if (!_moved) { _originalPivotPos = pivot.position; _moved = true; }
            info.SetPosition(new Vector2(targetPivotX, homePos.y));   // FF clamps to screen-left if needed
            _lastAppliedPos = pivot.position;                         // re-read clamp result for the Restore guard
        }

        // Put the info window back, but only if we still own its position (don't fight a
        // user/game move).
        private static void Restore()
        {
            if (!_moved) return;
            var info = FFUIOverhaulMod.GetBuildingInfoWindow();
            if (info != null && info.isOpen)
            {
                var pivot = Pivot(info);
                if (pivot != null && (pivot.position - _lastAppliedPos).sqrMagnitude < 1f)
                    info.SetPosition(_originalPivotPos);
            }
            _moved = false;
        }

        private static UIBuildingWindow? GetBuildMenu()
        {
            if (_menuCache == null)
            {
                var found = Object.FindObjectsOfType<UIBuildingWindow>(true);
                if (found.Length > 0) _menuCache = found[0];
            }
            return _menuCache;
        }

        private static RectTransform? Pivot(UIWindow w)
            => (PivotField?.GetValue(w) as RectTransform) ?? (w.transform as RectTransform);

        // FF's UI canvas is Screen-Space-Overlay, so world corners are screen pixels — the
        // overlap math and the SetPosition target all stay in one consistent space.
        private static Rect ScreenRect(RectTransform rt)
        {
            rt.GetWorldCorners(_corners);   // [0]=bottom-left, [2]=top-right
            return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
        }
    }
}
