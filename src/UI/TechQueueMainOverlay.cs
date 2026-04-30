using System;
using System.Text;
using FFUIOverhaul.TechTree;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Main-screen overlay listing the auto-research queue. Always visible
    /// while playing on the Map scene; collapsible to a small tab on the
    /// left edge. Same architecture as PinnedResourceOverlay (own Canvas,
    /// low sortingOrder so other UI windows draw over it).
    /// </summary>
    public class TechQueueMainOverlay
    {
        private const float PanelWidth = 180f;
        private const float HeaderHeight = 22f;
        private const float TabWidth = 22f;
        private const float TabHeight = 70f;
        private const int CanvasSortingOrder = 1;

        private static readonly Color PanelBg = new(0.11f, 0.09f, 0.09f, 0.92f);
        private static readonly Color HeaderTextColor = new(0.83f, 0.63f, 0.19f, 1f);
        private static readonly Color BodyTextColor = new(0.75f, 0.72f, 0.60f, 1f);
        private static readonly Color ButtonNormal = new(0.25f, 0.20f, 0.18f, 0.95f);

        public bool Visible
        {
            get => _canvasRoot != null && _canvasRoot.activeSelf;
            set { if (_canvasRoot != null) _canvasRoot.SetActive(value); }
        }

        private GameObject? _canvasRoot;
        private GameObject? _expandedPanel;
        private GameObject? _collapsedTab;
        private TextMeshProUGUI? _contentText;
        private TextMeshProUGUI? _collapseButtonLabel;
        private bool _initialized;
        private bool _collapsed;
        private static TMP_FontAsset? _cachedFont;

        public void Tick()
        {
            if (!_initialized)
            {
                try { Build(); _initialized = true; RefreshDisplay(); }
                catch (Exception e)
                {
                    FFUIOverhaulMod.Log.Warning($"[TechQueueOverlay] Build failed: {e.Message}\n{e.StackTrace}");
                    _initialized = true;
                }
            }
        }

        public void RefreshDisplay()
        {
            if (_contentText == null) return;
            var sb = new StringBuilder();
            if (TechAutoQueue.Count == 0)
            {
                sb.AppendLine("<i>Queue empty</i>");
                sb.Append("<size=10>Open tech tree, hover a node, press Q.</size>");
            }
            else
            {
                var gm = UnitySingleton<GameManager>.Instance;
                var tm = gm?.techTreeManager;
                var ids = TechAutoQueue.Queue;
                for (int i = 0; i < ids.Count; i++)
                {
                    int id = ids[i];
                    string name = ResolveTechName(tm, id);
                    string suffix = "";
                    if (tm != null && tm.GetTechTreeNodeData(id, out _, out _, out var state, out int numRanks, out int curRank, out _, out _))
                    {
                        suffix = state switch
                        {
                            TechTreeNodeData.State.Active => " <color=#7fbf7f>✓</color>",
                            TechTreeNodeData.State.PrereqsMet => $"  <color=#aaa>{curRank}/{numRanks}</color>",
                            TechTreeNodeData.State.Unlocked => "  <color=#888>(prereq)</color>",
                            TechTreeNodeData.State.Unknown => "  <color=#666>(locked)</color>",
                            _ => ""
                        };
                    }
                    sb.Append($"{i + 1}. {name}{suffix}");
                    if (i < ids.Count - 1) sb.AppendLine();
                }
            }
            _contentText.text = sb.ToString();
        }

        /// <summary>
        /// Resolve a tech node id to its display name. GetTechName() already
        /// returns the readable name in this build (e.g. "Mining", "Forging") —
        /// trying to run it back through LocalizationManager.Localize wraps the
        /// result in "Localization tag not found 'X'!" because the name isn't a
        /// valid loc key. So we use it raw.
        /// </summary>
        private static string ResolveTechName(TechTreeManager? tm, int id)
        {
            if (tm == null || tm.techTreeNodeData == null) return $"#{id}";
            foreach (var n in tm.techTreeNodeData)
            {
                if (n == null || n.GetId() != id) continue;
                var name = n.GetTechName();
                return string.IsNullOrEmpty(name) ? $"#{id}" : name;
            }
            return $"#{id}";
        }

        private void Build()
        {
            _canvasRoot = new GameObject("FFUI_TechQueueOverlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            _canvasRoot.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            BuildExpandedPanel();
            BuildCollapsedTab();
            ApplyCollapsed();
        }

        private void BuildExpandedPanel()
        {
            _expandedPanel = NewChild(_canvasRoot!, "ExpandedPanel");
            var rt = (RectTransform)_expandedPanel.transform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(8, -180);
            rt.sizeDelta = new Vector2(PanelWidth, 0);

            AddImage(_expandedPanel, PanelBg);

            var vlg = _expandedPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 6);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fit = _expandedPanel.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header row
            var header = NewChild(_expandedPanel, "Header");
            header.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 4, 2, 2);
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var headerLabel = NewText(header, "HeaderLabel", "TECH QUEUE", 11, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.MidlineLeft);
            headerLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Collapse button (◀)
            var collapseBtn = NewIconButton(header, "CollapseBtn", "◀", 22, ToggleCollapse);
            _collapseButtonLabel = collapseBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Body text — TMPro directly as child of the panel. TMPro implements
            // ILayoutElement and reports a content-driven preferredHeight, which
            // the parent VLG + ContentSizeFitter use to size the panel correctly.
            // Use TMPro.margin for internal padding (left, top, right, bottom).
            var bodyGo = NewChild(_expandedPanel, "Body");
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
            rt.anchoredPosition = new Vector2(4, -180);
            rt.sizeDelta = new Vector2(TabWidth, TabHeight);

            var img = AddImage(_collapsedTab, PanelBg);
            var btn = _collapsedTab.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleCollapse);

            // Rotate the label -90° so "TECH" reads top-to-bottom along the
            // narrow tab. Pre-rotation rect is wide-and-short (TabHeight × TabWidth);
            // after rotation it visually occupies the tall-and-narrow tab.
            var label = NewText(_collapsedTab, "Label", "TECH", 11, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.Center);
            var lrt = label.rectTransform;
            lrt.anchorMin = new Vector2(0.5f, 0.5f);
            lrt.anchorMax = new Vector2(0.5f, 0.5f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(TabHeight, TabWidth);
            lrt.localEulerAngles = new Vector3(0, 0, -90);
            label.raycastTarget = false;
        }

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            ApplyCollapsed();
        }

        private void ApplyCollapsed()
        {
            if (_expandedPanel != null) _expandedPanel.SetActive(!_collapsed);
            if (_collapsedTab != null) _collapsedTab.SetActive(_collapsed);
        }

        // ── UGUI helpers (mirror PinnedResourceOverlay's) ──────────────────

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
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.font = GetGameFont();
            return t;
        }

        private static GameObject NewIconButton(GameObject parent, string name, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var img = AddImage(go, ButtonNormal);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var lbl = NewText(go, "Label", label, 12, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.Center);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return go;
        }

        private static TMP_FontAsset? GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            var all = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }
    }
}
