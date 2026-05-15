using System;
using System.Text;
using FFUIOverhaul.Settings.UI;
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
        private CanvasScaler? _canvasScaler;
        private GameObject? _expandedPanel;
        private GameObject? _collapsedTab;
        private TextMeshProUGUI? _contentText;
        private DraggablePanel? _expandedDrag;
        private DraggablePanel? _collapsedDrag;
        private readonly System.Collections.Generic.List<Image> _opacityImages = new();
        private static readonly System.Collections.Generic.List<Image> _pendingChevrons = new();
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
                return;
            }
            UIScaleSync.Sync(_canvasScaler);
            SyncOpacity();
            SyncPendingChevrons();

            // Belt-and-suspenders against banked KP that the AddKnowledgePoints
            // postfix never saw (queue populated after points already accrued,
            // mod hot-reload, etc.). Throttled — TrySpendAll early-returns when
            // there's nothing to do, so the cost is one if-check per second.
            _autoSpendCheckTimer += Time.unscaledDeltaTime;
            if (_autoSpendCheckTimer >= 1f)
            {
                _autoSpendCheckTimer = 0f;
                int spent = TechTree.TechAutoQueue.TrySpendAll();
                if (spent > 0) RefreshDisplay();
            }
        }

        private float _autoSpendCheckTimer;

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

            // Scene-aware hide — see PinnedResourceOverlay.BuildUI for the
            // full rationale. Belt-and-suspenders with Plugin.OnSceneWasInitialized.
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            ApplySceneVisibility(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            _canvasScaler = _canvasRoot.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            UIScaleSync.Sync(_canvasScaler);

            BuildExpandedPanel();
            BuildCollapsedTab();
            WireDragHandles();
            ApplyCollapsed();
        }

        private void WireDragHandles()
        {
            if (_expandedPanel == null || _collapsedTab == null) return;

            var defaultPos = new Vector2(0.005f, 0.78f);
            var savedPos = new Vector2(
                FFUIOverhaulMod.TechQueueOverlayPosX?.Value ?? defaultPos.x,
                FFUIOverhaulMod.TechQueueOverlayPosY?.Value ?? defaultPos.y);

            // Drag from anywhere on the panel — see PinnedResourceOverlay for
            // rationale (header-only handle could hide behind the top bar).
            _expandedDrag = _expandedPanel.AddComponent<DraggablePanel>();
            _expandedDrag.Target = (RectTransform)_expandedPanel.transform;
            _expandedDrag.DefaultNormalizedPosition = defaultPos;
            _expandedDrag.OnPositionChanged = SavePosition;
            _expandedDrag.ApplyNormalized(savedPos, persist: false);

            _collapsedDrag = _collapsedTab.AddComponent<DraggablePanel>();
            _collapsedDrag.Target = (RectTransform)_collapsedTab.transform;
            _collapsedDrag.DefaultNormalizedPosition = defaultPos;
            _collapsedDrag.OnPositionChanged = SavePosition;
            _collapsedDrag.ApplyNormalized(savedPos, persist: false);
        }

        private static void SavePosition(Vector2 normalized)
        {
            if (FFUIOverhaulMod.TechQueueOverlayPosX == null || FFUIOverhaulMod.TechQueueOverlayPosY == null) return;
            FFUIOverhaulMod.TechQueueOverlayPosX.Value = normalized.x;
            FFUIOverhaulMod.TechQueueOverlayPosY.Value = normalized.y;
            MelonLoader.MelonPreferences.Save();
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

            ApplyFFChrome(_expandedPanel, PanelBg.a);

            var vlg = _expandedPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 6);
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fit = _expandedPanel.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header row — also serves as the drag handle. The Image is the
            // graphic that makes pointer events hit the header (without it
            // drag falls through to the canvas). Subtle gold tint signals
            // it's interactive without being loud.
            var header = NewChild(_expandedPanel, "Header");
            header.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;
            AddImage(header, new Color(0.83f, 0.63f, 0.19f, 0.10f));
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 4, 2, 2);
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var headerLabel = NewText(header, "HeaderLabel", "TECH QUEUE", 14, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
            if (FFNativeAssets.FontTitle != null) headerLabel.font = FFNativeAssets.FontTitle;
            headerLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Collapse button — chevron pointing LEFT, since this panel
            // collapses toward the left edge of the screen.
            NewArrowButton(header, "CollapseBtn", 180f, 22, ToggleCollapse);

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

            var img = ApplyFFChrome(_collapsedTab, PanelBg.a);
            var btn = _collapsedTab.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleCollapse);

            // Rotate the label -90° so "TECH" reads top-to-bottom along the
            // narrow tab. Pre-rotation rect is wide-and-short (TabHeight × TabWidth);
            // after rotation it visually occupies the tall-and-narrow tab.
            var label = NewText(_collapsedTab, "Label", "TECH", 11, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.Center);
            if (FFNativeAssets.FontTitle != null) label.font = FFNativeAssets.FontTitle;
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

            // Re-apply saved position to whichever side is now visible — see
            // PinnedResourceOverlay.ApplyCollapsedState for the full rationale.
            if (FFUIOverhaulMod.TechQueueOverlayPosX != null && FFUIOverhaulMod.TechQueueOverlayPosY != null)
            {
                var pos = new Vector2(FFUIOverhaulMod.TechQueueOverlayPosX.Value, FFUIOverhaulMod.TechQueueOverlayPosY.Value);
                if (_collapsed) _collapsedDrag?.ApplyNormalized(pos, persist: false);
                else _expandedDrag?.ApplyNormalized(pos, persist: false);
            }
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
            // See PinnedResourceOverlay.NewText for the rationale + the
            // try/catch reason (some TMP font assets NRE on outline set).
            try
            {
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(0, 0, 0, 220);
            }
            catch { }
            return t;
        }

        /// <summary>
        /// Image-backed collapse arrow — see PinnedResourceOverlay.NewArrowButton.
        /// </summary>
        private static GameObject NewArrowButton(GameObject parent, string name, float zRotation, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var bg = AddImage(go, ButtonNormal);
            if (FFNativeAssets.PanelBorderSimple != null)
            {
                bg.sprite = FFNativeAssets.PanelBorderSimple;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(1f, 1f, 1f, 1f);
            }
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.20f, 1.10f, 0.85f, 1f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
            arrowGo.transform.SetParent(go.transform, false);
            var arrt = (RectTransform)arrowGo.transform;
            arrt.anchorMin = new Vector2(0.5f, 0.5f);
            arrt.anchorMax = new Vector2(0.5f, 0.5f);
            arrt.pivot = new Vector2(0.5f, 0.5f);
            arrt.sizeDelta = new Vector2(18, 18);
            arrt.anchoredPosition = Vector2.zero;
            arrt.localRotation = Quaternion.Euler(0, 0, zRotation);
            var arrowImg = arrowGo.GetComponent<Image>();
            arrowImg.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            arrowImg.raycastTarget = false;
            if (FFNativeAssets.ArrowChevron != null)
                arrowImg.sprite = FFNativeAssets.ArrowChevron;
            else
                _pendingChevrons.Add(arrowImg);
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

        private static GameObject NewIconButton(GameObject parent, string name, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var img = AddImage(go, ButtonNormal);
            if (FFNativeAssets.PanelBorderSimple != null)
            {
                img.sprite = FFNativeAssets.PanelBorderSimple;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 1f);
            }
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.20f, 1.10f, 0.85f, 1f);
            colors.pressedColor = new Color(0.85f, 0.75f, 0.55f, 1f);
            btn.colors = colors;
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
            _cachedFont = FFNativeAssets.FontBody;
            if (_cachedFont != null) return _cachedFont;
            var all = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene next)
            => ApplySceneVisibility(next);

        private void ApplySceneVisibility(UnityEngine.SceneManagement.Scene scene)
        {
            if (_canvasRoot == null) return;
            string n = scene.name ?? "";
            bool isInGame = n == "Frontier";
            FFUIOverhaulMod.Log.Msg($"[TechQueueOverlay] scene='{n}' → visible={isInGame}");
            _canvasRoot.SetActive(isInGame);
        }

        private Image ApplyFFChrome(GameObject go, float alpha)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            var frame = FFNativeAssets.PanelBorderThick ?? FFNativeAssets.PanelBorderDark;
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, alpha);
            }
            else
            {
                img.color = PanelBg;
            }
            _opacityImages.Add(img);
            return img;
        }

        private static void ApplyGoldGradient(TextMeshProUGUI tmp)
        {
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(
                new Color(1.00f, 0.88f, 0.45f, 1f),
                new Color(1.00f, 0.88f, 0.45f, 1f),
                new Color(0.55f, 0.40f, 0.15f, 1f),
                new Color(0.55f, 0.40f, 0.15f, 1f));
        }

        private void SyncOpacity()
        {
            float wanted = FFUIOverhaulMod.OverlayOpacity?.Value ?? 0.92f;
            wanted = Mathf.Clamp(wanted, 0.05f, 1f);
            for (int i = 0; i < _opacityImages.Count; i++)
            {
                var img = _opacityImages[i];
                if (img == null) continue;
                if (Mathf.Abs(img.color.a - wanted) < 0.005f) continue;
                var c = img.color;
                c.a = wanted;
                img.color = c;
            }
        }
    }
}
