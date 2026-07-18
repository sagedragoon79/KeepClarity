using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// A compact Gantt-style panel hanging directly below FF's top-bar season
    /// strip: one row per forageable type on the map, with green segments marking
    /// the calendar quarters it is in season, plus a thin marker in lockstep with
    /// the game's own "today" position.
    ///
    /// Alignment strategy: the panel is parented under the strip's own Slider
    /// RectTransform (width-locked to it), and every segment is placed with
    /// anchor FRACTIONS taken from UITopBar's serialized season boundaries
    /// (seasonBarSummerStart/AutumnStart/WinterStart) — so the segments line up
    /// with the strip's baked art at any resolution, and the today-marker just
    /// mirrors seasonSlider.value, which FF itself maintains as the 0..1
    /// x-fraction of the current day.
    ///
    /// Visibility mirrors the game's own season popup (hover the strip to peek,
    /// pin with FF's info toggle), or always-on via pref. Driven by Tick() from
    /// Plugin.OnUpdate, same pattern as InfoWindowDock — no MonoBehaviour.
    /// Everything binds reflectively and degrades to a one-time warning.
    /// </summary>
    internal static class ForageCalendarBar
    {
        private const float RowHeight = 24f;
        private const float IconSize = 22f;
        private const float PadTop = 3f;
        private const float PadBottom = 3f;
        private const float PopupGap = 6f;   // gap between the popup text block and the bar
        private const float PopupFallbackY = -70f; // clearance if the text rect can't be measured

        private static readonly Color PanelBg    = new Color(0.07f, 0.06f, 0.045f, 0.88f);
        private static readonly Color TrackBase  = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color SegmentDim = new Color(0.45f, 0.62f, 0.28f, 0.55f);
        private static readonly Color SegmentHot = new Color(0.62f, 0.85f, 0.36f, 0.95f);
        private static readonly Color MarkerCol  = new Color(1f, 1f, 1f, 0.85f);

        // Reflected UITopBar members (bound once).
        private static bool _bound;
        private static bool _disabled;
        private static FieldInfo? _fiSeasonSliders;   // List<Slider>
        private static FieldInfo? _fiSummerStart;     // float
        private static FieldInfo? _fiAutumnStart;     // float
        private static FieldInfo? _fiWinterStart;     // float
        private static FieldInfo? _fiSeasonBarMarker; // GameObject (the hover/pin popup)
        private static FieldInfo? _fiSeasonBarText;   // List<Text> (the popup's date/weather lines)

        // Live state (all die with the Map scene; Unity-null checks rebuild).
        private static UITopBar? _topBar;
        private static Slider? _slider;
        private static GameObject? _panel;
        private static RectTransform? _marker;
        private static GameObject? _popup;
        private static int _builtDay = -1;
        private static int _builtRowCount = -1;
        private static float _nextAttemptTime;

        public static void ResetState()
        {
            // Scene objects are destroyed with the map; just drop the refs.
            _topBar = null;
            _slider = null;
            _panel = null;
            _marker = null;
            _popup = null;
            _builtDay = -1;
            _builtRowCount = -1;
            _nextAttemptTime = 0f;
            ForageSeasonData.ResetState();
        }

        /// <summary>Called every frame from Plugin.OnUpdate while a GameManager
        /// exists. Lazily builds the panel, keeps visibility mirrored to the
        /// vanilla season popup, and rebuilds rows when the in-game day advances.</summary>
        public static void Tick()
        {
            if (_disabled) return;

            if (!FFUIOverhaulMod.EnableForageCalendar.Value)
            {
                if (_panel != null) _panel.SetActive(false);
                return;
            }

            if (_panel == null)
            {
                if (Time.unscaledTime < _nextAttemptTime) return;
                _nextAttemptTime = Time.unscaledTime + 2f;
                TryBuild();
                if (_panel == null) return;
            }

            try
            {
                bool popupShown = _popup != null && _popup.activeSelf;
                bool visible = FFUIOverhaulMod.ForageCalendarAlwaysShow.Value || popupShown;
                if (_panel.activeSelf != visible) _panel.SetActive(visible);
                if (!visible) return;

                // The popup's date/weather text renders in the space right under the
                // strip — when it's up, slide the bar down below the text block so
                // the two never overlap.
                var panelRT = (RectTransform)_panel.transform;
                panelRT.anchoredPosition = new Vector2(0f, popupShown ? PopupClearanceY() : -2f);

                // Rebuild rows when the day (or the set of forage types) changes.
                var rows = ForageSeasonData.GetRows();
                if (ForageSeasonData.CacheDay != _builtDay || rows.Count != _builtRowCount)
                    RebuildRows(rows);

                // Today-marker mirrors the game's own slider fraction.
                if (_marker != null && _slider != null)
                {
                    float v = Mathf.Clamp01(_slider.value);
                    _marker.anchorMin = new Vector2(v, 0f);
                    _marker.anchorMax = new Vector2(v, 1f);
                }
            }
            catch (Exception e)
            {
                Disable("tick failed: " + e.Message);
            }
        }

        /// <summary>Panel-top anchoredPosition.y that clears the popup's text block:
        /// measures the active date/weather Text rect's bottom edge in slider-local
        /// space (the panel's parent). Falls back to a fixed clearance if the text
        /// can't be measured.</summary>
        private static float PopupClearanceY()
        {
            try
            {
                if (_slider == null || _topBar == null || _fiSeasonBarText == null) return PopupFallbackY;
                if (_fiSeasonBarText.GetValue(_topBar) is not List<Text> texts) return PopupFallbackY;

                var sliderRT = _slider.GetComponent<RectTransform>();
                foreach (var t in texts)
                {
                    if (t == null || !t.gameObject.activeInHierarchy) continue;
                    var textRT = t.rectTransform;
                    var corners = new Vector3[4];
                    textRT.GetWorldCorners(corners);
                    // corners[0] = bottom-left in world space.
                    float bottomLocal = sliderRT.InverseTransformPoint(corners[0]).y;
                    // anchoredPosition.y is relative to the slider rect's bottom edge
                    // (our anchors sit at parent y=0), pivot is the panel's top.
                    return (bottomLocal - PopupGap) - sliderRT.rect.yMin;
                }
            }
            catch { /* fall through to the fixed clearance */ }
            return PopupFallbackY;
        }

        private static void Disable(string why)
        {
            _disabled = true;
            FFUIOverhaulMod.Log.Warning("[ForageCalendar] " + why + " — season bar disabled for this session.");
            if (_panel != null) _panel.SetActive(false);
        }

        private static bool Bind()
        {
            if (_bound) return true;
            _fiSeasonSliders   = AccessTools.Field(typeof(UITopBar), "seasonSliders");
            _fiSummerStart     = AccessTools.Field(typeof(UITopBar), "seasonBarSummerStart");
            _fiAutumnStart     = AccessTools.Field(typeof(UITopBar), "seasonBarAutumnStart");
            _fiWinterStart     = AccessTools.Field(typeof(UITopBar), "seasonBarWinterStart");
            _fiSeasonBarMarker = AccessTools.Field(typeof(UITopBar), "seasonBarMarker");
            _fiSeasonBarText   = AccessTools.Field(typeof(UITopBar), "seasonBarText");
            if (_fiSeasonSliders == null || _fiSummerStart == null || _fiAutumnStart == null
                || _fiWinterStart == null || _fiSeasonBarMarker == null)
            {
                Disable("UITopBar season fields not found (game update?)");
                return false;
            }
            _bound = true;
            return true;
        }

        private static void TryBuild()
        {
            try
            {
                if (!Bind()) return;

                var gm = UnitySingleton<GameManager>.Instance;
                _topBar = gm != null && gm.buildManager != null ? gm.buildManager.uiTopBar : null;
                if (_topBar == null) return;

                // Pick whichever strip variant (hi-res/lo-res) is actually on screen.
                _slider = null;
                if (_fiSeasonSliders!.GetValue(_topBar) is List<Slider> sliders)
                    foreach (var s in sliders)
                        if (s != null && s.gameObject.activeInHierarchy) { _slider = s; break; }
                if (_slider == null) return;

                _popup = _fiSeasonBarMarker!.GetValue(_topBar) as GameObject;

                var sliderRT = _slider.GetComponent<RectTransform>();
                if (sliderRT == null || sliderRT.rect.width < 10f) return;

                // Don't build until the map's forageables are queryable.
                var rows = ForageSeasonData.GetRows();
                if (rows.Count == 0) return;

                _panel = new GameObject("KC_ForageCalendar", typeof(RectTransform), typeof(Image));
                var rt = (RectTransform)_panel.transform;
                rt.SetParent(sliderRT, worldPositionStays: false);
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(0f, -2f);
                _panel.GetComponent<Image>().color = PanelBg;
                _panel.GetComponent<Image>().raycastTarget = false;

                RebuildRows(rows);
                _panel.SetActive(false); // Tick decides visibility next frame
                FFUIOverhaulMod.Log.Msg($"[ForageCalendar] season bar built — {rows.Count} forageable types on this map.");
            }
            catch (Exception e)
            {
                Disable("build failed: " + e.Message);
            }
        }

        private static void RebuildRows(List<ForageSeasonData.Row> rows)
        {
            if (_panel == null || _topBar == null) return;
            var rt = (RectTransform)_panel.transform;

            // Wipe previous rows + marker and start clean (≤8 rows, cheap, once per day).
            for (int i = rt.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(rt.GetChild(i).gameObject);

            float height = PadTop + rows.Count * RowHeight + PadBottom;
            rt.sizeDelta = new Vector2(0f, height);

            // Quarter boundaries as 0..1 fractions of the strip, straight from FF.
            float summer = (float)_fiSummerStart!.GetValue(_topBar);
            float autumn = (float)_fiAutumnStart!.GetValue(_topBar);
            float winter = (float)_fiWinterStart!.GetValue(_topBar);
            // Segment fraction ranges indexed by quarter bit (Spring..Winter).
            float[] segA = { 0f, summer, autumn, winter };
            float[] segB = { summer, autumn, winter, 1f };

            var gm = UnitySingleton<GameManager>.Instance;
            int nowBit = -1;
            if (gm != null && gm.timeManager != null)
            {
                gm.timeManager.GetSeason(out var season, out _);
                nowBit = (int)season - 1;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float yTop = -(PadTop + i * RowHeight);

                var rowGo = new GameObject("Row_" + row.TypeIndex, typeof(RectTransform));
                var rowRT = (RectTransform)rowGo.transform;
                rowRT.SetParent(rt, worldPositionStays: false);
                rowRT.anchorMin = new Vector2(0f, 1f);
                rowRT.anchorMax = new Vector2(1f, 1f);
                rowRT.pivot = new Vector2(0.5f, 1f);
                rowRT.offsetMin = new Vector2(0f, yTop - RowHeight);
                rowRT.offsetMax = new Vector2(0f, yTop);

                // Faint full-width track so "out of season" reads as an empty lane.
                AddSegment(rowRT, 0f, 1f, TrackBase, "Track");

                for (int q = 0; q < 4; q++)
                {
                    if ((row.SeasonMask & (1 << q)) == 0) continue;
                    AddSegment(rowRT, segA[q], segB[q], q == nowBit ? SegmentHot : SegmentDim, "Seg" + q);
                }

                // Type icon at the row's left edge, drawn over the track (added
                // after the segments so it renders on top). Inside the panel so it
                // can never clip off the screen edge; segments keep their strip
                // alignment underneath it.
                if (row.Icon != null)
                {
                    var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    var iconRT = (RectTransform)iconGo.transform;
                    iconRT.SetParent(rowRT, worldPositionStays: false);
                    iconRT.anchorMin = new Vector2(0f, 0.5f);
                    iconRT.anchorMax = new Vector2(0f, 0.5f);
                    iconRT.pivot = new Vector2(0f, 0.5f);
                    iconRT.anchoredPosition = new Vector2(3f, 0f);
                    iconRT.sizeDelta = new Vector2(IconSize, IconSize);
                    var img = iconGo.GetComponent<Image>();
                    img.sprite = row.Icon;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }
            }

            // Today-marker spans every row; Tick keeps its x on the slider value.
            var markerGo = new GameObject("TodayMarker", typeof(RectTransform), typeof(Image));
            _marker = (RectTransform)markerGo.transform;
            _marker.SetParent(rt, worldPositionStays: false);
            _marker.anchorMin = new Vector2(0f, 0f);
            _marker.anchorMax = new Vector2(0f, 1f);
            _marker.pivot = new Vector2(0.5f, 0.5f);
            _marker.sizeDelta = new Vector2(2f, 0f);
            _marker.anchoredPosition = Vector2.zero;
            var markerImg = markerGo.GetComponent<Image>();
            markerImg.color = MarkerCol;
            markerImg.raycastTarget = false;

            _builtDay = ForageSeasonData.CacheDay;
            _builtRowCount = rows.Count;
        }

        private static void AddSegment(RectTransform parent, float a, float b, Color color, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var srt = (RectTransform)go.transform;
            srt.SetParent(parent, worldPositionStays: false);
            srt.anchorMin = new Vector2(a, 0.15f);
            srt.anchorMax = new Vector2(b, 0.85f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
