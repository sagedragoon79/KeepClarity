using System;
using System.Collections.Generic;
using System.Linq;
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
        private GameObject? _expandedPanel;
        private GameObject? _collapsedTab;
        private GameObject? _configPanel;
        private RectTransform? _itemsContainer;
        private RectTransform? _configContent;
        private TextMeshProUGUI? _collapseButtonLabel;
        private TextMeshProUGUI? _configButtonLabel;

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
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            var scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            BuildExpandedPanel();
            BuildCollapsedTab();
            BuildConfigPanel();

            ApplyCollapsedState();
            RebuildItemRows();
            RebuildConfigRows();
            RefreshValues();
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

            AddImage(_expandedPanel, PanelBg);

            var vlg = _expandedPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 4);
            vlg.spacing = 0;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = _expandedPanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header row
            var header = NewChild(_expandedPanel, "Header");
            var headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = HeaderHeight;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(6, 4, 2, 2);
            hlg.spacing = 4;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var headerLabel = NewText(header, "HeaderLabel", "PINNED", 11, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.MidlineLeft);
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

            // Collapse button (◀ → tab)
            NewIconButton(header, "CollapseButton", "▶", 22, ToggleCollapse);
            _collapseButtonLabel = header.transform.Find("CollapseButton/Label").GetComponent<TextMeshProUGUI>();

            // Separator
            var sep = NewChild(_expandedPanel, "Separator");
            var sepLE = sep.AddComponent<LayoutElement>();
            sepLE.preferredHeight = 1;
            AddImage(sep, SeparatorColor);

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

            AddImage(_collapsedTab, PanelBg);

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
            var label = NewText(_collapsedTab, "Label", "PIN", 10, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.Center);
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

            AddImage(_configPanel, PanelBg);

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
            var headerLabel = NewText(header, "Label", "PIN RESOURCES", 11, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.MidlineLeft);
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
                hlg.padding = new RectOffset(8, 8, 0, 0);
                hlg.spacing = 4;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = true;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childAlignment = TextAnchor.MiddleLeft;

                var name = NewText(rowGo, "Name", item.DisplayName, 11, FontStyles.Normal,
                    ResourceHelper.GetCategoryColor(item.Category), TextAlignmentOptions.MidlineLeft);
                name.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

                var value = NewText(rowGo, "Value", "0", 11, FontStyles.Bold,
                    new Color(0.75f, 0.72f, 0.60f, 1f), TextAlignmentOptions.MidlineRight);
                value.gameObject.AddComponent<LayoutElement>().preferredWidth = 56;

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
                    var catLabel = NewText(catGo, "Label", GetCategoryLabel(item.Category), 10,
                        FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.MidlineLeft);
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
                    HeaderTextColor, TextAlignmentOptions.Center);
                var checkRt = check.rectTransform;
                checkRt.anchorMin = Vector2.zero;
                checkRt.anchorMax = Vector2.one;
                checkRt.offsetMin = Vector2.zero;
                checkRt.offsetMax = Vector2.zero;
                check.raycastTarget = false;

                var label = NewText(rowGo, "Label", item.DisplayName, 11, FontStyles.Normal,
                    ConfigItemText, TextAlignmentOptions.MidlineLeft);
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

            var text = NewText(go, "Label", label, 10, FontStyles.Bold, HeaderTextColor, TextAlignmentOptions.Center);
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
            return t;
        }

        private static GameObject NewIconButton(GameObject parent, string name, string label, float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            go.AddComponent<LayoutElement>().preferredWidth = width;
            var img = AddImage(go, ButtonNormal);
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(ButtonHover.r / ButtonNormal.r, ButtonHover.g / ButtonNormal.g, ButtonHover.b / ButtonNormal.b, 1f);
            colors.pressedColor = new Color(ButtonPressed.r / ButtonNormal.r, ButtonPressed.g / ButtonNormal.g, ButtonPressed.b / ButtonNormal.b, 1f);
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
            var all = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
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
