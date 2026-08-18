using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// Left-rail mod list. Builds itself inside whatever RectTransform is
    /// passed to <see cref="Build"/>. Stage 2C: search box, only-changed
    /// filter, scrolling list of clickable mod rows with accent stripes,
    /// hover/active states, and a settings-count subtitle.
    ///
    /// Selecting a mod fires <see cref="OnModSelected"/>; the right pane
    /// (Stage 2D) listens to that event to repopulate.
    /// </summary>
    public class ModListPanel
    {
        public event Action<string?>? OnModSelected;
        public event Action? OnFilterChanged;
        public string? SelectedModId { get; private set; }
        public bool OnlyChanged => _onlyChanged;

        private RectTransform _content = null!;
        private TMP_InputField? _searchInput;
        private Toggle? _onlyChangedToggle;
        private string _search = "";
        private bool _onlyChanged;
        private readonly List<ModRow> _rows = new List<ModRow>();

        public void Build(RectTransform parent)
        {
            // Top control strip: toggle row on top (22px), search box below (32px).
            // Total strip height = 22 + 6 gap + 32 = 60.
            var top = new GameObject("ModListTop", typeof(RectTransform));
            top.transform.SetParent(parent, false);
            var topRT = (RectTransform)top.transform;
            topRT.anchorMin = new Vector2(0, 1);
            topRT.anchorMax = new Vector2(1, 1);
            topRT.pivot = new Vector2(0.5f, 1);
            topRT.sizeDelta = new Vector2(0, 60);
            topRT.anchoredPosition = new Vector2(0, -10);

            BuildOnlyChangedToggle(top.transform, topRT);
            BuildSearchBox(top.transform, topRT);

            // ScrollRect with vertical layout content. RectMask2D clips by
            // rect — no need for an Image+Mask combo where alpha cutoffs can
            // silently hide content.
            var scrollGo = new GameObject("ModListScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRT = (RectTransform)scrollGo.transform;
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(8, 8);
            scrollRT.offsetMax = new Vector2(-8, -82); // top strip is 60 + 10 offset + small gap
            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 30f;

            // Visible right-side scrollbar (auto-hide when content fits)
            SettingsCanvas.AttachVerticalScrollbar(sr, scrollRT);

            // Viewport with RectMask2D
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRT = (RectTransform)viewportGo.transform;
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            sr.viewport = vpRT;

            // Content (vertical layout)
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 0);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(2, 2, 2, 2);
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;  // VLG must control height to read LayoutElement.preferredHeight
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.content = _content;
        }

        private void BuildSearchBox(Transform parent, RectTransform topRT)
        {
            // Pin to bottom of the top strip — 32px tall, full width.
            // Hierarchy mirrors FF's working inputs (Trading Post BuyText_Input):
            //   SearchBox (Image + TMP_InputField)
            //     └── Text Area (RectMask2D)
            //           ├── Caret (created by TMP_InputField on first focus)
            //           ├── Placeholder (TMP_Text)
            //           └── Text (TMP_Text)
            //
            // The Text Area + RectMask2D is what clips the caret/selection
            // mesh; without it TMP's caret can render outside the box.
            var bgGo = new GameObject("SearchBox", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            // Hold the GO inactive until the TMP_InputField has its
            // textComponent/viewport assigned — see SettingControls.BuildInputField
            // for the why (OnEnable wires the caret subsystem).
            bgGo.SetActive(false);
            var bgRT = (RectTransform)bgGo.transform;
            bgRT.anchorMin = new Vector2(0, 0);
            bgRT.anchorMax = new Vector2(1, 0);
            bgRT.pivot = new Vector2(0.5f, 0);
            bgRT.sizeDelta = new Vector2(-16, 32);
            bgRT.anchoredPosition = new Vector2(0, 0);
            var bgImg = bgGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(bgImg, FFNativeAssets.PanelBorderSimple, FFNativeAssets.PanelTintDefault);

            // Text Area (textViewport) — RectMask2D for caret clipping
            var areaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(bgGo.transform, false);
            var areaRT = (RectTransform)areaGo.transform;
            areaRT.anchorMin = Vector2.zero;
            areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(8, 2);
            areaRT.offsetMax = new Vector2(-8, -2);

            // Placeholder (added before Text so Text renders on top)
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(areaGo.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) ph.font = FFNativeAssets.FontBody;
            ph.fontSize = 13;
            ph.fontStyle = FontStyles.Italic | FontStyles.SmallCaps;
            ph.color = FFNativeAssets.TextMuted;
            ph.text = "Search…";
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            var phRT = ph.rectTransform;
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero;
            phRT.offsetMax = Vector2.zero;

            // Text component
            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(areaGo.transform, false);
            var txt = textGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) txt.font = FFNativeAssets.FontBody;
            txt.fontSize = 13;
            txt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            txt.color = FFNativeAssets.TextPrimary;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            txt.enableWordWrapping = false;
            txt.richText = false;
            txt.overflowMode = TextOverflowModes.Masking;
            var txtRT = txt.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            // The actual TMP_InputField
            var input = bgGo.AddComponent<TMP_InputField>();
            input.textViewport = areaRT;
            input.textComponent = txt;
            input.placeholder = ph;
            input.targetGraphic = bgImg;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            if (txt.font != null)
            {
                input.fontAsset = txt.font;
                input.pointSize = 13;
            }
            input.customCaretColor = true;
            input.caretColor = new Color(0.84f, 0.84f, 0.82f, 1f);
            input.caretWidth = 2;
            input.caretBlinkRate = 0.85f;
            input.selectionColor = new Color(0.86f, 0.83f, 0.73f, 0.75f);
            input.onValueChanged.AddListener(v => { _search = v ?? ""; Refresh(); FireFilterChanged(); });
            _searchInput = input;

            bgGo.SetActive(true);
        }

        private void BuildOnlyChangedToggle(Transform parent, RectTransform topRT)
        {
            // Pin to top of the top strip — 22px tall row, sits above the search.
            var toggleRowGo = new GameObject("OnlyChangedRow", typeof(RectTransform));
            toggleRowGo.transform.SetParent(parent, false);
            var rowRT = (RectTransform)toggleRowGo.transform;
            rowRT.anchorMin = new Vector2(0, 1);
            rowRT.anchorMax = new Vector2(1, 1);
            rowRT.pivot = new Vector2(0.5f, 1);
            rowRT.sizeDelta = new Vector2(-16, 22);
            rowRT.anchoredPosition = new Vector2(0, 0);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(toggleRowGo.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) label.font = FFNativeAssets.FontBody;
            label.fontSize = 11;
            label.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            label.color = FFNativeAssets.TextDim;
            label.text = "Show only changed";
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var labelRT = label.rectTransform;
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(28, 0);
            labelRT.offsetMax = new Vector2(0, 0);

            var checkGo = new GameObject("Check", typeof(RectTransform), typeof(Image), typeof(Toggle), typeof(Button));
            checkGo.transform.SetParent(toggleRowGo.transform, false);
            var checkRT = (RectTransform)checkGo.transform;
            checkRT.anchorMin = new Vector2(0, 0.5f);
            checkRT.anchorMax = new Vector2(0, 0.5f);
            checkRT.pivot = new Vector2(0, 0.5f);
            checkRT.anchoredPosition = new Vector2(4, 0);
            checkRT.sizeDelta = new Vector2(20, 20);

            var checkImg = checkGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(checkImg, FFNativeAssets.PanelBorderSimple, FFNativeAssets.PanelTintDefault);

            // Inner check mark — a small image that toggles visibility
            var markGo = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            markGo.transform.SetParent(checkGo.transform, false);
            var markRT = (RectTransform)markGo.transform;
            markRT.anchorMin = new Vector2(0.5f, 0.5f);
            markRT.anchorMax = new Vector2(0.5f, 0.5f);
            markRT.pivot = new Vector2(0.5f, 0.5f);
            markRT.sizeDelta = new Vector2(12, 12);
            var markImg = markGo.GetComponent<Image>();
            markImg.color = FFNativeAssets.RowActive;

            var toggle = checkGo.GetComponent<Toggle>();
            toggle.isOn = false;
            toggle.targetGraphic = checkImg;
            toggle.graphic = markImg;
            toggle.onValueChanged.AddListener(v => { _onlyChanged = v; Refresh(); FireFilterChanged(); });
            _onlyChangedToggle = toggle;
        }

        public void Refresh()
        {
            // Clear existing rows
            foreach (var r in _rows)
            {
                if (r.Root != null) UnityEngine.Object.Destroy(r.Root);
            }
            _rows.Clear();

            // Sort: Order asc, then DisplayName asc
            var ordered = SettingsRegistry.Mods
                .OrderBy(m => m.Value.Order)
                .ThenBy(m => m.Value.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var kv in ordered)
            {
                var modId = kv.Key;
                var info = kv.Value;
                var entries = SettingsRegistry.ForMod(modId).ToList();
                if (entries.Count == 0) continue;
                if (!MatchesFilter(modId, info, entries)) continue;

                _rows.Add(BuildRow(modId, info, entries));
            }

            // If selected mod no longer in list, reselect first
            if (SelectedModId != null && !_rows.Any(r => r.ModId == SelectedModId))
            {
                Select(_rows.Count > 0 ? _rows[0].ModId : null);
            }
            else
            {
                // Refresh active highlights
                foreach (var r in _rows) r.SetActive(r.ModId == SelectedModId);
            }
        }

        private bool MatchesFilter(string modId, ModSettingsInfo info, List<SettingEntryRecord> entries)
        {
            if (!string.IsNullOrEmpty(_search))
            {
                string s = _search.ToLowerInvariant();
                if ((info.DisplayName ?? "").ToLowerInvariant().Contains(s)) return true;
                if (modId.ToLowerInvariant().Contains(s)) return true;
                foreach (var e in entries)
                {
                    if (e.Key.ToLowerInvariant().Contains(s)) return true;
                    if (!string.IsNullOrEmpty(e.Meta.Label) && e.Meta.Label!.ToLowerInvariant().Contains(s)) return true;
                    if (!string.IsNullOrEmpty(e.VanillaDisplayName) && e.VanillaDisplayName!.ToLowerInvariant().Contains(s)) return true;
                }
                return false;
            }
            if (_onlyChanged)
            {
                foreach (var e in entries)
                {
                    try { if (!Equals(e.Get(), e.DefaultValue)) return true; } catch { }
                }
                return false;
            }
            return true;
        }

        private ModRow BuildRow(string modId, ModSettingsInfo info, List<SettingEntryRecord> entries)
        {
            // Outer container with FF border sprite as background
            var rowGo = new GameObject("ModRow:" + modId, typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            rowGo.transform.SetParent(_content, false);

            var le = rowGo.GetComponent<LayoutElement>();
            le.minHeight = 44;
            le.preferredHeight = 44;

            // Use FF's actual main-menu button sprites:
            // BTN_Border02_UP (default) ↔ BTN_BorderFocus02_UP (selected).
            // These are the same sprites used on the New Settlement / Continue
            // buttons in the title screen, with proper 23,18,23,18 borders so
            // corners scale cleanly at any row size.
            var bgImg = rowGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(bgImg, FFNativeAssets.ButtonGeneric, FFNativeAssets.PanelTintDefault);

            var btn = rowGo.GetComponent<Button>();
            btn.targetGraphic = bgImg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = FFNativeAssets.PanelTintDefault;
            colors.highlightedColor = FFNativeAssets.RowHover;
            colors.pressedColor = new Color(0.85f, 0.78f, 0.62f, 1f);
            colors.selectedColor = FFNativeAssets.PanelTintDefault;
            btn.colors = colors;
            btn.onClick.AddListener(() => Select(modId));

            // Accent stripe — left edge, full height, 4px wide
            var stripeGo = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
            stripeGo.transform.SetParent(rowGo.transform, false);
            var stripeRT = (RectTransform)stripeGo.transform;
            stripeRT.anchorMin = new Vector2(0, 0);
            stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.pivot = new Vector2(0, 0.5f);
            stripeRT.offsetMin = new Vector2(4, 6);
            stripeRT.offsetMax = new Vector2(8, -6);
            stripeRT.sizeDelta = new Vector2(4, 0);
            var stripe = stripeGo.GetComponent<Image>();
            stripe.color = info.AccentColor ?? new Color(0.55f, 0.45f, 0.30f, 1f);

            // Text container, anchored to fill row except for accent stripe
            var textWrapGo = new GameObject("TextWrap", typeof(RectTransform));
            textWrapGo.transform.SetParent(rowGo.transform, false);
            var twRT = (RectTransform)textWrapGo.transform;
            twRT.anchorMin = new Vector2(0, 0);
            twRT.anchorMax = new Vector2(1, 1);
            twRT.offsetMin = new Vector2(16, 4);
            twRT.offsetMax = new Vector2(-8, -4);

            // Mod name
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(textWrapGo.transform, false);
            var nameTxt = nameGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) nameTxt.font = FFNativeAssets.FontBody;
            nameTxt.text = Localization.KcLoc.Tr(modId + "/meta/name", info.DisplayName ?? modId);
            nameTxt.fontSize = 14;
            nameTxt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            nameTxt.color = FFNativeAssets.TextPrimary;
            var nameRT = nameTxt.rectTransform;
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.offsetMin = new Vector2(0, 0);
            nameRT.offsetMax = new Vector2(0, -2);
            nameTxt.alignment = TextAlignmentOptions.BottomLeft;

            // Settings count subtitle
            var subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(textWrapGo.transform, false);
            var subTxt = subGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) subTxt.font = FFNativeAssets.FontBody;
            int changedCount = entries.Count(e => { try { return !Equals(e.Get(), e.DefaultValue); } catch { return false; } });
            subTxt.text = changedCount > 0
                ? $"{entries.Count} settings  ({changedCount} changed)"
                : $"{entries.Count} settings";
            subTxt.fontSize = 11;
            subTxt.fontStyle = FontStyles.SmallCaps;
            subTxt.color = changedCount > 0 ? FFNativeAssets.TextChanged : FFNativeAssets.TextDim;
            var subRT = subTxt.rectTransform;
            subRT.anchorMin = new Vector2(0, 0);
            subRT.anchorMax = new Vector2(1, 0.5f);
            subRT.offsetMin = new Vector2(0, 2);
            subRT.offsetMax = new Vector2(0, 0);
            subTxt.alignment = TextAlignmentOptions.TopLeft;

            return new ModRow
            {
                ModId = modId,
                Root = rowGo,
                Background = bgImg,
                Stripe = stripe,
                NameText = nameTxt,
            };
        }

        private void FireFilterChanged()
        {
            try { OnFilterChanged?.Invoke(); }
            catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings/2C] OnFilterChanged handler threw: {ex.Message}"); }
        }

        public void Select(string? modId)
        {
            if (SelectedModId == modId) return;
            SelectedModId = modId;
            foreach (var r in _rows) r.SetActive(r.ModId == modId);
            try { OnModSelected?.Invoke(modId); }
            catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings/2C] OnModSelected handler threw: {ex.Message}"); }
        }

        private class ModRow
        {
            public string ModId = "";
            public GameObject Root = null!;
            public Image Background = null!;
            public Image Stripe = null!;
            public TMP_Text NameText = null!;

            public void SetActive(bool active)
            {
                if (Background == null) return;
                // Swap sprite to match FF's main menu: focused row uses
                // BTN_BorderFocus02_UP (highlighted), inactive uses BTN_Border02_UP.
                var sprite = active ? FFNativeAssets.ButtonFocusSecondary : FFNativeAssets.ButtonGeneric;
                if (sprite != null)
                {
                    Background.sprite = sprite;
                    Background.type = Image.Type.Sliced;
                }
                Background.color = FFNativeAssets.PanelTintDefault;
            }
        }
    }
}
