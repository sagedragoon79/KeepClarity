using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// UGUI panel that replaces the Stage 1 IMGUI prototype. Built on a
    /// standalone Canvas so we don't have to integrate with FF's UI hierarchy
    /// (avoids breaking when Crate updates the options menu).
    ///
    /// Currently a skeleton — header bar, close button, draggable, master/detail
    /// columns with placeholder content. Phases 2C/2D fill in the columns.
    /// </summary>
    public class SettingsCanvas : MonoBehaviour
    {
        private const int SORT_ORDER = 5000;
        private const float WIDTH = 1320f;
        private const float HEIGHT = 870f;
        private const float HEADER_HEIGHT = 48f;
        private const float LEFT_RAIL_WIDTH = 320f;

        private static SettingsCanvas? _instance;
        public static SettingsCanvas? Instance => _instance;

        private Canvas? _canvas;
        private RectTransform? _root;
        public RectTransform? LeftRail { get; private set; }
        public RectTransform? RightPane { get; private set; }
        private TMP_Text? _headerLabel;
        private ModListPanel? _modList;
        private ModDetailPanel? _modDetail;
        private GameObject? _restartBanner;
        private Image? _saveBtnImage;
        private Button? _saveBtn;
        private GameObject? _unsavedPrompt;
        private CanvasGroup? _canvasGroup;
        private bool _isDirty;          // any change since last save (or session start)
        private bool _saveBtnIsGreen;   // Save button currently in "saved" state
        // Banner reflects "any RestartRequired pref currently differs from
        // its session-start value" — a revert clears the entry; banner hides
        // when the set is empty. Session-start values themselves live on
        // SettingsRegistry so the Reset button can also restore from them.
        private readonly System.Collections.Generic.HashSet<string> _pendingRestartChanges
            = new System.Collections.Generic.HashSet<string>();

        public static SettingsCanvas EnsureInstance()
        {
            if (_instance != null) return _instance;
            FFNativeAssets.EnsureProbed();

            var go = new GameObject("KeepClarity_SettingsCanvas");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsCanvas>();
            _instance.Build();
            // Initial hide — directly, no animation. Calling SetVisible(false)
            // here would kick off a ZoomOut coroutine on a panel that was never
            // actually open, and the next F10 (which arrives in the same frame
            // via Toggle) would mis-detect IsOpen=true and double-close,
            // producing a flash before the panel finally opens on the 2nd press.
            if (_instance._canvas != null) _instance._canvas.gameObject.SetActive(false);
            if (_instance._canvasGroup != null) _instance._canvasGroup.alpha = 0f;
            return _instance;
        }

        public static bool IsOpen => _instance != null && _instance._canvas != null && _instance._canvas.gameObject.activeSelf;

        public static void Toggle()
        {
            var inst = EnsureInstance();
            inst.SetVisible(!IsOpen);
        }

        public static void Open()
        {
            var inst = EnsureInstance();
            inst.SetVisible(true);
        }

        public static void Close()
        {
            if (_instance != null) _instance.SetVisible(false);
        }

        /// <summary>Re-render everything that shows localized strings. Called by
        /// KcLoc when the game language changes: the mod list rebuilds its names
        /// and the detail panel rebuilds its sections/labels (Select() early-outs
        /// on the same mod, so a plain reopen kept stale labels — this doesn't).</summary>
        public static void OnLanguageChanged()
        {
            if (_instance == null) return;
            try
            {
                _instance._modList?.Refresh();
                _instance._modDetail?.Refresh(); // rebuilds current mod's sections via ShowMod
            }
            catch (System.Exception ex)
            {
                FFUIOverhaulMod.Log.Warning("[Loc] language-change re-render failed: " + ex.Message);
            }
        }

        private System.Collections.IEnumerator? _animCoroutine;

        private void SetVisible(bool visible)
        {
            if (_canvas == null) return;

            // Cancel any in-flight zoom so rapid F10 toggles don't overlap.
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);

            if (visible)
            {
                _canvasGroup!.alpha = 0f;
                _canvas.gameObject.SetActive(true);
                // Snapshot every entry's current value once. Reset uses these
                // for "revert to session-start"; restart-banner detection
                // uses these for diff against current value.
                SettingsRegistry.CaptureSessionStartValues();
                if (_restartBanner != null) _restartBanner.SetActive(_pendingRestartChanges.Count > 0);

                // Repopulate list each time we open — picks up newly-registered
                // mods and reflects current "changed" counts.
                _modList?.Refresh();
                if (_modList != null && _modList.SelectedModId == null)
                {
                    var first = SettingsRegistry.Mods.OrderBy(m => m.Value.Order)
                                                    .ThenBy(m => m.Value.DisplayName)
                                                    .FirstOrDefault();
                    if (!first.Equals(default(System.Collections.Generic.KeyValuePair<string, ModSettingsInfo>)))
                        _modList.Select(first.Key);
                }

                _animCoroutine = ZoomIn(1.0f);
                StartCoroutine(_animCoroutine);
            }
            else
            {
                _animCoroutine = ZoomOut(0.6f);
                StartCoroutine(_animCoroutine);
                TooltipOverlay.Hide();
            }
        }

        // Zoom origin offset — relative to canvas center, points toward the
        // upper-right where FF's settings menu icon lives.
        private Vector2 ZoomOriginPosition()
        {
            if (_canvas == null) return new Vector2(800, 400);
            var canvasRT = (RectTransform)_canvas.transform;
            var rect = canvasRT.rect;
            // Roughly upper-right corner — 50px padding from top/right edge.
            return new Vector2(rect.width * 0.5f - 50f, rect.height * 0.5f - 50f);
        }

        private System.Collections.IEnumerator ZoomIn(float duration)
        {
            if (_root == null || _canvasGroup == null) yield break;
            var origin = ZoomOriginPosition();
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Ease-out cubic: 1 - (1-u)^3
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                _root.anchoredPosition = Vector2.Lerp(origin, Vector2.zero, eased);
                _root.localScale = Vector3.one * Mathf.Lerp(0.05f, 1f, eased);
                _canvasGroup.alpha = eased;
                yield return null;
            }
            _root.anchoredPosition = Vector2.zero;
            _root.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
            _animCoroutine = null;
        }

        private System.Collections.IEnumerator ZoomOut(float duration)
        {
            if (_root == null || _canvasGroup == null || _canvas == null) yield break;
            var target = ZoomOriginPosition();
            var startPos = _root.anchoredPosition;
            float startScale = _root.localScale.x;
            float startAlpha = _canvasGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                // Ease-in cubic so it accelerates away
                float eased = u * u * u;
                _root.anchoredPosition = Vector2.Lerp(startPos, target, eased);
                _root.localScale = Vector3.one * Mathf.Lerp(startScale, 0.05f, eased);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
                yield return null;
            }
            _canvas.gameObject.SetActive(false);
            // Reset for next open so layout doesn't measure a tiny rect.
            _root.anchoredPosition = Vector2.zero;
            _root.localScale = Vector3.one;
            _canvasGroup.alpha = 0f;
            _animCoroutine = null;
        }

        private void Build()
        {
            // === Canvas ===
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SORT_ORDER;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGroup = canvasGo.AddComponent<CanvasGroup>();

            // === Click-blocker (full screen, swallows clicks behind the panel) ===
            var blocker = NewImage("ClickBlocker", canvasGo.transform);
            blocker.color = new Color(0, 0, 0, 0.45f);
            var blockerRT = blocker.rectTransform;
            blockerRT.anchorMin = Vector2.zero;
            blockerRT.anchorMax = Vector2.one;
            blockerRT.offsetMin = Vector2.zero;
            blockerRT.offsetMax = Vector2.zero;

            // === Drop shadow behind panel (FF's IMG_BGShadowSoft01) ===
            if (FFNativeAssets.ShadowSoft != null)
            {
                var shadow = NewImage("PanelShadow", canvasGo.transform);
                shadow.sprite = FFNativeAssets.ShadowSoft;
                shadow.type = Image.Type.Sliced;
                shadow.color = new Color(0, 0, 0, 0.7f);
                var srt = shadow.rectTransform;
                srt.sizeDelta = new Vector2(WIDTH + 60, HEIGHT + 60);
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = Vector2.zero;
            }

            // === Root panel — uses FF's IMG_BorderSimpleThick01B 9-slice ===
            var panel = NewImage("Panel", canvasGo.transform);
            ApplyBorderSprite(panel, FFNativeAssets.PanelBorder, FFNativeAssets.PanelTintDefault);
            _root = panel.rectTransform;
            _root.sizeDelta = new Vector2(WIDTH, HEIGHT);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.anchoredPosition = Vector2.zero;

            BuildHeader(panel.transform);
            BuildBody(panel.transform);
            BuildFooter(panel.transform);

            // Tooltip overlay lives INSIDE the main Canvas so z-order is
            // simple hierarchy ordering — Show() calls SetAsLastSibling() so
            // it renders on top of everything else in the panel.
            TooltipOverlay.EnsureInstance(canvasGo.transform);

            BuildUnsavedPrompt(canvasGo.transform);
        }

        private void BuildUnsavedPrompt(Transform parent)
        {
            // Outer click-blocker — full canvas, dark transparent.
            var rootGo = new GameObject("UnsavedPrompt", typeof(RectTransform), typeof(Image));
            rootGo.transform.SetParent(parent, false);
            var rrt = (RectTransform)rootGo.transform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            var blocker = rootGo.GetComponent<Image>();
            blocker.color = new Color(0, 0, 0, 0.55f);

            // Centered dialog panel
            var dlgGo = new GameObject("Dialog", typeof(RectTransform), typeof(Image));
            dlgGo.transform.SetParent(rootGo.transform, false);
            var drt = (RectTransform)dlgGo.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(520, 200);
            drt.anchoredPosition = Vector2.zero;
            ApplyBorderSprite(dlgGo.GetComponent<Image>(), FFNativeAssets.PanelBorder, FFNativeAssets.PanelTintDefault);

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(dlgGo.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontHeader != null) title.font = FFNativeAssets.FontHeader;
            title.text = "Unsaved Changes";
            title.fontSize = 22;
            title.color = FFNativeAssets.WarningAmber;
            title.characterSpacing = 3f;
            title.alignment = TextAlignmentOptions.Center;
            var trt2 = title.rectTransform;
            trt2.anchorMin = new Vector2(0, 1);
            trt2.anchorMax = new Vector2(1, 1);
            trt2.pivot = new Vector2(0.5f, 1);
            trt2.sizeDelta = new Vector2(0, 36);
            trt2.anchoredPosition = new Vector2(0, -16);

            // Body
            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(dlgGo.transform, false);
            var body = bodyGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) body.font = FFNativeAssets.FontBody;
            body.text = "Settings have changed but haven't been saved.\nClose anyway?";
            body.fontSize = 14;
            body.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            body.color = FFNativeAssets.TextPrimary;
            body.alignment = TextAlignmentOptions.Center;
            body.enableWordWrapping = true;
            var brt2 = body.rectTransform;
            brt2.anchorMin = new Vector2(0, 0);
            brt2.anchorMax = new Vector2(1, 1);
            brt2.offsetMin = new Vector2(20, 60);
            brt2.offsetMax = new Vector2(-20, -60);

            // Buttons (Cancel / Close Anyway)
            var cancelBtn = NewButton("Cancel", dlgGo.transform, "Cancel", () =>
            {
                if (_unsavedPrompt != null) _unsavedPrompt.SetActive(false);
            }, fancy: true);
            var crt = cancelBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 0);
            crt.anchorMax = new Vector2(0, 0);
            crt.pivot = new Vector2(0, 0);
            crt.sizeDelta = new Vector2(160, 40);
            crt.anchoredPosition = new Vector2(40, 20);

            var closeAnywayBtn = NewButton("CloseAnyway", dlgGo.transform, "Close Anyway", () =>
            {
                if (_unsavedPrompt != null) _unsavedPrompt.SetActive(false);
                Close();
            }, fancy: true);
            var carT = closeAnywayBtn.GetComponent<RectTransform>();
            carT.anchorMin = new Vector2(1, 0);
            carT.anchorMax = new Vector2(1, 0);
            carT.pivot = new Vector2(1, 0);
            carT.sizeDelta = new Vector2(200, 40);
            carT.anchoredPosition = new Vector2(-40, 20);

            _unsavedPrompt = rootGo;
            _unsavedPrompt.SetActive(false);
        }

        private void ShowUnsavedPrompt()
        {
            if (_unsavedPrompt == null) return;
            _unsavedPrompt.SetActive(true);
            _unsavedPrompt.transform.SetAsLastSibling();
        }

        private void BuildHeader(Transform parent)
        {
            var header = NewImage("Header", parent);
            ApplyBorderSprite(header, FFNativeAssets.PanelHeaderTop ?? FFNativeAssets.PanelBorderDark,
                FFNativeAssets.PanelTintHeader);
            var rt = header.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0, HEADER_HEIGHT);
            rt.anchoredPosition = Vector2.zero;

            // Drag handle on the header
            var drag = header.gameObject.AddComponent<PanelDragHandle>();
            drag.Target = _root;

            // Title text — FF's medieval headline font (FePIrm27C). Match the
            // size FF uses for its big window titles ("Buildings", "Reports").
            _headerLabel = NewHeaderText("HeaderLabel", header.transform, "Keep Clarity — Mod Settings", 26);
            _headerLabel.characterSpacing = 4f;
            var labelRT = _headerLabel.rectTransform;
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(24, 0);
            labelRT.offsetMax = new Vector2(-52, 0);
            _headerLabel.alignment = TextAlignmentOptions.MidlineLeft;

            // Close button — uses FF's standard BTN_ExitRound_UP sprite (the
            // round X close button used on every named window in FF). The
            // sprite IS the visual, no text label needed.
            var closeBtn = NewSpriteButton("CloseBtn", header.transform,
                FFNativeAssets.ExitRoundButton, () => Close());
            var closeRT = closeBtn.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1, 0.5f);
            closeRT.anchorMax = new Vector2(1, 0.5f);
            closeRT.pivot = new Vector2(1, 0.5f);
            closeRT.anchoredPosition = new Vector2(-12, 0);
            closeRT.sizeDelta = new Vector2(32, 32);  // native sprite size
        }

        private void BuildBody(Transform parent)
        {
            var body = new GameObject("Body", typeof(RectTransform));
            body.transform.SetParent(parent, false);
            var bodyRT = (RectTransform)body.transform;
            bodyRT.anchorMin = new Vector2(0, 0);
            bodyRT.anchorMax = new Vector2(1, 1);
            bodyRT.offsetMin = new Vector2(12, 64); // leave room for footer
            bodyRT.offsetMax = new Vector2(-12, -HEADER_HEIGHT - 12);

            // Left rail — FF's lighter inner panel
            var leftGo = new GameObject("LeftRail", typeof(RectTransform));
            leftGo.transform.SetParent(body.transform, false);
            LeftRail = (RectTransform)leftGo.transform;
            LeftRail.anchorMin = new Vector2(0, 0);
            LeftRail.anchorMax = new Vector2(0, 1);
            LeftRail.pivot = new Vector2(0, 0.5f);
            LeftRail.sizeDelta = new Vector2(LEFT_RAIL_WIDTH, 0);
            LeftRail.anchoredPosition = Vector2.zero;
            var leftBg = leftGo.AddComponent<Image>();
            ApplyBorderSprite(leftBg, FFNativeAssets.PanelBorderLight, FFNativeAssets.PanelTintRail);

            // Mod list (Phase 2C)
            _modList = new ModListPanel();
            _modList.Build(LeftRail);
            _modList.OnModSelected += modId => _modDetail?.ShowMod(modId);
            // When the "only changed" toggle / search box changes, re-render
            // the right pane so unchanged entries within the selected mod
            // also hide.
            _modList.OnFilterChanged += () => _modDetail?.Refresh();

            // Track whether any restart-required pref changed this session;
            // if so, show the sticky banner in the footer.
            SettingsRegistry.SettingChanged += OnAnySettingChanged;

            // Vertical divider — FF's directional divider sprite
            var divider = NewImage("Divider", body.transform);
            if (FFNativeAssets.DividerDirectional != null)
            {
                divider.sprite = FFNativeAssets.DividerDirectional;
                divider.type = Image.Type.Sliced;
                divider.color = FFNativeAssets.DividerTint;
            }
            else
            {
                divider.color = new Color(0.30f, 0.24f, 0.18f, 1f);
            }
            var divRT = divider.rectTransform;
            divRT.anchorMin = new Vector2(0, 0);
            divRT.anchorMax = new Vector2(0, 1);
            divRT.pivot = new Vector2(0, 0.5f);
            divRT.sizeDelta = new Vector2(2, 0);
            divRT.anchoredPosition = new Vector2(LEFT_RAIL_WIDTH + 2, 0);

            // Right pane
            var rightGo = new GameObject("RightPane", typeof(RectTransform));
            rightGo.transform.SetParent(body.transform, false);
            RightPane = (RectTransform)rightGo.transform;
            RightPane.anchorMin = new Vector2(0, 0);
            RightPane.anchorMax = new Vector2(1, 1);
            RightPane.offsetMin = new Vector2(LEFT_RAIL_WIDTH + 10, 0);
            RightPane.offsetMax = new Vector2(0, 0);

            // Detail pane (Phase 2C placeholder + 2D controls)
            _modDetail = new ModDetailPanel();
            _modDetail.Build(RightPane);
            // Forward the mod list's filter state so detail filters in lockstep
            _modDetail.OnlyChangedProvider = () => _modList?.OnlyChanged ?? false;
        }

        private void BuildFooter(Transform parent)
        {
            var footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(parent, false);
            var rt = (RectTransform)footer.transform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(0, 56);
            rt.anchoredPosition = Vector2.zero;

            // Restart-required banner — left side of footer, hidden until a
            // RestartRequired pref is changed during the session.
            BuildRestartBanner(footer.transform);

            // Close button (rightmost) — closes panel. If there are unsaved
            // changes, prompts the user before actually closing.
            var closeBtn = NewButton("CloseBtn", footer.transform, "Close", () =>
            {
                if (_isDirty) ShowUnsavedPrompt();
                else Close();
            }, fancy: true);
            var cbRT = closeBtn.GetComponent<RectTransform>();
            cbRT.anchorMin = new Vector2(1, 0.5f);
            cbRT.anchorMax = new Vector2(1, 0.5f);
            cbRT.pivot = new Vector2(1, 0.5f);
            cbRT.anchoredPosition = new Vector2(-24, 4);
            cbRT.sizeDelta = new Vector2(140, 40);

            // Save button — turns green and STAYS green after click. Reverts
            // to normal the next time any setting changes.
            var saveBtn = NewButton("SaveBtn", footer.transform, "Save", () =>
            {
                try { MelonLoader.MelonPreferences.Save(); } catch { }
                _isDirty = false;
                StartCoroutine(FadeSaveButtonToGreen());
            }, fancy: true);
            var sbRT = saveBtn.GetComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1, 0.5f);
            sbRT.anchorMax = new Vector2(1, 0.5f);
            sbRT.pivot = new Vector2(1, 0.5f);
            sbRT.anchoredPosition = new Vector2(-172, 4);  // left of Close (140 + 8 gap)
            sbRT.sizeDelta = new Vector2(140, 40);

            _saveBtnImage = saveBtn.GetComponent<Image>();
            _saveBtn = saveBtn.GetComponent<Button>();

            // Reload button — re-runs auto-discovery to pick up any new prefs
            // added by a mod update without requiring a game restart. Useful
            // for live mod iteration and as a fallback when an integration
            // file is out of date with its mod's prefs.
            var reloadBtn = NewButton("ReloadBtn", footer.transform, "Reload", () =>
            {
                ReloadRegistrations();
            }, fancy: false);
            var rbRT = reloadBtn.GetComponent<RectTransform>();
            rbRT.anchorMin = new Vector2(1, 0.5f);
            rbRT.anchorMax = new Vector2(1, 0.5f);
            rbRT.pivot = new Vector2(1, 0.5f);
            rbRT.anchoredPosition = new Vector2(-320, 4);  // left of Save
            rbRT.sizeDelta = new Vector2(110, 36);
        }

        private void ReloadRegistrations()
        {
            try
            {
                int discovered = SettingsDiscovery.RunDiscovery();
                FFUIOverhaulMod.Log.Msg($"[Settings] Reload: re-ran discovery, +{discovered} new entries");
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"[Settings] Reload failed: {ex.Message}");
            }
            // Repaint the panel — list might have new mods, detail might have
            // new entries under "Other settings".
            _modList?.Refresh();
            _modDetail?.Refresh();
        }

        private void BuildRestartBanner(Transform parent)
        {
            var bannerGo = new GameObject("RestartBanner", typeof(RectTransform));
            bannerGo.transform.SetParent(parent, false);
            var brt = (RectTransform)bannerGo.transform;
            brt.anchorMin = new Vector2(0, 0.5f);
            brt.anchorMax = new Vector2(0, 0.5f);
            brt.pivot = new Vector2(0, 0.5f);
            brt.anchoredPosition = new Vector2(24, 4);
            brt.sizeDelta = new Vector2(820, 36);

            // Warning icon — only render if FF has a real warning sprite to use.
            // Otherwise omit entirely so we don't paint a mystery white box.
            float textLeftOffset = 0f;
            if (FFNativeAssets.IconWarning != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(bannerGo.transform, false);
                var iconRT = (RectTransform)iconGo.transform;
                iconRT.anchorMin = new Vector2(0, 0.5f);
                iconRT.anchorMax = new Vector2(0, 0.5f);
                iconRT.pivot = new Vector2(0, 0.5f);
                iconRT.sizeDelta = new Vector2(20, 20);
                iconRT.anchoredPosition = new Vector2(0, 0);
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = FFNativeAssets.IconWarning;
                iconImg.preserveAspect = true;
                iconImg.color = FFNativeAssets.WarningAmber;
                textLeftOffset = 28f;
            }

            // Glow strip behind the text (FF-style "TextGlow" pattern) so the
            // amber message reads clearly against the dark footer.
            if (FFNativeAssets.ShadowSoft != null)
            {
                var glowGo = new GameObject("TextGlow", typeof(RectTransform), typeof(Image));
                glowGo.transform.SetParent(bannerGo.transform, false);
                var grt = (RectTransform)glowGo.transform;
                grt.anchorMin = new Vector2(0, 0);
                grt.anchorMax = new Vector2(1, 1);
                grt.offsetMin = new Vector2(textLeftOffset - 6, -2);
                grt.offsetMax = new Vector2(0, 2);
                var gimg = glowGo.GetComponent<Image>();
                gimg.sprite = FFNativeAssets.ShadowSoft;
                gimg.type = Image.Type.Sliced;
                gimg.color = new Color(0.10f, 0.07f, 0.04f, 0.65f);
                gimg.raycastTarget = false;
            }

            // Text — exact wording from the plan; bumped to 17pt and bold +
            // small-caps for prominence.
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(bannerGo.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) txt.font = FFNativeAssets.FontBody;
            txt.fontSize = 17;
            txt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            txt.color = FFNativeAssets.WarningAmber;
            txt.text = "Changes have been made that require a restart of Farthest Frontier to take effect";
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            // TMP outline for an extra hint of depth around the amber text.
            txt.outlineColor = new Color(0.20f, 0.10f, 0f, 1f);
            txt.outlineWidth = 0.18f;
            var trt = txt.rectTransform;
            trt.anchorMin = new Vector2(0, 0);
            trt.anchorMax = new Vector2(1, 1);
            trt.offsetMin = new Vector2(textLeftOffset, 0);
            trt.offsetMax = new Vector2(0, 0);

            _restartBanner = bannerGo;
            _restartBanner.SetActive(false);
        }

        private static readonly Color SAVE_BTN_NORMAL = Color.white;
        private static readonly Color SAVE_BTN_SAVED = new Color(0.55f, 0.85f, 0.45f, 1f);

        private System.Collections.IEnumerator FadeSaveButtonToGreen()
        {
            float t = 0f;
            const float fadeIn = 0.15f;
            var start = _saveBtnImage != null ? _saveBtnImage.color : SAVE_BTN_NORMAL;
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                if (_saveBtnImage != null)
                    _saveBtnImage.color = Color.Lerp(start, SAVE_BTN_SAVED, t / fadeIn);
                yield return null;
            }
            if (_saveBtnImage != null) _saveBtnImage.color = SAVE_BTN_SAVED;
            _saveBtnIsGreen = true;
        }

        private System.Collections.IEnumerator FadeSaveButtonToNormal()
        {
            float t = 0f;
            const float fadeOut = 0.20f;
            var start = _saveBtnImage != null ? _saveBtnImage.color : SAVE_BTN_SAVED;
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                if (_saveBtnImage != null)
                    _saveBtnImage.color = Color.Lerp(start, SAVE_BTN_NORMAL, t / fadeOut);
                yield return null;
            }
            if (_saveBtnImage != null) _saveBtnImage.color = SAVE_BTN_NORMAL;
            _saveBtnIsGreen = false;
        }

        private void OnAnySettingChanged(string modId, string key, object? newValue)
        {
            // Any registered change marks the panel dirty + reverts the
            // Save button's "saved" green tint back to normal.
            _isDirty = true;
            if (_saveBtnIsGreen && _saveBtnImage != null)
            {
                StartCoroutine(FadeSaveButtonToNormal());
            }

            SettingEntryRecord? match = null;
            foreach (var rec in SettingsRegistry.Entries)
            {
                if (rec.ModId == modId && rec.Key == key) { match = rec; break; }
            }

            try
            {
                if (FFUIOverhaulMod.SettingsVerboseLog?.Value ?? false)
                {
                    bool rr = match?.Meta.RestartRequired ?? false;
                    FFUIOverhaulMod.Log.Msg($"[Settings] Change: {modId}::{key} = {newValue}  (restartRequired={rr})");
                }
            }
            catch { }

            if (match == null || !match.Meta.RestartRequired) return;

            string compoundKey = modId + "::" + key;

            // Compare current value against session-start. If equal, the change
            // was reverted → drop it from pending. If different, add it.
            if (SettingsRegistry.TryGetSessionStartValue(modId, key, out var startVal))
            {
                bool sameAsStart = false;
                try { sameAsStart = Equals(match.Get(), startVal); } catch { }
                if (sameAsStart) _pendingRestartChanges.Remove(compoundKey);
                else _pendingRestartChanges.Add(compoundKey);
            }
            else
            {
                _pendingRestartChanges.Add(compoundKey);
            }

            bool needsRestart = _pendingRestartChanges.Count > 0;
            if (_restartBanner != null)
            {
                _restartBanner.SetActive(needsRestart);
                if (needsRestart) _restartBanner.transform.SetAsLastSibling();
            }

            if (FFUIOverhaulMod.SettingsVerboseLog?.Value ?? false)
            {
                try { FFUIOverhaulMod.Log.Msg($"[Settings] Pending restart changes: {_pendingRestartChanges.Count} ({compoundKey} {(_pendingRestartChanges.Contains(compoundKey) ? "added" : "reverted")})"); } catch { }
            }
        }


        private void OnDestroy()
        {
            SettingsRegistry.SettingChanged -= OnAnySettingChanged;
        }

        // === Helpers ===

        private static Image NewImage(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        public static TMP_Text NewHeaderText(string name, Transform parent, string text, float size = 18)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontHeader != null) t.font = FFNativeAssets.FontHeader;
            t.text = text;
            t.fontSize = size;
            t.color = FFNativeAssets.TextPrimary;
            t.enableWordWrapping = true;
            return t;
        }

        public static TMP_Text NewBodyText(string name, Transform parent, string text, float size = 13)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) t.font = FFNativeAssets.FontBody;
            t.text = text;
            t.fontSize = size;
            // FF's body text uses Bold + SmallCaps — match that styling
            t.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            t.color = FFNativeAssets.TextPrimary;
            t.enableWordWrapping = true;
            return t;
        }

        /// <summary>
        /// Button whose visual IS a sprite (no text label). Used for close/cancel
        /// buttons where FF ships a dedicated icon sprite.
        /// </summary>
        public static GameObject NewSpriteButton(string name, Transform parent, Sprite? sprite, Action onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.color = FFNativeAssets.PanelTintDefault;
                img.preserveAspect = true;
            }
            else
            {
                // Fallback if the sprite probe failed — small dark square
                img.color = new Color(0.20f, 0.16f, 0.12f, 1f);
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = FFNativeAssets.PanelTintDefault;
            colors.highlightedColor = new Color(1.20f, 1.10f, 0.90f, 1f);
            colors.pressedColor = new Color(0.80f, 0.75f, 0.65f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(() => { try { onClick(); } catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings] sprite button click threw: {ex.Message}"); } });
            return go;
        }

        public static GameObject NewButton(string name, Transform parent, string label, Action onClick, bool fancy = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            ApplyBorderSprite(img,
                fancy ? FFNativeAssets.ButtonFancy : (FFNativeAssets.ButtonFocus ?? FFNativeAssets.ButtonGeneric),
                FFNativeAssets.PanelTintDefault);

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) lbl.font = FFNativeAssets.FontBody;
            lbl.text = label;
            lbl.fontSize = 14;
            lbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            lbl.color = FFNativeAssets.TextPrimary;
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(8, 2);
            lblRT.offsetMax = new Vector2(-8, -2);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            // Hover/pressed transitions via color tint on the FF border sprite
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = FFNativeAssets.PanelTintDefault;
            colors.highlightedColor = new Color(1.15f, 1.10f, 0.95f, 1f);
            colors.pressedColor = new Color(0.80f, 0.75f, 0.65f, 1f);
            colors.selectedColor = new Color(1.05f, 1.00f, 0.90f, 1f);
            btn.colors = colors;

            btn.onClick.AddListener(() => { try { onClick(); } catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings] button click threw: {ex.Message}"); } });

            return go;
        }

        /// <summary>
        /// Builds a vertical scrollbar attached to the right edge of the given
        /// scroll-rect, wires it to <see cref="ScrollRect.verticalScrollbar"/>,
        /// and tweaks decelerationRate for a smoother glide. Used by both
        /// the mod list and the detail pane.
        /// </summary>
        public static void AttachVerticalScrollbar(ScrollRect sr, RectTransform scrollRectRT, float widthPx = 15f)
        {
            // Create the scrollbar GameObject as a sibling of the viewport so
            // RectMask2D doesn't clip it.
            var sbGo = new GameObject("VScrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(scrollRectRT, false);
            var sbRT = (RectTransform)sbGo.transform;
            sbRT.anchorMin = new Vector2(1, 0);
            sbRT.anchorMax = new Vector2(1, 1);
            sbRT.pivot = new Vector2(1, 0.5f);
            sbRT.sizeDelta = new Vector2(widthPx, 0);
            sbRT.anchoredPosition = new Vector2(0, 0);

            var trackImg = sbGo.GetComponent<Image>();
            // Track: thin dark strip; reuse FF's small dark border 9-slice.
            ApplyBorderSprite(trackImg, FFNativeAssets.PanelBorderSimple,
                new Color(0.20f, 0.18f, 0.14f, 0.85f));

            // SlidingArea + Handle
            var slideAreaGo = new GameObject("Sliding Area", typeof(RectTransform));
            slideAreaGo.transform.SetParent(sbGo.transform, false);
            var slideRT = (RectTransform)slideAreaGo.transform;
            slideRT.anchorMin = Vector2.zero;
            slideRT.anchorMax = Vector2.one;
            slideRT.offsetMin = new Vector2(2, 2);
            slideRT.offsetMax = new Vector2(-2, -2);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slideAreaGo.transform, false);
            var handleRT = (RectTransform)handleGo.transform;
            handleRT.anchorMin = Vector2.zero;
            handleRT.anchorMax = Vector2.one;
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;
            var handleImg = handleGo.GetComponent<Image>();
            // Handle: amber-ish to match FF's slider fill style.
            ApplyBorderSprite(handleImg, FFNativeAssets.PanelBorderLight,
                new Color(0.95f, 0.70f, 0.30f, 1f));

            var sb = sbGo.GetComponent<Scrollbar>();
            sb.targetGraphic = handleImg;
            sb.handleRect = handleRT;
            sb.direction = Scrollbar.Direction.BottomToTop;

            sr.verticalScrollbar = sb;
            sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            sr.verticalScrollbarSpacing = 2f;

            // Smoother glide: lower deceleration = slower coast-out.
            sr.inertia = true;
            sr.decelerationRate = 0.05f;
        }

        public static void ApplyBorderSprite(Image img, Sprite? sprite, Color tint)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = tint;
            }
            else
            {
                // Fallback when sprite probe failed — flat tinted box
                img.color = new Color(0.10f, 0.09f, 0.08f, 1f);
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }
    }

    /// <summary>Drag handle — clicking and dragging on this transform moves the target rect.</summary>
    public class PanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform? Target;
        private Vector2 _startMouse;
        private Vector2 _startPos;

        public void OnBeginDrag(PointerEventData e)
        {
            if (Target == null) return;
            _startMouse = e.position;
            _startPos = Target.anchoredPosition;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Target == null) return;
            Target.anchoredPosition = _startPos + (e.position - _startMouse);
        }
    }
}
