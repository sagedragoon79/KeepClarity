using UnityEngine;

namespace FFUIOverhaul.UI
{
    /// <summary>Which way an overlay's list grows from its header.</summary>
    public enum OverlayGrowDirection
    {
        Down, // header on top, items below (default)
        Up,   // header on bottom, items grow upward
    }

    /// <summary>
    /// Shared grow-direction logic for the header-plus-content overlays
    /// (pinned / tech queue / company). All three are a vertical stack anchored
    /// at the top that grows downward. To grow upward instead we do two things:
    ///   1. Flip the panel's pivot.y (1 → 0) so the BOTTOM edge becomes the
    ///      fixed anchor point and added content extends upward.
    ///   2. Reverse the child order so the header (first child when growing
    ///      down) ends up at the bottom — i.e. at the now-fixed pivot edge —
    ///      with the content stacked above it.
    /// DraggablePanel positions by anchor (pivot-independent), so dragging keeps
    /// working after the pivot flip; the saved position tracks the pivot edge,
    /// which is the header in both directions.
    /// </summary>
    public static class OverlayLayout
    {
        /// <param name="topToBottom">Panel children in their natural
        /// grow-DOWN order (header first, content after).</param>
        public static void Apply(RectTransform panel, OverlayGrowDirection dir, params Transform[] topToBottom)
        {
            if (panel == null) return;
            bool up = dir == OverlayGrowDirection.Up;

            var p = panel.pivot;
            float wantY = up ? 0f : 1f;
            if (!Mathf.Approximately(p.y, wantY)) { p.y = wantY; panel.pivot = p; }

            int n = topToBottom.Length;
            for (int i = 0; i < n; i++)
            {
                var child = up ? topToBottom[n - 1 - i] : topToBottom[i];
                if (child != null) child.SetSiblingIndex(i);
            }
        }
    }
}
