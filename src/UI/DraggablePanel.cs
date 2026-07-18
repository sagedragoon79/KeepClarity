using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Attach to a GameObject that should act as a drag handle. The handle's
    /// pointer events drag the configured target RectTransform around its
    /// canvas. Right-click on the handle resets to the default position.
    ///
    /// Persistence is delegated to a callback that takes normalized 0..1
    /// canvas-space coordinates of the target's pivot — saving normalized
    /// coords means the panel lands in the same spot after resolution changes.
    ///
    /// The handle's Image must have raycastTarget = true; child buttons inside
    /// the handle still receive their own clicks because the event system's
    /// drag propagation walks up the parent chain looking for IDragHandler.
    /// </summary>
    public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        public RectTransform? Target;          // panel to move
        public RectTransform? DefaultTarget;   // optional second target moved in lockstep (e.g. config panel)
        public Vector2 DefaultTargetOffset;    // DefaultTarget anchored offset relative to Target
        public Action<Vector2>? OnPositionChanged; // normalized 0..1 of Target.pivot in canvas space
        public Vector2 DefaultNormalizedPosition = new(0.5f, 0.5f);
        public float TopMargin; // reserved px at the top so the panel can't slide under FF's top bar

        private Canvas? _canvas;
        private RectTransform? _canvasRt;
        private Vector2 _startPointerLocal;
        private Vector2 _startTargetPos;

        private Canvas? Canvas => _canvas ??= GetComponentInParent<Canvas>();
        private RectTransform? CanvasRt => _canvasRt ??= Canvas?.transform as RectTransform;

        // Self-register so other panels can snap to this one (and vice versa).
        private void OnEnable() => Snapping.OverlayRegistry.Register(this);
        private void OnDisable() => Snapping.OverlayRegistry.Unregister(this);

        public void OnBeginDrag(PointerEventData e)
        {
            if (Target == null || CanvasRt == null) return;
            if (e.button != PointerEventData.InputButton.Left) return;

            // Convert pointer screen pos → canvas-local space. Drag deltas in
            // canvas-local handle DPI/scale automatically; computing in screen
            // pixels and dividing by scaleFactor works too but breaks under
            // ScaleWithScreenSize where the relationship is non-trivial.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                CanvasRt, e.position, e.pressEventCamera, out _startPointerLocal);
            _startTargetPos = Target.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Target == null || CanvasRt == null) return;
            if (e.button != PointerEventData.InputButton.Left) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                CanvasRt, e.position, e.pressEventCamera, out var local);
            var newPos = _startTargetPos + (local - _startPointerLocal);
            ApplyPosition(newPos, persist: true, snap: true);
        }

        public void OnPointerClick(PointerEventData e)
        {
            // Right-click resets — escape hatch if the panel got dragged off-screen.
            if (e.button != PointerEventData.InputButton.Right) return;
            ApplyNormalized(DefaultNormalizedPosition, persist: true);
        }

        /// <summary>
        /// Set the target's anchoredPosition, clamped so at least 32px of the
        /// panel stays on-screen on every edge. Persists if requested.
        /// </summary>
        public void ApplyPosition(Vector2 anchoredPos, bool persist, bool snap = false)
        {
            if (Target == null || CanvasRt == null) return;

            // Edge-snap to other overlays / the minimap / the screen BEFORE clamping, so a
            // snap toward the reserved top strip still gets clamped sane. Drag-only (snap).
            if (snap) anchoredPos += ComputeSnapOffset(anchoredPos);

            // Clamp into a safe rect: panel pivot must stay within
            // [margin, canvasSize - margin - panelSize] (anchor-corrected).
            // Margin is small — the user can always right-click the handle to
            // recover, so the clamp's only real job is preventing total
            // off-screen disappearance, not preserving a comfortable gap.
            var canvasSize = CanvasRt.rect.size;
            var panelSize = Target.rect.size;
            const float Margin = 1f;

            // Anchor min == anchor max for our overlays (corner-anchored), so
            // anchoredPosition is in canvas-local coords offset from that anchor.
            // Compute valid bounds based on the anchor + pivot.
            var anchor = Target.anchorMin;
            var pivot = Target.pivot;
            float anchorX = anchor.x * canvasSize.x;
            float anchorY = anchor.y * canvasSize.y;

            // World-space bounds of the panel given (anchorX + anchoredPos.x):
            //   leftEdgeX = anchorX + anchoredPos.x - pivot.x * panelSize.x
            //   rightEdgeX = leftEdgeX + panelSize.x
            // Constrain so leftEdgeX >= Margin and rightEdgeX <= canvasSize.x - Margin.
            float minX = Margin - anchorX + pivot.x * panelSize.x;
            float maxX = canvasSize.x - Margin - anchorX - (1f - pivot.x) * panelSize.x;
            float minY = Margin - anchorY + pivot.y * panelSize.y;
            // TopMargin reserves a strip at the top (FF's top bar) so the panel
            // collides with / stays below it instead of sliding underneath.
            float maxY = canvasSize.y - Margin - TopMargin - anchorY - (1f - pivot.y) * panelSize.y;

            // If the panel is wider/taller than the safe area, prefer the min
            // (so at least the top-left stays reachable).
            if (minX > maxX) maxX = minX;
            if (minY > maxY) maxY = minY;

            anchoredPos.x = Mathf.Clamp(anchoredPos.x, minX, maxX);
            anchoredPos.y = Mathf.Clamp(anchoredPos.y, minY, maxY);

            Target.anchoredPosition = anchoredPos;
            if (DefaultTarget != null)
                DefaultTarget.anchoredPosition = anchoredPos + DefaultTargetOffset;

            if (persist && OnPositionChanged != null)
                OnPositionChanged(NormalizedFromAnchored(anchoredPos));
        }

        // Snap offset (canvas-local px) for the proposed position: aligns the nearest edge
        // within the threshold to another overlay, the minimap, or a screen edge. Recomputed
        // from the raw drag position every tick (no feedback, no hysteresis), rounded to whole
        // pixels so flush panels land seam-free.
        private Vector2 ComputeSnapOffset(Vector2 proposedAnchored)
        {
            var on = FFUIOverhaulMod.SnapOverlaysEnabled;
            if (on == null || !on.Value) return Vector2.zero;
            var canvas = Canvas;
            if (Target == null || CanvasRt == null || canvas == null) return Vector2.zero;
            float threshold = FFUIOverhaulMod.SnapThresholdPx != null ? FFUIOverhaulMod.SnapThresholdPx.Value : 12f;
            if (threshold <= 0f) return Vector2.zero;

            var cam = Snapping.SnapEngine.CanvasCamera(canvas);
            if (!Snapping.SnapEngine.TryLocalRect(CanvasRt, cam, Target, out var cur)) return Vector2.zero;
            var moveDelta = proposedAnchored - Target.anchoredPosition;   // canvas-local delta
            var proposed = new Rect(cur.xMin + moveDelta.x, cur.yMin + moveDelta.y, cur.width, cur.height);
            var off = Snapping.OverlayRegistry.ComputeSnap(this, CanvasRt, cam, proposed, threshold);
            return new Vector2(Mathf.Round(off.x), Mathf.Round(off.y));
        }

        public void ApplyNormalized(Vector2 normalized, bool persist)
        {
            if (Target == null || CanvasRt == null) return;
            ApplyPosition(AnchoredFromNormalized(normalized), persist);
        }

        /// <summary>Pivot point of Target as 0..1 of canvas size.</summary>
        private Vector2 NormalizedFromAnchored(Vector2 anchoredPos)
        {
            var canvasSize = CanvasRt!.rect.size;
            var anchor = Target!.anchorMin;
            float pivotX = anchor.x * canvasSize.x + anchoredPos.x;
            float pivotY = anchor.y * canvasSize.y + anchoredPos.y;
            return new Vector2(
                canvasSize.x > 0 ? pivotX / canvasSize.x : 0,
                canvasSize.y > 0 ? pivotY / canvasSize.y : 0);
        }

        private Vector2 AnchoredFromNormalized(Vector2 normalized)
        {
            var canvasSize = CanvasRt!.rect.size;
            var anchor = Target!.anchorMin;
            float pivotX = normalized.x * canvasSize.x;
            float pivotY = normalized.y * canvasSize.y;
            return new Vector2(pivotX - anchor.x * canvasSize.x, pivotY - anchor.y * canvasSize.y);
        }
    }
}
