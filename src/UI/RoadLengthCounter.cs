using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// A small cursor-following readout showing how many grid squares the road
    /// you're currently dragging will cover — visible BEFORE you commit the
    /// placement, so you can size a road to a plan without counting tiles.
    ///
    /// HOW THE COUNT IS SOURCED
    /// Roads are splines, not grid segments (walls use SegmentBuilder; roads use
    /// SplineRoadBuilder → PlaceableSplineRoad → SplineRoadContainer). The
    /// container walks its cubic bezier and records every terrain cell the curve
    /// passes through into its public <c>cells</c> set — that IS the road's
    /// footprint, so <c>cells.Count</c> is the game's own tile count rather than
    /// anything we re-derive. (Do not use <c>buildCells</c>: that samples the
    /// curve at only 5 points for build markers and maxes out at 5.)
    ///
    /// LIFECYCLE
    /// Two feeds: RoadLengthPatch pushes a count whenever the preview curve
    /// changes, and a per-frame heartbeat (SplineRoadBuilder.UpdateSegmentPlacer,
    /// whose isPlacing is public) holds the label alive while the road tool is
    /// active. The heartbeat matters because SetControlPoints fires only on
    /// CHANGE — without it, holding the mouse still starved the label and it
    /// vanished mid-drag. HideDelay is then just the tail that covers placing,
    /// cancelling, tool-switching and scene changes with one mechanism.
    /// </summary>
    internal static class RoadLengthCounter
    {
        // Above FF's window canvas (10) so the readout is never buried by the
        // build panel, but below KC's settings canvas.
        private const int CanvasSortingOrder = 30;

        // Grace period after the last report before the label hides. Long enough
        // to survive a frame where the curve didn't change, short enough that it
        // vanishes the instant you finish placing.
        private const float HideDelay = 0.2f;

        // Cursor offset, in pixels. Up-and-right of the pointer keeps the label
        // clear of the road ghost and of FF's own cost tooltip below the cursor.
        private static readonly Vector2 CursorOffset = new Vector2(26f, 26f);

        private static GameObject? _root;
        private static RectTransform? _rt;
        private static TextMeshProUGUI? _label;
        private static float _expiresAt;
        private static int _lastTiles = -1;
        private static bool _failed;

        /// <summary>Feed the counter a fresh tile count (called from the patch on
        /// every preview update). Showing is implicit — reporting keeps it alive.</summary>
        public static void Report(int tiles)
        {
            if (_failed) return;
            _expiresAt = Time.unscaledTime + HideDelay;

            if (!EnsureBuilt()) return;
            if (tiles != _lastTiles)
            {
                _lastTiles = tiles;
                _label!.text = tiles + " " + Localization.KcLoc.Tr("KeepClarity/road/tiles", "tiles");
            }
            if (!_root!.activeSelf) _root.SetActive(true);
        }

        /// <summary>Heartbeat from the road builder: placement is still active, so
        /// hold the current count on screen even though the curve hasn't changed.</summary>
        public static void KeepAlive()
        {
            if (_root != null && _root.activeSelf) _expiresAt = Time.unscaledTime + HideDelay;
        }

        /// <summary>Driven from Plugin.OnUpdate: follows the cursor while live and
        /// hides once the road builder stops reporting.</summary>
        public static void Tick()
        {
            if (_root == null || !_root.activeSelf) return;

            if (Time.unscaledTime > _expiresAt)
            {
                _root.SetActive(false);
                _lastTiles = -1;
                return;
            }

            // ConstantPixelSize overlay canvas: mouse pixels map straight onto
            // anchoredPosition because the label anchors/pivots at bottom-left.
            var p = (Vector2)Input.mousePosition + CursorOffset;
            _rt!.anchoredPosition = p;
        }

        /// <summary>Hide immediately (scene change / feature turned off).</summary>
        public static void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
            _lastTiles = -1;
        }

        private static bool EnsureBuilt()
        {
            if (_root != null) return true;
            try
            {
                _root = new GameObject("FFUI_RoadLengthCounter",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                Object.DontDestroyOnLoad(_root);

                var canvas = _root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = CanvasSortingOrder;

                var scaler = _root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                // Label root: bottom-left anchored so anchoredPosition == screen px.
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(_root.transform, worldPositionStays: false);
                _rt = labelGo.GetComponent<RectTransform>();
                _rt.anchorMin = _rt.anchorMax = _rt.pivot = Vector2.zero;
                _rt.sizeDelta = new Vector2(160f, 30f);

                var bg = labelGo.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.62f);
                bg.raycastTarget = false;

                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(labelGo.transform, worldPositionStays: false);
                var trt = textGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f, 2f);
                trt.offsetMax = new Vector2(-8f, -2f);

                _label = textGo.AddComponent<TextMeshProUGUI>();
                _label.fontSize = 18f;
                _label.fontStyle = FontStyles.Bold;
                _label.color = Color.white;
                _label.alignment = TextAlignmentOptions.Center;
                _label.raycastTarget = false;
                if (Settings.UI.FFNativeAssets.FontTitle != null)
                    _label.font = Settings.UI.FFNativeAssets.FontTitle;
                try
                {
                    _label.outlineWidth = 0.18f;
                    _label.outlineColor = new Color32(0, 0, 0, 220);
                }
                catch { /* font asset without an outline channel — cosmetic only */ }

                _root.SetActive(false);
                return true;
            }
            catch (System.Exception e)
            {
                // One-shot: never spam the log from a per-frame path.
                _failed = true;
                FFUIOverhaulMod.Log.Warning("[RoadLength] UI build failed, counter disabled: " + e.Message);
                return false;
            }
        }
    }
}
