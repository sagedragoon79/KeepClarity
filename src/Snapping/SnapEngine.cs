using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Snapping
{
    /// <summary>
    /// Pure edge-snapping math for draggable UGUI panels. Everything happens in one
    /// coordinate space — the dragged panel's canvas-local pixels — so a snap offset
    /// returned here can be added straight to a panel's anchoredPosition. No game
    /// references beyond UnityEngine; just rect geometry.
    /// </summary>
    internal static class SnapEngine
    {
        private static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>The camera a canvas renders through — null for Screen-Space-Overlay.</summary>
        public static Camera? CanvasCamera(Canvas? canvas)
            => (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        /// <summary>
        /// Express <paramref name="rt"/>'s on-screen box in <paramref name="canvasRt"/>'s
        /// local space. Routes through screen space so it works even when rt lives under a
        /// different canvas/camera (e.g. the game minimap). Returns false if degenerate.
        /// </summary>
        public static bool TryLocalRect(RectTransform canvasRt, Camera? canvasCam, RectTransform rt, out Rect rect)
        {
            rect = default;
            if (canvasRt == null || rt == null) return false;

            rt.GetWorldCorners(_corners);                       // [0]=bottom-left, [2]=top-right
            Camera? srcCam = SourceCamera(rt);
            Vector2 blScreen = RectTransformUtility.WorldToScreenPoint(srcCam, _corners[0]);
            Vector2 trScreen = RectTransformUtility.WorldToScreenPoint(srcCam, _corners[2]);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, blScreen, canvasCam, out var bl)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, trScreen, canvasCam, out var tr)) return false;

            rect = Rect.MinMaxRect(Mathf.Min(bl.x, tr.x), Mathf.Min(bl.y, tr.y), Mathf.Max(bl.x, tr.x), Mathf.Max(bl.y, tr.y));
            return rect.width > 0f && rect.height > 0f;
        }

        // The camera rendering rt's own canvas (null for overlay). Render mode lives on the root canvas.
        private static Camera? SourceCamera(RectTransform rt)
        {
            var c = rt.GetComponentInParent<Canvas>();
            if (c == null) return null;
            c = c.rootCanvas;
            return c.renderMode == RenderMode.ScreenSpaceOverlay ? null : c.worldCamera;
        }

        /// <summary>
        /// Nearest-edge snap. Tests both X edges and both Y edges of <paramref name="dragged"/>
        /// against every candidate edge (each other rect's four sides + the screen bounds),
        /// per axis independently, and returns the offset (canvas-local px) to add so the
        /// closest edge within <paramref name="threshold"/> aligns. Testing both dragged
        /// edges against both candidate sides yields flush adjacency (my right ↔ your left)
        /// AND alignment (my left ↔ your left) with no special-casing; corner snap is just X
        /// and Y solved together. Zero if nothing is within threshold.
        /// </summary>
        public static Vector2 Apply(Rect dragged, IList<Rect> others, Rect screen, float threshold)
        {
            float dx = 0f, dy = 0f;
            float bestX = threshold, bestY = threshold;

            void ConsiderX(float candidate)
            {
                float a = candidate - dragged.xMin; if (Mathf.Abs(a) < bestX) { bestX = Mathf.Abs(a); dx = a; }
                float b = candidate - dragged.xMax; if (Mathf.Abs(b) < bestX) { bestX = Mathf.Abs(b); dx = b; }
            }
            void ConsiderY(float candidate)
            {
                float a = candidate - dragged.yMin; if (Mathf.Abs(a) < bestY) { bestY = Mathf.Abs(a); dy = a; }
                float b = candidate - dragged.yMax; if (Mathf.Abs(b) < bestY) { bestY = Mathf.Abs(b); dy = b; }
            }

            if (others != null)
            {
                for (int i = 0; i < others.Count; i++)
                {
                    var r = others[i];
                    ConsiderX(r.xMin); ConsiderX(r.xMax);
                    ConsiderY(r.yMin); ConsiderY(r.yMax);
                }
            }
            ConsiderX(screen.xMin); ConsiderX(screen.xMax);
            ConsiderY(screen.yMin); ConsiderY(screen.yMax);

            return new Vector2(dx, dy);
        }
    }
}
