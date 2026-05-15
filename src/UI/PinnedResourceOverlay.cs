using System;
using System.Collections.Generic;
using System.Linq;
using FFUIOverhaul.Settings.UI;
using FFUIOverhaul.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// ONI-style pinnable resource overlay, rendered as UGUI on a dedicated Canvas.
    ///
    /// Why not IMGUI:
    ///   - OnGUI runs twice per frame and allocates GC every call.
    ///   - IMGUI always draws above all UGUI canvases (no way to be "below" UI).
    ///   - Aesthetics — IMGUI uses Unity's built-in skin, not the game's TMPro look.
    ///
    /// This implementation:
    ///   - Builds a Canvas with a low sortingOrder so menus/tooltips draw over it.
    ///   - Uses TMPro for text (matches game style) and Image for solid backgrounds.
    ///   - Refreshes count values on a timer (default 0.5s) instead of every frame.
    ///   - Collapses to a thin tab on the screen edge that re-expands on click.
    /// </summary>
    public class PinnedResourceOverlay
    {
        private const float PanelWidth = 180f;
        private const float HeaderHeight = 22f;
        private const float RowHeight = 18f;
        private const float TabWidth = 24f;
        private const float TabHeight = 60f;
        private const int CanvasSortingOrder = 1; // above game world, below typical UI

        // Palette — borrowed from prior IMGUI version; tuned to game's brown/gold.
        private static readonly Color PanelBg = new(0.11f, 0.09f, 0.09f, 0.92f);
        private static readonly Color HeaderTextColor = new(0.83f, 0.63f, 0.19f, 1f);
        private static readonly Color SeparatorColor = new(0.30f, 0.24f, 0.18f, 1f);
        private static readonly Color CategoryBg = new(0.83f, 0.63f, 0.19f, 0.10f);
        private static readonly Color ButtonNormal = new(0.25f, 0.20f, 0.18f, 0.95f);
        private static readonly Color ButtonHover = new(0.35f, 0.28f, 0.20f, 0.95f);
        private static readonly Color ButtonPressed = new(0.45f, 0.36f, 0.24f, 0.95f);
        private static readonly Color ConfigItemText = new(0.63f, 0.53f, 0.38f, 1f);

        public bool ConfigOpen { get; set; } // referenced by Plugin.HandleEscapeKey

        public bool Visible
        {
            get => _canvasRoot != null && _canvasRoot.activeSelf;
            set { if (_canvasRoot != null) _canvasRoot.SetActive(value); }
        }

        private GameObject? _canvasRoot;
        private CanvasScaler? _canvasScaler;
        private GameObject? _expandedPanel;
        private GameObject? _collapsedTab;
        private GameObject? _configPanel;
        private RectTransform? _itemsContainer;
        private RectTransform? _configContent;
        private TextMeshProUGUI? _configButtonLabel;
        private DraggablePanel? _expandedDrag;
        private DraggablePanel? _collapsedDrag;
        private readonly List<Image> _opacityImages = new(); // panels whose alpha tracks OverlayOpacity
        private static readonly List<Image> _pendingChevrons = new(); // arrow images awaiting sprite resolution

        private readonly List<PinnedRow> _rows = new();
        private readonly List<PinnedItem> _pinnedItems = new();
        private bool _initialized;
        private bool _collapsed;
        private float _refreshTimer;
        private static TMP_FontAsset? _cachedFont;

        public PinnedResourceOverlay()
        {
            LoadPinnedItems();
        }

        /// <summary>Call this every frame from MelonMod.OnUpdate.</summary>
        public void Tick()
        {
            if (!_initialized)
            {
                try { BuildUI(); _initialized = true; }
                catch (Exception e)
                {
                    FFUIOverhaulMod.Log.Warning($"[PinnedOverlay] BuildUI failed: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                    _initialized = true; // don't retry forever
                }
                return;
            }

            UIScaleSync.Sync(_canvasScaler);
            SyncOpacity();
            SyncPendingChevrons();

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.5f)
            {
                _refreshTimer = 0;
                RefreshValues();
            }
        }

        // ── Build ──────────────────────────────────────────────────────────

        private void BuildUI()
        {
            _canvasRoot = new GameObject("FFUI_PinnedOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);

            // Scene-aware hide. DontDestroyOnLoad keeps the canvas alive across
            // scene reloads, so without this the overlay would persist on the
            // main menu after Exit-to-Menu. We belt-and-suspender it alongside
            // Plugin.OnSceneWasInitialized — that MelonMod hook fires reliably
            // for the *initial* in-game scene but the activeSceneChanged event
            // fires for every transition (menu↔load↔map), so this catches the
            // edges OnSceneWasInitialized can miss.
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            ApplySceneVisibility(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            _canvasScaler = _canvasRoot.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            // Mirror the game's scaler immediately if available so first-frame
            // sizing matches FF's UI scale instead of momentarily showing at 1×.
            UIScaleSync.Sync(_canvasScaler);

            BuildExpandedPanel();
            BuildCollapsedTab();
            BuildConfigPanel();

            WireDragHandles();
            ApplyCollapsedState();
            RebuildItemRows();
            RebuildConfigRows();
            RefreshValues();
        }

        private void WireDragHandles()
        {
            if (_expandedPanel == null || _collapsedTab == null) return;

            var defaultPos = new Vector2(0.995f, 0.95f);
            var savedPos = new Vector2(
                FFUIOverhaulMod.PinnedOverlayPosX?.Value ?? defaultPos.x,
                FFUIOverhaulMod.PinnedOverlayPosY?.Value ?? defaultPos.y);

            // Expanded: header strip is the handle; whole config panel rides
            // along so it stays glued to the side of the panel as it moves.
            var configPanelRt = _configPanel != null ? (RectTransform)_configPanel.transform : null;
            var configOffset = configPanelRt != null
                ? configPanelRt.anchoredPosition - ((RectTransform)_expandedPanel.transform).anchoredPosition
                : Vector2.zero;

            // Drag from anywhere on the panel, not just the header — when the
            // header was the only handle, dragging the panel under the top bar
            // hid the only grab point and the panel became unreachable.
            // Button clicks still route to buttons (Unity dispatches click and
            // drag events independently — drags walk up parents looking for
            // IDragHandler, clicks fire on the directly-hit object).
            _expandedDrag = _expandedPanel.AddComponent<DraggablePanel>();
            _expandedDrag.Target = (RectTransform)_expandedPanel.transform;
            _expandedDrag.DefaultTarget = configPanelRt;
            _expandedDrag.DefaultTargetOffset = configOffset;
            _expandedDrag.DefaultNormalizedPosition = defaultPos;
            _expandedDrag.OnPositionChanged = SavePosition;
            _expandedDrag.ApplyNormalized(savedPos, persist: false);

            // Collapsed: tab itself is the handle. Tab and panel share the
            // same persisted position so toggling between them stays visually
            // anchored. Tab keeps its existing Button click for un-collapse —
            // drag handlers don't conflict with click handlers.
            _collapsedDrag = _collapsedTab.AddComponent<DraggablePanel>();
            _collapsedDrag.Target = (RectTransform)_collapsedTab.transform;
            _collapsedDrag.DefaultNormalizedPosition = defaultPos;
            _collapsedDrag.OnPositionChanged = SavePosition;
            _collapsedDrag.ApplyNormalized(savedPos, persist: false);
        }

        private static void SavePosition(Vector2 normalized)
        {
            if (FFUIOverhaulMod.PinnedOverlayPosX == null || FFUIOverhaulMod.PinnedOverlayPosY == null) return;
            FFUIOverhaulMod.PinnedOverlayPosX.Value = normalized.x;
            FFUIOverhaulMod.PinnedOverlayPosY.Value = normalized.y;
            MelonLoader.MelonPreferences.Save();
        }

        private void BuildExpandedPanel()
        {
            _expandedPanel = NewChild(_canvasRoot!, "ExpandedPanel");
            var rt = (RectTransform)_expandedPanel.transform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-8, -44);
            rt.sizeDelta = new Vector2(PanelWidth, 100); // height set by ContentSizeFitter

            ApplyFFChrome(_expandedPanel, PanelBg.a);

            var vlg = _expandedPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(4, 4, 4, 6);
            vlg.spacing = 0;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = _expandedPanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header row — also serves as the drag handle. The Image is the
            // graphic that makes pointer events hit the header (without it,
            // drag events fall through to the canvas). Subtle gold tint so
            // the user sees the strip is interactive without it dominating.
            var header = NewChild(_expandedPanel, "Header");
            var headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = HeaderHeight;
            AddImage(header, new Color(0.83f, 0.63f, 0.19f, 0.10f));
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 6, 2, 2);
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            // Header — match FF's modal popup title style (Transfer Items,
            // confirmation dialogs, etc.): NotoSerifJP-Regular SDF, Bold +
            // SmallCaps, size 14. Reads "native" alongside game windows.
            var headerLabel = NewText(header, "HeaderLabel", "PINNED", 14, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
            if (FFNativeAssets.FontTitle != null) headerLabel.font = FFNativeAssets.FontTitle;
            var headerLabelLE = headerLabel.gameObject.AddComponent<LayoutElement>();
            headerLabelLE.flexibleWidth = 1;

            // Config button [+] / [-]
            var configBtn = NewIconButton(header, "ConfigButton", "+", 22, () =>
            {
                ConfigOpen = !ConfigOpen;
                if (_configPanel != null) _configPanel.SetActive(ConfigOpen);
                _configButtonLabel!.text = ConfigOpen ? "−" : "+";
            });
            _configButtonLabel = configBtn.GetComponentInChildren<TextMeshProUGUI>();

            // Collapse button — FF chevron pointing right (panel collapses
            // toward the right edge of the screen, matching the panel's
            // top-right anchor). Sprite is the same one FF uses on its
            // building-info cycle arrows.
            NewArrowButton(header, "CollapseButton", 0f, 22, ToggleCollapse);

            // Separator — thin warm-brown line tinted to match the carved
            // border instead of a bright gold accent (which clashed against
            // the FF panel background).
            var sep = NewChild(_expandedPanel, "Separator");
            var sepLE = sep.AddComponent<LayoutElement>();
            sepLE.preferredHeight = 1;
            AddImage(sep, new Color(0.35f, 0.27f, 0.18f, 0.85f));

            // Items container
            var itemsGo = NewChild(_expandedPanel, "Items");
            _itemsContainer = (RectTransform)itemsGo.transform;
            var itemsVlg = itemsGo.AddComponent<VerticalLayoutGroup>();
            itemsVlg.padding = new RectOffset(0, 0, 2, 2);
            itemsVlg.spacing = 0;
            itemsVlg.childForceExpandWidth = true;
            itemsVlg.childForceExpandHeight = false;
            itemsVlg.childControlWidth = true;
            itemsVlg.childControlHeight = true;
        }

        private void BuildCollapsedTab()
        {
            _collapsedTab = NewChild(_canvasRoot!, "CollapsedTab");
            var rt = (RectTransform)_collapsedTab.transform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-4, -44);
            rt.sizeDelta = new Vector2(TabWidth, TabHeight);

            ApplyFFChrome(_collapsedTab, PanelBg.a);

            // Click anywhere on the tab to expand
            var btn = _collapsedTab.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var img = _collapsedTab.GetComponent<Image>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(ToggleCollapse);

            // Vertical "PINNED" label
            var label = NewText(_collapsedTab, "Label", "PIN", 10, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.Center);
            if (FFNativeAssets.FontTitle != null) label.font = FFNativeAssets.FontTitle;
            var labelRt = label.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            label.raycastTarget = false; // let the tab Button receive the click
        }

        private void BuildConfigPanel()
        {
            _configPanel = NewChild(_canvasRoot!, "ConfigPanel");
            _configPanel.SetActive(false);
            var rt = (RectTransform)_configPanel.transform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-(PanelWidth + 16), -44);
            rt.sizeDelta = new Vector2(220, 440);

            ApplyFFChrome(_configPanel, PanelBg.a);

            // Header — explicit RectTransform anchors instead of a parent layout group
            // so we have full control over scroll positioning below.
            const float headerHeight = 22f;
            var header = NewChild(_configPanel, "Header");
            var hrt = (RectTransform)header.transform;
            hrt.anchorMin = new Vector2(0, 1);
            hrt.anchorMax = new Vector2(1, 1);
            hrt.pivot = new Vector2(0.5f, 1);
            hrt.anchoredPosition = Vector2.zero;
            hrt.sizeDelta = new Vector2(0, headerHeight);
            AddImage(header, new Color(0, 0, 0, 0.25f));
            // 12pt to clear the All / None bulk-action buttons that share the
            // same header strip. "PIN RESOURCES" at 14pt overlapped them.
            var headerLabel = NewText(header, "Label", "PIN RESOURCES", 12, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
            if (FFNativeAssets.FontTitle != null) headerLabel.font = FFNativeAssets.FontTitle;
            var hLabelRt = headerLabel.rectTransform;
            hLabelRt.anchorMin = new Vector2(0, 0);
            hLabelRt.anchorMax = new Vector2(0.55f, 1);
            hLabelRt.offsetMin = new Vector2(8, 0);
            hLabelRt.offsetMax = Vector2.zero;

            // Bulk-action buttons on the right side of the header.
            CreateBulkActionButton(header, "All", -52f, PinAll);
            CreateBulkActionButton(header, "None", -6f, PinNone);

            // Scroll root — fills the rest of the panel below the header.
            var scrollGo = NewChild(_configPanel, "Scroll");
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.pivot = new Vector2(0.5f, 1);
            scrollRt.anchoredPosition = new Vector2(0, -headerHeight);
            scrollRt.sizeDelta = new Vector2(0, -headerHeight - 4);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 16f;

            // Viewport — owns the mask. Standard ScrollRect pattern.
            var viewport = NewChild(scrollGo, "Viewport");
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            // Need a Graphic for Mask to clip; RectMask2D is the modern lightweight option
            // and doesn't require a graphic. Add a transparent image so raycasts hit.
            var vpImg = AddImage(viewport, new Color(0, 0, 0, 0.001f));
            vpImg.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = vrt;

            // Content — top-anchored, stretches horizontally, height driven by ContentSizeFitter.
            var content = NewChild(viewport, "Content");
            _configContent = (RectTransform)content.transform;
            _configContent.anchorMin = new Vector2(0, 1);
            _configContent.anchorMax = new Vector2(1, 1);
            _configContent.pivot = new Vector2(0.5f, 1);
            _configContent.anchoredPosition = Vector2.zero;
            _configContent.sizeDelta = new Vector2(0, 0); // height fills via fitter; width = viewport
            scroll.content = _configContent;

            var cvlg = content.AddComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(0, 0, 2, 2);
            cvlg.spacing = 0;
            cvlg.childAlignment = TextAnchor.UpperCenter;
            cvlg.childForceExpandWidth = true;
            cvlg.childForceExpandHeight = false;
            cvlg.childControlWidth = true;
            cvlg.childControlHeight = true;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        // ── Rows ───────────────────────────────────────────────────────────

        private void RebuildItemRows()
        {
            if (_itemsContainer == null) return;

            // DestroyImmediate (not Destroy) so old rows are gone before the next
            // layout pass — otherwise the panel briefly sizes to old+new and the
            // ContentSizeFitter can leave permanent slack at the top.
            foreach (var row in _rows)
                if (row.Root != null) UnityEngine.Object.DestroyImmediate(row.Root);
            // Also kill any leftover separator children from previous builds.
            for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(_itemsContainer.GetChild(i).gameObject);
            _rows.Clear();

            // Group by category for display. OrderBy is stable so insertion order
            // is preserved within a category — toggling a new pin appends it to the
            // end of its group rather than reshuffling the list.
            var sorted = _pinnedItems.OrderBy(p => p.Category).ToList();

            ResourceCategory? lastCat = null;
            foreach (var item in sorted)
            {
                if (lastCat.HasValue && item.Category != lastCat.Value)
                {
                    var sep = NewChild(_itemsContainer.gameObject, "CatSep");
                    sep.AddComponent<LayoutElement>().preferredHeight = 2;
                    AddImage(sep, SeparatorColor);
                }
                lastCat = item.Category;

                var rowGo = NewChild(_itemsContainer.gameObject, $"Row_{item.ItemId}");
                rowGo.AddComponent<LayoutElement>().preferredHeight = RowHeight;
                var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                // Tightened right padding + value column width to close the
                // visible gap between item name and count.
                hlg.padding = new RectOffset(8, 4, 0, 0);
                hlg.spacing = 2;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childAlignment = TextAnchor.MiddleLeft;

                // Resource name — flexes to fill remaining width. The name
                // column auto-sizes to whatever's left after we reserve the
                // value column on the right.
                var name = NewText(rowGo, "Name", item.DisplayName, 13, FontStyles.Normal,
                    ResourceHelper.GetCategoryColor(item.Category), TextAlignmentOptions.MidlineLeft);
                if (FFNativeAssets.FontHeader != null) name.font = FFNativeAssets.FontHeader;
                name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                name.overflowMode = TextOverflowModes.Truncate;

                // Value — width sized for "11321/15000" (11 chars) at 13pt. If
                // a player's quota exceeds that, the digits truncate gracefully.
                var value = NewText(rowGo, "Value", "0", 13, FontStyles.Normal,
                    new Color(0.75f, 0.72f, 0.60f, 1f), TextAlignmentOptions.MidlineRight);
                if (FFNativeAssets.FontHeader != null) value.font = FFNativeAssets.FontHeader;
                value.gameObject.AddComponent<LayoutElement>().preferredWidth = 72;

                _rows.Add(new PinnedRow { Root = rowGo, NameText = name, ValueText = value, Item = item });
            }

            // Force a layout pass now so the panel's ContentSizeFitter shrinks/grows
            // to fit the new row count this frame (otherwise we'd see one frame of
            // stale sizing).
            if (_expandedPanel != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_expandedPanel.transform as RectTransform);
        }

        private void RebuildConfigRows()
        {
            if (_configContent == null) return;

            for (int i = _configContent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(_configContent.GetChild(i).gameObject);

            var all = GetAllPinnableItems();
            ResourceCategory? lastCat = null;
            foreach (var item in all)
            {
                if (item.Category != lastCat)
                {
                    var catGo = NewChild(_configContent.gameObject, "Cat");
                    catGo.AddComponent<LayoutElement>().preferredHeight = 18;
                    AddImage(catGo, CategoryBg);
                    var catLabel = NewText(catGo, "Label", GetCategoryLabel(item.Category), 12,
                        FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
                    if (FFNativeAssets.FontTitle != null) catLabel.font = FFNativeAssets.FontTitle;
                    var crt = catLabel.rectTransform;
                    crt.anchorMin = Vector2.zero;
                    crt.anchorMax = Vector2.one;
                    crt.offsetMin = new Vector2(6, 0);
                    crt.offsetMax = Vector2.zero;
                    lastCat = item.Category;
                }

                bool pinned = _pinnedItems.Any(p => p.ItemId == item.ItemId);
                var rowGo = NewChild(_configContent.gameObject, $"CfgRow_{item.ItemId}");
                rowGo.AddComponent<LayoutElement>().preferredHeight = 18;

                // Whole-row clickable button (transparent bg)
                var bg = AddImage(rowGo, new Color(0, 0, 0, 0.001f));
                var btn = rowGo.AddComponent<Button>();
                btn.targetGraphic = bg;
                var capturedItem = item;
                btn.onClick.AddListener(() => TogglePin(capturedItem));

                const float padLeft = 6f;
                const float boxSize = 12f;
                const float gap = 6f;

                // Real bordered checkbox: outer Image is the border color (matches
                // label font), inner Image insets 1px to make it look like a frame,
                // then the ✓ text floats on top centered.
                var boxGo = NewChild(rowGo, "Box");
                var boxRt = (RectTransform)boxGo.transform;
                boxRt.anchorMin = new Vector2(0, 0.5f);
                boxRt.anchorMax = new Vector2(0, 0.5f);
                boxRt.pivot = new Vector2(0, 0.5f);
                boxRt.anchoredPosition = new Vector2(padLeft, 0);
                boxRt.sizeDelta = new Vector2(boxSize, boxSize);
                var borderImg = boxGo.AddComponent<Image>();
                borderImg.color = ConfigItemText; // border = font color
                borderImg.raycastTarget = false;

                var innerGo = NewChild(boxGo, "Inner");
                var innerRt = (RectTransform)innerGo.transform;
                innerRt.anchorMin = Vector2.zero;
                innerRt.anchorMax = Vector2.one;
                innerRt.offsetMin = new Vector2(1, 1);
                innerRt.offsetMax = new Vector2(-1, -1);
                var innerImg = innerGo.AddComponent<Image>();
                innerImg.color = PanelBg; // matches panel background to look like a hollow frame
                innerImg.raycastTarget = false;

                var check = NewText(boxGo, "Check", pinned ? "✓" : "", 13, FontStyles.Bold,
                    Color.white, TextAlignmentOptions.Center);
                var checkRt = check.rectTransform;
                checkRt.anchorMin = Vector2.zero;
                checkRt.anchorMax = Vector2.one;
                checkRt.offsetMin = Vector2.zero;
                checkRt.offsetMax = Vector2.zero;
                check.raycastTarget = false;

                // Config row label — header font tinted to its category color
                // so the user can scan by color while picking pins.
                var label = NewText(rowGo, "Label", item.DisplayName, 12, FontStyles.Normal,
                    ResourceHelper.GetCategoryColor(item.Category), TextAlignmentOptions.MidlineLeft);
                if (FFNativeAssets.FontHeader != null) label.font = FFNativeAssets.FontHeader;
                var lrt = label.rectTransform;
                lrt.anchorMin = new Vector2(0, 0);
                lrt.anchorMax = new Vector2(1, 1);
                lrt.pivot = new Vector2(0, 0.5f);
                lrt.offsetMin = new Vector2(padLeft + boxSize + gap, 0);
                lrt.offsetMax = new Vector2(-6, 0);
                label.raycastTarget = false;
            }
        }

        // ── Update ─────────────────────────────────────────────────────────

        private void RefreshValues()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm?.resourceManager == null) return;

            foreach (var row in _rows)
            {
                int stored = 0;
                bool critical = false;
                string display = "0";

                var info = gm.resourceManager.GetItemInfo(row.Item.ItemId);
                if (info != null)
                {
                    stored = (int)info.unusedCount;
                    critical = stored == 0 && row.Item.Category != ResourceCategory.Livestock;

                    // Settlement-wide max quota shows as "X/Y"; "∞" if no limit
                    // is set (Resources menu → "Limit Production" off, or maxQuota
                    // not configured for this item).
                    bool hasLimit = info.areProductionLimitsEnabled && info.maxQuota > 0;
                    display = hasLimit
                        ? $"{stored}/{info.maxQuota}"
                        : $"{stored}/∞";
                }

                row.ValueText.text = display;
                row.NameText.color = critical ? ResourceHelper.CriticalColor : ResourceHelper.GetCategoryColor(row.Item.Category);
                row.ValueText.color = critical ? ResourceHelper.CriticalColor : new Color(0.75f, 0.72f, 0.60f, 1f);
            }
        }

        /// <summary>
        /// Small "All" / "None" action button rendered in the config panel
        /// header. Anchored to the right edge with the supplied offset (negative
        /// X = inset from right). Click runs the action immediately.
        /// </summary>
        private void CreateBulkActionButton(GameObject parent, string label, float xOffset, System.Action onClick)
        {
            var go = NewChild(parent, $"BulkBtn_{label}");
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, 0);
            rt.sizeDelta = new Vector2(42, 16);

            var img = AddImage(go, new Color(0.25f, 0.20f, 0.18f, 0.9f));
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.4f, 1.2f, 0.8f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick());

            var text = NewText(go, "Label", label, 10, FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.Center);
            if (FFNativeAssets.FontTitle != null) text.font = FFNativeAssets.FontTitle;
            var trt = text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }

        private void PinAll()
        {
            _pinnedItems.Clear();
            foreach (var item in GetAllPinnableItems())
                _pinnedItems.Add(item);
            SavePinnedItems();
            RebuildItemRows();
            RebuildConfigRows();
            RefreshValues();
        }

        private void PinNone()
        {
            _pinnedItems.Clear();
            SavePinnedItems();
            RebuildItemRows();
            RebuildConfigRows();
            RefreshValues();
        }

        // ── State ──────────────────────────────────────────────────────────

        private void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            ApplyCollapsedState();
            if (FFUIOverhaulMod.PinnedCollapsed != null)
                FFUIOverhaulMod.PinnedCollapsed.Value = _collapsed;
            MelonLoader.MelonPreferences.Save();
        }

        private void ApplyCollapsedState()
        {
            // Read persisted state on first build
            if (FFUIOverhaulMod.PinnedCollapsed != null && _expandedPanel != null && _collapsedTab != null)
            {
                if (!_initialized) _collapsed = FFUIOverhaulMod.PinnedCollapsed.Value;
            }
            if (_expandedPanel != null) _expandedPanel.SetActive(!_collapsed);
            if (_collapsedTab != null) _collapsedTab.SetActive(_collapsed);

            // Re-apply the saved position to whichever panel is now visible.
            // Without this, dragging while collapsed updates only the tab; on
            // re-expand the expanded panel still has its old anchoredPosition
            // and snaps back to where it was last shown.
            if (FFUIOverhaulMod.PinnedOverlayPosX != null && FFUIOverhaulMod.PinnedOverlayPosY != null)
            {
                var pos = new Vector2(FFUIOverhaulMod.PinnedOverlayPosX.Value, FFUIOverhaulMod.PinnedOverlayPosY.Value);
                if (_collapsed) _collapsedDrag?.ApplyNormalized(pos, persist: false);
                else _expandedDrag?.ApplyNormalized(pos, persist: false);
            }

            // When collapsed, the config panel must close too
            if (_collapsed && _configPanel != null && ConfigOpen)
            {
                ConfigOpen = false;
                _configPanel.SetActive(false);
                if (_configButtonLabel != null) _configButtonLabel.text = "+";
            }
        }

        private void TogglePin(PinnedItem item)
        {
            if (_pinnedItems.Any(p => p.ItemId == item.ItemId))
                _pinnedItems.RemoveAll(p => p.ItemId == item.ItemId);
            else
                _pinnedItems.Add(item);
            SavePinnedItems();
            RebuildItemRows();
            RebuildConfigRows();
            RefreshValues();
        }

        // ── Pinnable item catalog ──────────────────────────────────────────

        private static string GetCategoryLabel(ResourceCategory cat) => cat switch
        {
            ResourceCategory.Food => "FOOD & CONSUMABLES",
            ResourceCategory.RawMaterial => "RAW MATERIALS",
            ResourceCategory.Produced => "PRODUCED",
            ResourceCategory.Usable => "USABLE",
            ResourceCategory.Livestock => "LIVESTOCK",
            _ => "OTHER"
        };

        private List<PinnedItem> GetAllPinnableItems()
        {
            var items = new List<PinnedItem>();

            // Food
            AddItem(items, "ItemBerries", "Berries", ResourceCategory.Food);
            AddItem(items, "ItemMeat", "Meat", ResourceCategory.Food);
            AddItem(items, "ItemFish", "Fish", ResourceCategory.Food);
            AddItem(items, "ItemSmokedMeat", "Smoked Meat", ResourceCategory.Food);
            AddItem(items, "ItemSmokedFish", "Smoked Fish", ResourceCategory.Food);
            AddItem(items, "ItemPreserves", "Preserves", ResourceCategory.Food);
            AddItem(items, "ItemPreservedVegetables", "Preserved Veg.", ResourceCategory.Food);
            AddItem(items, "ItemGreens", "Greens", ResourceCategory.Food);
            AddItem(items, "ItemRootVegetables", "Root Vegetables", ResourceCategory.Food);
            AddItem(items, "ItemBread", "Bread", ResourceCategory.Food);
            AddItem(items, "ItemMushrooms", "Mushrooms", ResourceCategory.Food);
            AddItem(items, "ItemFruit", "Fruit", ResourceCategory.Food);
            AddItem(items, "ItemNuts", "Nuts", ResourceCategory.Food);
            AddItem(items, "ItemEggs", "Eggs", ResourceCategory.Food);
            AddItem(items, "ItemBeans", "Beans", ResourceCategory.Food);
            AddItem(items, "ItemMilk", "Milk", ResourceCategory.Food);
            AddItem(items, "ItemCheese", "Cheese", ResourceCategory.Food);
            AddItem(items, "ItemPastries", "Pastries", ResourceCategory.Food);
            AddItem(items, "ItemMedicine", "Medicine", ResourceCategory.Food);

            // Raw Materials
            AddItem(items, "ItemLogs", "Logs", ResourceCategory.RawMaterial);
            AddItem(items, "ItemMedicinalRoots", "Medicinal Roots", ResourceCategory.RawMaterial);
            AddItem(items, "ItemHerbs", "Herbs", ResourceCategory.RawMaterial);
            AddItem(items, "ItemWillow", "Willow", ResourceCategory.RawMaterial);
            AddItem(items, "ItemStone", "Stone", ResourceCategory.RawMaterial);
            AddItem(items, "ItemGrain", "Grain", ResourceCategory.RawMaterial);
            AddItem(items, "ItemWater", "Water", ResourceCategory.RawMaterial);
            AddItem(items, "ItemIronOre", "Iron Ore", ResourceCategory.RawMaterial);
            AddItem(items, "ItemGoldOre", "Gold Ore", ResourceCategory.RawMaterial);
            AddItem(items, "ItemCoal", "Coal", ResourceCategory.RawMaterial);
            AddItem(items, "ItemFlax", "Flax", ResourceCategory.RawMaterial);
            AddItem(items, "ItemClay", "Clay", ResourceCategory.RawMaterial);
            AddItem(items, "ItemHoney", "Honey", ResourceCategory.RawMaterial);
            AddItem(items, "ItemWax", "Wax", ResourceCategory.RawMaterial);
            AddItem(items, "ItemSand", "Sand", ResourceCategory.RawMaterial);
            AddItem(items, "ItemHay", "Hay", ResourceCategory.RawMaterial);

            // Produced
            AddItem(items, "ItemFirewood", "Firewood", ResourceCategory.Produced);
            AddItem(items, "ItemWoodPlanks", "Wood Planks", ResourceCategory.Produced);
            AddItem(items, "ItemPelts", "Pelts", ResourceCategory.Produced);
            AddItem(items, "ItemTallow", "Tallow", ResourceCategory.Produced);
            AddItem(items, "ItemFlour", "Flour", ResourceCategory.Produced);
            AddItem(items, "ItemIron", "Iron", ResourceCategory.Produced);
            AddItem(items, "ItemBrick", "Brick", ResourceCategory.Produced);
            AddItem(items, "ItemGoldIngot", "Gold Ingots", ResourceCategory.Produced);
            AddItem(items, "ItemPaper", "Paper", ResourceCategory.Produced);

            // Usable
            AddItem(items, "ItemHeavyTools", "Heavy Tools", ResourceCategory.Usable);
            AddItem(items, "ItemClothing", "Clothing", ResourceCategory.Usable);
            AddItem(items, "ItemShoes", "Shoes", ResourceCategory.Usable);
            AddItem(items, "ItemCandles", "Candles", ResourceCategory.Usable);
            AddItem(items, "ItemSoap", "Soap", ResourceCategory.Usable);
            AddItem(items, "ItemBeer", "Beer", ResourceCategory.Usable);
            AddItem(items, "ItemGlass", "Glass", ResourceCategory.Usable);
            AddItem(items, "ItemLinen", "Linen", ResourceCategory.Usable);
            AddItem(items, "ItemLeather", "Leather", ResourceCategory.Usable);
            AddItem(items, "ItemWeapons", "Weapons", ResourceCategory.Usable);
            AddItem(items, "ItemArmor", "Armor", ResourceCategory.Usable);

            // Livestock
            AddItem(items, "ItemCattle", "Cattle", ResourceCategory.Livestock);
            AddItem(items, "ItemGoat", "Goats", ResourceCategory.Livestock);
            AddItem(items, "ItemChicken", "Chickens", ResourceCategory.Livestock);

            return items;
        }

        private static void AddItem(List<PinnedItem> list, string itemId, string displayName, ResourceCategory cat)
            => list.Add(new PinnedItem { ItemId = itemId, DisplayName = displayName, Category = cat });

        // ── Persistence ────────────────────────────────────────────────────

        private void LoadPinnedItems()
        {
            _pinnedItems.Clear();
            string json = FFUIOverhaulMod.PinnedResourcesJson?.Value ?? "";
            if (string.IsNullOrEmpty(json))
            {
                _pinnedItems.Add(new PinnedItem { ItemId = "ItemLogs", DisplayName = "Logs", Category = ResourceCategory.RawMaterial });
                _pinnedItems.Add(new PinnedItem { ItemId = "ItemFirewood", DisplayName = "Firewood", Category = ResourceCategory.Produced });
                _pinnedItems.Add(new PinnedItem { ItemId = "ItemHeavyTools", DisplayName = "Heavy Tools", Category = ResourceCategory.Usable });
                _pinnedItems.Add(new PinnedItem { ItemId = "ItemClothing", DisplayName = "Clothing", Category = ResourceCategory.Usable });
                return;
            }

            bool dirty = false;
            foreach (var entry in json.Split(';'))
            {
                var parts = entry.Split(':');
                if (parts.Length >= 3)
                {
                    // Drop the legacy "Food_Total" pin — it never resolved to a real
                    // item count and the top bar already shows total food / months.
                    if (parts[0] == "Food_Total") { dirty = true; continue; }

                    Enum.TryParse<ResourceCategory>(parts[2], out var cat);
                    _pinnedItems.Add(new PinnedItem
                    {
                        ItemId = parts[0],
                        DisplayName = parts[1],
                        Category = cat
                    });
                }
            }
            if (dirty) SavePinnedItems();
        }

        private void SavePinnedItems()
        {
            var entries = _pinnedItems.Select(p => $"{p.ItemId}:{p.DisplayName}:{p.Category}");
            string json = string.Join(";", entries);
            if (FFUIOverhaulMod.PinnedResourcesJson != null)
            {
                FFUIOverhaulMod.PinnedResourcesJson.Value = json;
                MelonLoader.MelonPreferences.Save();
            }
        }

        // ── UGUI helpers ───────────────────────────────────────────────────

        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, worldPositionStays: false);
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
            // Thin dark outline so text reads against any background — even
            // a low-opacity panel chrome or the world bleeding through. TMP's
            // SetOutlineThickness pokes the shared font material; if the
            // resolved font lacks an outline channel (some legacy / non-SDF
            // TMP assets), the setter NREs. Try/catch keeps the panel build
            // alive if it does — the outline is purely a polish.
            try
            {
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(0, 0, 0, 220);
            }
            catch { }
            return t;
        }

        /// <summary>
        /// Like <see cref="NewIconButton"/> but renders an Image (the FF
        /// chevron sprite) inside the button instead of a text glyph. Used
        /// for collapse / expand affordances so they match FF's native arrow
        /// styling on info-window cycle buttons.
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

            // Chevron child — fills the button's interior. FF's chevron is a
            // 20×20 sprite with the visible glyph centered; render at 18×18
            // (full button minus 2px each side) so the glyph reads at panel
            // resolution. preserveAspect would shrink the visual further on
            // non-square buttons, so we let it fill the rect.
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
            arrowImg.color = new Color(0.95f, 0.85f, 0.55f, 1f); // warm amber
            arrowImg.raycastTarget = false;
            // Try to assign now; if the sprite probe hasn't found it yet
            // (BuildingInfo prefab not loaded), register for retry — Tick
            // re-tries each frame until the sprite resolves, then drops it
            // from the pending list.
            if (FFNativeAssets.ArrowChevron != null)
                arrowImg.sprite = FFNativeAssets.ArrowChevron;
            else
                _pendingChevrons.Add(arrowImg);
            return go;
        }

        /// <summary>
        /// Re-resolve chevron sprites on collapse buttons that were built
        /// before FF's BuildingInfo prefab was loaded. Cheap — runs once on
        /// first availability, then the list is empty for the rest of the run.
        /// </summary>
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
            // FF uses IMG_BorderSimpleDark01 (sliced 5,5,5,5) for its in-panel
            // 22×22 increment/decrement buttons — same form factor as ours.
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

            var labelText = NewText(go, "Label", label, 12, FontStyles.Bold, new Color(0.83f, 0.63f, 0.19f, 1f), TextAlignmentOptions.Center);
            var lrt = labelText.rectTransform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            return go;
        }

        private static TMP_FontAsset? GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            // Prefer the body font FF uses inside its info panels (matches the
            // visual weight of native text). Fall back to whatever TMP font is
            // already loaded if the asset probe hasn't found it yet.
            _cachedFont = FFNativeAssets.FontBody;
            if (_cachedFont != null) return _cachedFont;
            var all = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }

        /// <summary>
        /// Apply FF's dark sliced panel chrome to a GameObject, keeping the
        /// caller's see-through alpha so the overlay reads as "FF panel" but
        /// stays nearly transparent over the world. Falls back to a flat tint
        /// if the sprite isn't loaded yet (rare — happens only on the very
        /// first frame after fresh boot).
        /// </summary>
        private void OnSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene next)
            => ApplySceneVisibility(next);

        private void ApplySceneVisibility(UnityEngine.SceneManagement.Scene scene)
        {
            if (_canvasRoot == null) return;
            // FF's gameplay scene is "Frontier" (confirmed via earlier log).
            // Anywhere else (main menu, loading splashes, credits, etc.) the
            // overlay must hide. The earlier "menu allowlist" approach
            // missed the actual menu scene name and let panels leak.
            string n = scene.name ?? "";
            bool isInGame = n == "Frontier";
            FFUIOverhaulMod.Log.Msg($"[PinnedOverlay] scene='{n}' → visible={isInGame}");
            _canvasRoot.SetActive(isInGame);
        }

        /// <summary>
        /// Apply FF's clean square panel frame. Earlier attempts to use the
        /// carved BuildingInfo sprite (with mirrored corners for symmetry)
        /// pushed the carved decoration into header text and arrows; users
        /// preferred a simple symmetric frame.
        /// </summary>
        private Image ApplyFFChrome(GameObject go, float alpha)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            // Layered look: the dark inner background is the dropdown frame
            // (uniform dark fill), and we sit on top of it. Single Image is
            // fine — IMG_BorderSimpleThick01 carries both the frame and a
            // dark see-through interior in one sprite.
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

        /// <summary>
        /// Apply a vertical gold gradient matching FF's title-bar text style
        /// (bright top, deep bottom). Cheap visual upgrade vs. a flat color.
        /// </summary>
        private static void ApplyGoldGradient(TextMeshProUGUI tmp)
        {
            tmp.enableVertexGradient = true;
            tmp.colorGradient = new VertexGradient(
                new Color(1.00f, 0.88f, 0.45f, 1f),  // bright top
                new Color(1.00f, 0.88f, 0.45f, 1f),
                new Color(0.55f, 0.40f, 0.15f, 1f),  // dark bottom
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

        private class PinnedRow
        {
            public GameObject Root = null!;
            public TextMeshProUGUI NameText = null!;
            public TextMeshProUGUI ValueText = null!;
            public PinnedItem Item = null!;
        }
    }

    public class PinnedItem
    {
        public string ItemId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public ResourceCategory Category { get; set; }
    }
}
