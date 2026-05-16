using System.Collections.Generic;
using FFUIOverhaul.Settings.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Per-company popup overlay. Shows each soldier as a single horizontal
    /// row: HP-bar background + class icon + name + (future: activity text,
    /// status icons). Click a row to select that soldier; double-click to
    /// center the camera on them. The company's banner sprite at the top of
    /// the panel is the drag handle.
    ///
    /// Multiple instances allowed — one panel per company is intended so a
    /// player can monitor several skirmishes at once (large-battle use case).
    /// Opened via Shift+LClick on the bottom-of-screen banner button; toggle
    /// closed by Shift+LClick again.
    ///
    /// Auto-closes when the company has 0 soldiers (dies entirely) so dead
    /// companies don't litter the screen.
    /// </summary>
    internal class CompanyOverlay
    {
        private readonly MilitaryCompany _company;
        private GameObject? _canvasRoot;
        private CanvasScaler? _canvasScaler;
        private GameObject? _panel;
        private GameObject? _rowsContainer;
        private DraggablePanel? _drag;
        private readonly List<SoldierRow> _rows = new();
        private float _refreshTimer;
        private bool _initialized;

        public bool IsValid =>
            _company != null && !_company.Equals(null)
            && (_company.divisions?.Count ?? 0) > 0
            && AnySoldier();

        private bool AnySoldier()
        {
            foreach (var div in _company.divisions)
                if (div?.soldiers != null && div.soldiers.Count > 0) return true;
            return false;
        }

        public CompanyOverlay(MilitaryCompany company) { _company = company; }

        public void Show()
        {
            if (!_initialized)
            {
                try { Build(); _initialized = true; }
                catch (System.Exception e)
                {
                    FFUIOverhaulMod.Log.Warning($"[CompanyOverlay] Build failed: {e.Message}\n{e.StackTrace}");
                    _initialized = true;
                    return;
                }
            }
            if (_canvasRoot != null) _canvasRoot.SetActive(true);
        }

        public void Hide()
        {
            if (_canvasRoot != null) _canvasRoot.SetActive(false);
        }

        public bool Visible => _canvasRoot != null && _canvasRoot.activeSelf;

        public void Destroy()
        {
            if (_canvasRoot != null) Object.Destroy(_canvasRoot);
            _canvasRoot = null;
            _rows.Clear();
        }

        /// <summary>Per-frame tick from the manager. Cheap when hidden.</summary>
        public void Tick()
        {
            if (!_initialized || _canvasRoot == null || !_canvasRoot.activeSelf) return;
            UIScaleSync.Sync(_canvasScaler);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.25f)
            {
                _refreshTimer = 0f;
                Refresh();
            }
        }

        // ── Build ──────────────────────────────────────────────────────────

        private void Build()
        {
            _canvasRoot = new GameObject($"FFUI_CompanyOverlay_{_company.name}",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(_canvasRoot);
            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2;
            _canvasScaler = _canvasRoot.GetComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            UIScaleSync.Sync(_canvasScaler);

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            ApplySceneVisibility(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            BuildPanel();
            BuildRows();
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene _, UnityEngine.SceneManagement.Scene next)
            => ApplySceneVisibility(next);

        private void ApplySceneVisibility(UnityEngine.SceneManagement.Scene scene)
        {
            if (_canvasRoot == null) return;
            _canvasRoot.SetActive(scene.name == "Frontier");
        }

        private void BuildPanel()
        {
            const float PanelWidth = 260f;
            const float HeaderHeight = 32f;

            _panel = NewChild(_canvasRoot!, "Panel");
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(PanelWidth, 100f);
            rt.anchoredPosition = ReadSavedAnchor();

            // Panel chrome — FF's dark frame, slight transparency that the
            // OverlayOpacity slider can tune. Stays in sync with the pinned
            // overlay's opacity via the same SyncOpacity helper indirectly:
            // for v1 we just set a fixed alpha here; live opacity sync can
            // be added later if the user asks.
            var bgImg = _panel.AddComponent<Image>();
            var frame = FFNativeAssets.PanelBorderThick ?? FFNativeAssets.PanelBorderDark;
            if (frame != null) { bgImg.sprite = frame; bgImg.type = Image.Type.Sliced; }
            float a = FFUIOverhaulMod.OverlayOpacity?.Value ?? 0.92f;
            bgImg.color = new Color(1f, 1f, 1f, a);

            var vlg = _panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = _panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Header row: small banner (left) + company name (right), in a
            // single HorizontalLayoutGroup so the footprint stays compact.
            // Banner aspect is roughly 77:175 (wide:tall) per the dump, so at
            // 32px tall it's ~14px wide. The banner is also the drag handle.
            var header = NewChild(_panel, "Header");
            header.AddComponent<LayoutElement>().preferredHeight = HeaderHeight;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(2, 2, 0, 0);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var bannerGo = NewChild(header, "Banner");
            var bannerLE = bannerGo.AddComponent<LayoutElement>();
            bannerLE.preferredWidth = HeaderHeight * (77f / 175f); // proportional
            bannerLE.preferredHeight = HeaderHeight;
            var bannerImg = bannerGo.AddComponent<Image>();
            bannerImg.sprite = _company.bannerSprite;
            bannerImg.preserveAspect = true;
            bannerImg.raycastTarget = true;
            _drag = bannerGo.AddComponent<DraggablePanel>();
            _drag.Target = (RectTransform)_panel.transform;
            _drag.DefaultNormalizedPosition = new Vector2(0.5f, 0.7f);
            _drag.OnPositionChanged = SavePosition;

            var nameLabel = NewText(header, "CompanyName", _company.displayName, 17,
                FontStyles.Bold | FontStyles.SmallCaps, Color.white, TextAlignmentOptions.MidlineLeft);
            nameLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;

            // Container for soldier rows
            _rowsContainer = NewChild(_panel, "Rows");
            var rvlg = _rowsContainer.AddComponent<VerticalLayoutGroup>();
            rvlg.padding = new RectOffset(0, 0, 4, 0);
            rvlg.spacing = 2;
            rvlg.childForceExpandWidth = true;
            rvlg.childControlWidth = true;
            rvlg.childControlHeight = true;
        }

        // ── Rows ───────────────────────────────────────────────────────────

        private void BuildRows()
        {
            if (_rowsContainer == null) return;
            _rows.Clear();

            // Group: iterate divisions in order — each division is one
            // SoldierTypeData, so divisions already cluster soldiers by class.
            foreach (var division in _company.divisions)
            {
                if (division?.soldiers == null) continue;
                var typeIcon = division.soldierType?.typeIcon;
                foreach (var soldier in division.soldiers)
                {
                    if (soldier?.villager == null) continue;
                    var row = BuildRow(soldier, typeIcon);
                    if (row != null) _rows.Add(row);
                }
            }
            Refresh();
        }

        private SoldierRow? BuildRow(VillagerOccupationSoldier soldier, Sprite? classIcon)
        {
            if (_rowsContainer == null) return null;
            const float RowHeight = 22f;

            // Full-row HP bar design: row bg is dark (shows through the
            // empty portion of the bar); HP fill is an anchor-driven Rect
            // that shrinks from the right as HP drops. Name + status text
            // overlay the bar in dark color with a light outline.
            var go = NewChild(_rowsContainer, "Row");
            go.AddComponent<LayoutElement>().preferredHeight = RowHeight;

            // Row background — dark "empty bar" color. Captures raycasts.
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.09f, 0.08f, 0.92f);
            bg.raycastTarget = true;

            // HP fill — sits on top of the row bg, full row height. Width is
            // driven by anchorMax.x in Refresh (no sprite needed; flat color
            // Image with anchor-driven width). This is the fix for the
            // earlier bug where Image.Type.Filled without a sprite rendered
            // as a constant-width flat rectangle.
            var fillGo = NewChild(go, "HpFill");
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.type = Image.Type.Simple;
            fillImg.color = HpColor(1f);
            fillImg.raycastTarget = false;

            // Class icon (left, vertically centered) — sits on top of the bar.
            if (classIcon != null)
            {
                var iconGo = NewChild(go, "ClassIcon");
                var iRt = (RectTransform)iconGo.transform;
                iRt.anchorMin = new Vector2(0, 0.5f);
                iRt.anchorMax = new Vector2(0, 0.5f);
                iRt.pivot = new Vector2(0, 0.5f);
                iRt.sizeDelta = new Vector2(18, 18);
                iRt.anchoredPosition = new Vector2(3, 0);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = classIcon;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // Name text — left-aligned, takes the left ~65% of the bar.
            // Dark navy color with a white outline so it reads against any
            // HP color (green / yellow / red).
            var nameText = NewText(go, "Name", soldier.villager.villagerName, 14,
                FontStyles.Bold | FontStyles.SmallCaps,
                Color.white, TextAlignmentOptions.MidlineLeft);
            var ntRt = nameText.rectTransform;
            ntRt.anchorMin = new Vector2(0, 0);
            ntRt.anchorMax = new Vector2(0.50f, 1);
            ntRt.pivot = new Vector2(0.5f, 0.5f);
            ntRt.offsetMin = new Vector2(classIcon != null ? 24 : 6, 0);
            ntRt.offsetMax = new Vector2(-2, 0);

            // Status text — right-aligned, takes the right ~35% of the bar.
            // Updated each Refresh tick from villager.GetStateString().
            var statusText = NewText(go, "Status", "", 13,
                FontStyles.Bold | FontStyles.SmallCaps,
                Color.white, TextAlignmentOptions.MidlineRight);
            statusText.enableWordWrapping = false;
            statusText.overflowMode = TextOverflowModes.Ellipsis;
            var stRt = statusText.rectTransform;
            stRt.anchorMin = new Vector2(0.50f, 0);
            stRt.anchorMax = new Vector2(1, 1);
            stRt.pivot = new Vector2(0.5f, 0.5f);
            stRt.offsetMin = new Vector2(2, 0);
            stRt.offsetMax = new Vector2(-6, 0);

            var click = go.AddComponent<RowClickHandler>();
            click.Soldier = soldier;
            return new SoldierRow
            {
                Root = go,
                Fill = fillImg,
                FillRt = fillRt,
                Name = nameText,
                Status = statusText,
                Soldier = soldier,
            };
        }

        /// <summary>Applies a soft white outline. Wrapped because the
        /// outlineWidth setter NREs on some font assets without an outline
        /// material channel (see PinnedResourceOverlay.NewText for the
        /// rationale).</summary>
        private static void ApplyLightOutline(TextMeshProUGUI t)
        {
            try
            {
                t.outlineWidth = 0.25f;
                t.outlineColor = new Color32(255, 255, 255, 200);
            }
            catch { }
        }

        private void Refresh()
        {
            // Prune dead/invalid rows; refresh HP bars on the rest.
            bool anyAlive = false;
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                var r = _rows[i];
                var s = r.Soldier;
                if (s == null || s.Equals(null) || s.villager == null)
                {
                    if (r.Root != null) Object.Destroy(r.Root);
                    _rows.RemoveAt(i);
                    continue;
                }
                // Combat HP comes from DamageableComponent.life/maxLife.
                // villagerHealth.health is general well-being (sickness /
                // exposure) and stays at ~1 even when the soldier is bleeding
                // out from arrows — wrong source for the bar.
                float hp = 1f;
                if (r.Damageable == null)
                    r.Damageable = s.villager.gameObject != null
                        ? s.villager.gameObject.GetComponent<DamageableComponent>() : null;
                if (r.Damageable != null && r.Damageable.maxLife > 0f)
                    hp = Mathf.Clamp01(r.Damageable.life / r.Damageable.maxLife);

                // Width is driven by the fill RectTransform's anchorMax.x —
                // no sprite needed, no Filled-mode rendering quirks.
                if (r.FillRt != null)
                {
                    var amax = r.FillRt.anchorMax;
                    amax.x = hp;
                    r.FillRt.anchorMax = amax;
                }

                // Status text — same source FF's vanilla barracks UI uses.
                if (r.Status != null)
                {
                    try
                    {
                        var newStatus = s.villager.GetStateString() ?? "";
                        if (r.Status.text != newStatus) r.Status.text = newStatus;
                    }
                    catch { /* state string can transiently throw — leave last value */ }
                }
                r.Fill.fillAmount = hp;
                r.Fill.color = HpColor(hp);
                anyAlive = true;
            }
            if (!anyAlive)
                CompanyOverlayManager.RequestClose(_company);
        }

        private static Color HpColor(float pct)
        {
            // Bright green at full HP, yellow at 50%, deep red at 0.
            if (pct >= 0.5f)
                return Color.Lerp(new Color(0.95f, 0.80f, 0.18f, 0.9f), new Color(0.20f, 0.75f, 0.20f, 0.9f), (pct - 0.5f) * 2f);
            return Color.Lerp(new Color(0.75f, 0.15f, 0.15f, 0.9f), new Color(0.95f, 0.80f, 0.18f, 0.9f), pct * 2f);
        }

        // ── Position persistence ───────────────────────────────────────────

        private Vector2 ReadSavedAnchor()
        {
            // Convert normalized → anchored at first show. DraggablePanel does
            // the same math on its own once attached, but we need an initial
            // position before the drag handler exists.
            float x = FFUIOverhaulMod.CompanyOverlayPosX?.Value ?? 0.5f;
            float y = FFUIOverhaulMod.CompanyOverlayPosY?.Value ?? 0.7f;
            var size = new Vector2(Screen.width, Screen.height);
            return new Vector2(x * size.x - 0.5f * size.x, y * size.y - 1f * size.y);
        }

        private static void SavePosition(Vector2 normalized)
        {
            if (FFUIOverhaulMod.CompanyOverlayPosX == null || FFUIOverhaulMod.CompanyOverlayPosY == null) return;
            FFUIOverhaulMod.CompanyOverlayPosX.Value = normalized.x;
            FFUIOverhaulMod.CompanyOverlayPosY.Value = normalized.y;
            MelonLoader.MelonPreferences.Save();
        }

        // ── UGUI helpers (mirrors PinnedResourceOverlay's) ─────────────────

        private static GameObject NewChild(GameObject parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
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
            t.font = FFNativeAssets.FontBody;
            try
            {
                t.outlineWidth = 0.18f;
                t.outlineColor = new Color32(0, 0, 0, 220);
            }
            catch { }
            return t;
        }

        private class SoldierRow
        {
            public GameObject Root = null!;
            public Image Fill = null!;
            public RectTransform FillRt = null!;
            public TextMeshProUGUI Name = null!;
            public TextMeshProUGUI Status = null!;
            public VillagerOccupationSoldier Soldier = null!;
            public DamageableComponent? Damageable;
        }
    }

    /// <summary>
    /// Pointer click handler attached to each soldier row. Single click selects
    /// the soldier; a second click within 0.4s centers the camera on them.
    /// </summary>
    internal class RowClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public VillagerOccupationSoldier? Soldier;
        private static float _lastClickTime;
        private static RowClickHandler? _lastClicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (Soldier == null || Soldier.villager == null || Soldier.villager.gameObject == null) return;
            var go = Soldier.villager.gameObject;
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return;

            // Single-click → select via SelectVillager so the input state
            // machine pushes Input_SelectVillager — that's the state that
            // wires up right-click move/attack commands. Input_SelectGameObject
            // (what SelectGameObject would push) doesn't accept move orders,
            // so the soldier looked selected but right-click just deselected.
            try { gm.inputManager.SelectVillager(Soldier.villager, false, false); }
            catch (System.Exception ex) { FFUIOverhaulMod.Log.Warning($"[CompanyOverlay] Select failed: {ex.Message}"); }

            // Double-click within 0.4s → camera focus
            if (_lastClicked == this && Time.unscaledTime - _lastClickTime < 0.4f)
            {
                try { gm.cameraManager?.FocusOnObject(go); }
                catch (System.Exception ex) { FFUIOverhaulMod.Log.Warning($"[CompanyOverlay] Focus failed: {ex.Message}"); }
                _lastClicked = null;
                _lastClickTime = 0;
                return;
            }
            _lastClicked = this;
            _lastClickTime = Time.unscaledTime;
        }
    }

    /// <summary>
    /// Tracks open overlays keyed by MilitaryCompany. Shift+LClick on a
    /// banner button toggles the matching panel; Tick() drives per-frame
    /// refresh and auto-closes panels for dead companies.
    /// </summary>
    public static class CompanyOverlayManager
    {
        private static readonly Dictionary<MilitaryCompany, CompanyOverlay> _open = new();
        private static readonly List<MilitaryCompany> _toClose = new();

        public static void Toggle(MilitaryCompany company)
        {
            if (company == null) return;
            if (_open.TryGetValue(company, out var existing))
            {
                existing.Destroy();
                _open.Remove(company);
                return;
            }
            var overlay = new CompanyOverlay(company);
            overlay.Show();
            _open[company] = overlay;
        }

        internal static void RequestClose(MilitaryCompany company)
        {
            if (!_toClose.Contains(company)) _toClose.Add(company);
        }

        public static void Tick()
        {
            foreach (var kvp in _open) kvp.Value.Tick();
            if (_toClose.Count > 0)
            {
                foreach (var c in _toClose)
                    if (_open.TryGetValue(c, out var o)) { o.Destroy(); _open.Remove(c); }
                _toClose.Clear();
            }
        }
    }
}
