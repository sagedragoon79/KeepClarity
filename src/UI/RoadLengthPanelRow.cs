using System;
using TMPro;
using UnityEngine;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Adds a "Length: N tiles" row to FF's own placement panel (PlaceableHUDUI)
    /// while a road is being laid.
    ///
    /// WHY IN THIS PANEL: players already read this box for grid dimensions when
    /// placing crop fields and graveyards — it's how you measure distance in FF.
    /// Putting road length there means it lands where people are already looking,
    /// instead of teaching a new place to look.
    ///
    /// PANEL SHAPE (from a UI dump taken during road placement):
    ///   PlaceableHUDUI(Clone)/VerticalLayout
    ///     ├─ Title Glow/Building Title   "Dirt Road"
    ///     ├─ DIVIDER
    ///     ├─ Help Text                   "Click to finish road…"
    ///     └─ Cost/HorizontalLayout       "Cost:" + item icon/count
    /// VerticalLayout is a layout group, so a row appended to it positions itself
    /// — we add ours last, i.e. along the bottom, and never touch vanilla rows.
    ///
    /// LIFECYCLE: the panel is instantiated per placement ("(Clone)") and calls
    /// Init(Placeable) each time, so the row is rebuilt per placement and the
    /// cached label is dropped on OnDestroy. No pooling assumptions.
    /// </summary>
    internal static class RoadLengthPanelRow
    {
        private const string RowName = "FFUI_RoadLengthRow";

        private static TextMeshProUGUI? _label;
        private static GameObject? _row;
        private static bool _failed;

        /// <summary>True once a row exists for the live panel.</summary>
        public static bool IsAttached => _label != null;

        /// <summary>Build (or re-find) our row inside a freshly-initialized panel.
        /// Call only for road placements.</summary>
        public static void Attach(Component panel)
        {
            if (_failed || panel == null) return;
            try
            {
                var vertical = panel.transform.Find("VerticalLayout");
                if (vertical == null) return;

                // Re-use if this panel instance already has our row.
                var existing = vertical.Find(RowName);
                if (existing != null)
                {
                    _row = existing.gameObject;
                    _label = existing.GetComponent<TextMeshProUGUI>();
                    return;
                }

                // Copy styling from the panel's own Help Text so the row reads as
                // native — same font asset, size and colour as FF's own copy.
                TextMeshProUGUI? style = null;
                var help = vertical.Find("Help Text");
                if (help != null) style = help.GetComponent<TextMeshProUGUI>();

                var go = new GameObject(RowName, typeof(RectTransform));
                go.transform.SetParent(vertical, worldPositionStays: false);
                go.transform.SetAsLastSibling();

                var t = go.AddComponent<TextMeshProUGUI>();
                t.alignment = TextAlignmentOptions.Center;
                t.raycastTarget = false;
                t.enableWordWrapping = false;
                if (style != null)
                {
                    t.font = style.font;
                    t.fontSize = style.fontSize;
                    t.fontStyle = style.fontStyle;
                    t.color = style.color;
                    t.fontSharedMaterial = style.fontSharedMaterial;
                }
                else
                {
                    t.fontSize = 16f;
                    t.color = Color.white;
                }

                _row = go;
                _label = t;
                _label.text = "";
            }
            catch (Exception e)
            {
                _failed = true;
                FFUIOverhaulMod.Log.Warning("[RoadLength] panel row failed, using cursor label only: " + e.Message);
            }
        }

        /// <summary>Update the row's text. No-op if we never attached.</summary>
        public static void SetCount(int tiles)
        {
            if (_label == null) return;
            try
            {
                _label.text = Localization.KcLoc.Tr("KeepClarity/road/length", "Length")
                    + ": " + tiles + " " + Localization.KcLoc.Tr("KeepClarity/road/tiles", "tiles");
                if (_row != null && !_row.activeSelf) _row.SetActive(true);
            }
            catch { /* panel torn down mid-frame */ }
        }

        /// <summary>Forget the row (its panel is going away).</summary>
        public static void Detach()
        {
            _label = null;
            _row = null;
        }
    }
}
