using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using FFUIOverhaul.Settings.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Main-screen overlay listing the build order — top 10 active build sites by
    /// Build Priority (the hijacked userDefinedBuilders value), highest first.
    /// Same architecture as the Tech Queue overlay: own Canvas (sortingOrder 9),
    /// header-only drag (body click-through), collapsible to an edge tab, and an
    /// Up/Down grow direction. Shown only when EnableBuildQueueOverlay is on.
    /// Content comes from BuildSiteRegistry (no scene scans), reusing buffers (no GC).
    /// </summary>
    public class BuildQueueOverlay
    {
        private const float PanelWidth = 200f;
        private const float HeaderHeight = 22f;
        private const float TabWidth = 22f;
        private const float TabHeight = 70f;
        private const int CanvasSortingOrder = 9;
        private const float TopBarMargin = 44f;
        private const int MaxRows = 10;

        private static readonly Color PanelBg = new(0.11f, 0.09f, 0.09f, 0.92f);
        private static readonly Color BodyTextColor = new(0.78f, 0.75f, 0.62f, 1f);
        private static readonly Color ButtonNormal = new(0.25f, 0.20f, 0.18f, 0.95f);

        private GameObject? _canvasRoot;
        private CanvasScaler? _canvasScaler;
        private GameObject? _expandedPanel;
        private GameObject? _collapsedTab;
        private GameObject? _header;
        private GameObject? _body;
        private TextMeshProUGUI? _contentText;
        private DraggablePanel? _expandedDrag;
        private DraggablePanel? _collapsedDrag;
        private OverlayGrowDirection _lastGrowDir = (OverlayGrowDirection)(-1);
        private readonly List<Image> _opacityImages = new();
        private static readonly List<Image> _pendingChevrons = new();
        private bool _initialized;
        private bool _collapsed;
        private float _refreshTimer;
        private static TMP_FontAsset? _cachedFont;
        private static readonly Regex _trailingVariant = new(@"_\d+[A-Za-z]?$", RegexOptions.Compiled);

        private readonly List<BuildSiteResource> _sitesBuf = new();   // reused each refresh (no GC)
        private readonly StringBuilder _sb = new();                   // reused each refresh (no GC)

        private static readonly Comparison<BuildSiteResource> CompareByPriority = (a, b) =>
        {
            int pa = a.userDefinedBuilders, pb = b.userDefinedBuilders;
            if (pa != pb) return pb.CompareTo(pa);            // priority desc
            return b.GetNumberOfBuilders().CompareTo(a.GetNumberOfBuilders()); // active builders desc
        };

        public void Tick()
        {
            bool wantVisible = FFUIOverhaulMod.EnableBuildQueueOverlay.Value;

            if (!_initialized)
            {
                if (!wantVisible) return; // build lazily on first enable
                try { Build(); _initialized = true; }
                catch (Exception e)
                {
                    FFUIOverhaulMod.Log.Warning($"[BuildQueueOverlay] Build failed: {e.Message}\n{e.StackTrace}");
                    _initialized = true;
                }
            }

            if (_canvasRoot != null && _canvasRoot.activeSelf != wantVisible)
                _canvasRoot.SetActive(wantVisible);
            if (!wantVisible) return;

            UIScaleSync.Sync(_canvasScaler, FFUIOverhaulMod.BuildQueueOverlayScale?.Value ?? 1f);
            ApplyGrowDirection();
            SyncOpacity();
            SyncPendingChevrons();

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 1f) { _refreshTimer = 0f; RefreshDisplay(); }
        }

        public void OnSceneChanged() { /* canvas is DontDestroyOnLoad; Tick re-shows */ }

        private void RefreshDisplay()
        {
            if (_contentText == null) return;
            Patches.BuildSiteRegistry.SnapshotInto(_sitesBuf);
            var sites = _sitesBuf;

            if (sites.Count == 0) { _contentText.text = "<i>No active build sites</i>"; return; }

            sites.Sort(CompareByPriority);
            var sb = _sb; sb.Clear();
            int shown = Math.Min(MaxRows, sites.Count);
            for (int i = 0; i < shown; i++)
            {
                var bs = sites[i];
                int prio = bs.userDefinedBuilders;
                int builders = bs.GetNumberOfBuilders();
                string active = builders > 0 ? $"  <color=#7fbf7f>•{builders}</color>" : "";
                sb.Append($"<color=#d8a93a>P{prio}</color> {SiteName(bs)}{active}");
                if (i < shown - 1) sb.AppendLine();
            }
            if (sites.Count > shown)
                sb.Append($"\n<size=10><i>+{sites.Count - shown} more…</i></size>");
            _contentText.text = sb.ToString();
        }

        private static string SiteName(BuildSiteResource bs)
        {
            try
            {
                var prefab = bs.prefabToConstruct;
                string raw = prefab != null ? prefab.name : bs.buildGroup;
                if (string.IsNullOrEmpty(raw)) return "Build Site";
                raw = raw.Replace("(Clone)", "").Trim();
                raw = _trailingVariant.Replace(raw, "");
                raw = raw.Replace('_', ' ').Trim();
                return raw.Length == 0 ? "Build Site" : raw;
            }
            catch { return "Build Site"; }
        }

        private void Build()
        {
            _canvasRoot = new GameObject("FFUI_BuildQueueOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            _canvasScaler = _canvasRoot.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            UIScaleSync.Sync(_canvasScaler, FFUIOverhaulMod.BuildQueueOverlayScale?.Value ?? 1f);

            _collapsed = FFUIOverhaulMod.BuildQueueCollapsed?.Value ?? false;

            BuildExpandedPanel();
            BuildCollapsedTab();
            WireDragHandles();
            ApplyCollapsed();
            ApplyGrowDirection();
            RefreshDisplay();
        }

        private void BuildExpandedPanel()
        {
            _expandedPanel = NewChild(_canvasRoot!, "ExpandedPanel");
            var rt = (RectTransform)_expandedPanel.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(PanelWidth, 0);
            // Background non-blocking so world clicks pass through the body.
            ApplyFFChrome(_expandedPanel, PanelBg.a).raycastTarget = false;

            var vlg = _expandedPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 6);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fit = _expandedPanel.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var header = NewChild(_expandedPanel, "Header");
            _header = header;
            header.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;
            AddImage(header, new Color(0.83f, 0.63f, 0.19f, 0.10f)); // raycast handle for drag
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 4, 2, 2);
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            var headerLabel = NewText(header, "HeaderLabel", "BUILD QUEUE", 14, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
            if (FFNativeAssets.FontTitle != null) headerLabel.font = FFNativeAssets.FontTitle;
            headerLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            NewArrowButton(header, "CollapseBtn", 180f, 22, ToggleCollapse); // chevron toward left edge

            var bodyGo = NewChild(_expandedPanel, "Body");
            _body = bodyGo;
            _contentText = bodyGo.AddComponent<TextMeshProUGUI>();
            _contentText.fontSize = 12;
            _contentText.color = BodyTextColor;
            _contentText.alignment = TextAlignmentOptions.TopLeft;
            _contentText.raycastTarget = false;
            _contentText.font = GetGameFont();
            _contentText.enableWordWrapping = true;
            _contentText.margin = new Vector4(10, 2, 10, 6);
        }

        private void BuildCollapsedTab()
        {
            _collapsedTab = NewChild(_canvasRoot!, "CollapsedTab");
            var rt = (RectTransform)_collapsedTab.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.sizeDelta = new Vector2(TabWidth, TabHeight);

            var img = ApplyFFChrome(_collapsedTab, PanelBg.a);
            var btn = _collapsedTab.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleCollapse);

            var label = NewText(_collapsedTab, "Label", "BUILD", 11, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.Center);
            if (FFNativeAssets.FontTitle != null) label.font = FFNativeAssets.FontTitle;
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(TabHeight, TabWidth);
            lrt.localEulerAngles = new Vector3(0, 0, -90);
            label.raycastTarget = false;
        }

        private void WireDragHandles()
        {
            if (_expandedPanel == null || _collapsedTab == null || _header == null) return;
            var defaultPos = new Vector2(0.005f, 0.55f);
            var savedPos = new Vector2(
                FFUIOverhaulMod.BuildQueueOverlayPosX?.Value ?? defaultPos.x,
                FFUIOverhaulMod.BuildQueueOverlayPosY?.Value ?? defaultPos.y);

            _expandedDrag = _header.AddComponent<DraggablePanel>();   // header-only handle (body click-through)
            _expandedDrag.Target = (RectTransform)_expandedPanel.transform;
            _expandedDrag.DefaultNormalizedPosition = defaultPos;
            _expandedDrag.OnPositionChanged = SavePosition;
            _expandedDrag.TopMargin = TopBarMargin;
            _expandedDrag.ApplyNormalized(savedPos, persist: false);

            _collapsedDrag = _collapsedTab.AddComponent<DraggablePanel>();
            _collapsedDrag.Target = (RectTransform)_collapsedTab.transform;
            _collapsedDrag.DefaultNormalizedPosition = defaultPos;
            _collapsedDrag.OnPositionChanged = SavePosition;
            _collapsedDrag.TopMargin = TopBarMargin;
            _collapsedDrag.ApplyNormalized(savedPos, persist: false);
        }

        private void ApplyGrowDirection()
        {
            var dir = FFUIOverhaulMod.BuildQueueGrowDirection?.Value ?? OverlayGrowDirection.Down;
            if (dir == _lastGrowDir) return;
            _lastGrowDir = dir;
            bool up = dir == OverlayGrowDirection.Up;
            if (_expandedPanel != null && _header != null && _body != null)
                OverlayLayout.Apply((RectTransform)_expandedPanel.transform, dir, _header.transform, _body.transform);
            if (_collapsedTab != null)
            {
                var trt = (RectTransform)_collapsedTab.transform;
                var p = trt.pivot; p.y = up ? 0f : 1f; trt.pivot = p;
            }
            var saved = new Vector2(
                FFUIOverhaulMod.BuildQueueOverlayPosX?.Value ?? 0.005f,
                FFUIOverhaulMod.BuildQueueOverlayPosY?.Value ?? 0.55f);
            _expandedDrag?.ApplyNormalized(saved, persist: false);
            _collapsedDrag?.ApplyNormalized(saved, persist: false);
        }

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            if (FFUIOverhaulMod.BuildQueueCollapsed != null)
            {
                FFUIOverhaulMod.BuildQueueCollapsed.Value = _collapsed;
                MelonLoader.MelonPreferences.Save();
            }
            ApplyCollapsed();
        }

        private void ApplyCollapsed()
        {
            if (_expandedPanel != null) _expandedPanel.SetActive(!_collapsed);
            if (_collapsedTab != null) _collapsedTab.SetActive(_collapsed);
            var pos = new Vector2(
                FFUIOverhaulMod.BuildQueueOverlayPosX?.Value ?? 0.005f,
                FFUIOverhaulMod.BuildQueueOverlayPosY?.Value ?? 0.55f);
            if (_collapsed) _collapsedDrag?.ApplyNormalized(pos, persist: false);
            else _expandedDrag?.ApplyNormalized(pos, persist: false);
        }

        private static void SavePosition(Vector2 normalized)
        {
            if (FFUIOverhaulMod.BuildQueueOverlayPosX == null || FFUIOverhaulMod.BuildQueueOverlayPosY == null) return;
            FFUIOverhaulMod.BuildQueueOverlayPosX.Value = normalized.x;
            FFUIOverhaulMod.BuildQueueOverlayPosY.Value = normalized.y;
            MelonLoader.MelonPreferences.Save();
        }

        // ── helpers (mirror TechQueueMainOverlay) ──────────────────────────
        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        private static TextMeshProUGUI NewText(GameObject parent, string name, string text,
            float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = NewChild(parent, name);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.fontStyle = style;
            t.color = color; t.alignment = align; t.raycastTarget = false;
            t.font = GetGameFont();
            try { t.outlineWidth = 0.18f; t.outlineColor = new Color32(0, 0, 0, 220); } catch { }
            return t;
        }

        private static GameObject NewArrowButton(GameObject parent, string name, float zRotation, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var bg = AddImage(go, ButtonNormal);
            if (FFNativeAssets.PanelBorderSimple != null)
            {
                bg.sprite = FFNativeAssets.PanelBorderSimple;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            btn.onClick.AddListener(onClick);

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);
            var arrt = (RectTransform)arrowGo.transform;
            arrt.anchorMin = arrt.anchorMax = arrt.pivot = new Vector2(0.5f, 0.5f);
            arrt.sizeDelta = new Vector2(18, 18);
            arrt.anchoredPosition = Vector2.zero;
            arrt.localRotation = Quaternion.Euler(0, 0, zRotation);
            var arrowImg = arrowGo.GetComponent<Image>();
            arrowImg.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            arrowImg.raycastTarget = false;
            if (FFNativeAssets.ArrowChevron != null) arrowImg.sprite = FFNativeAssets.ArrowChevron;
            else _pendingChevrons.Add(arrowImg);
            return go;
        }

        private static void SyncPendingChevrons()
        {
            if (_pendingChevrons.Count == 0) return;
            var sprite = FFNativeAssets.ArrowChevron;
            if (sprite == null) return;
            for (int i = _pendingChevrons.Count - 1; i >= 0; i--)
            {
                var img = _pendingChevrons[i];
                if (img == null) { _pendingChevrons.RemoveAt(i); continue; }
                img.sprite = sprite;
                _pendingChevrons.RemoveAt(i);
            }
        }

        private Image ApplyFFChrome(GameObject go, float alpha)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            var frame = FFNativeAssets.PanelBorderThick ?? FFNativeAssets.PanelBorderDark;
            if (frame != null) { img.sprite = frame; img.type = Image.Type.Sliced; img.color = new Color(1f, 1f, 1f, alpha); }
            else img.color = PanelBg;
            _opacityImages.Add(img);
            return img;
        }

        private void SyncOpacity()
        {
            float wanted = Mathf.Clamp(FFUIOverhaulMod.BuildQueueOverlayOpacity?.Value ?? 0.92f, 0.05f, 1f);
            for (int i = 0; i < _opacityImages.Count; i++)
            {
                var img = _opacityImages[i];
                if (img == null) continue;
                if (Mathf.Abs(img.color.a - wanted) < 0.005f) continue;
                var c = img.color; c.a = wanted; img.color = c;
            }
        }

        private static TMP_FontAsset? GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            _cachedFont = FFNativeAssets.FontBody;
            if (_cachedFont != null) return _cachedFont;
            var all = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all) if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }
    }
}
