using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// Right pane — shows the detail view for a single mod. Stage 2C: header
    /// (mod name + accent stripe + description + version) and per-category
    /// section placeholders. Stage 2D fills the sections with real controls.
    /// </summary>
    public class ModDetailPanel
    {
        private RectTransform _root = null!;
        private RectTransform _content = null!;
        private RectTransform _accentStripe = null!;
        private TMP_Text _nameText = null!;
        private TMP_Text _versionText = null!;
        private TMP_Text _descText = null!;
        private string? _currentModId;
        private ScrollRect? _scrollRect;

        /// <summary>
        /// Optional predicate that says whether the panel should currently show
        /// only entries differing from their defaults. Wired by SettingsCanvas
        /// from <see cref="ModListPanel.OnlyChanged"/>.
        /// </summary>
        public Func<bool>? OnlyChangedProvider;

        public void Build(RectTransform parent)
        {
            _root = parent;

            // Header strip — accent stripe + mod name + version + description
            var headerGo = new GameObject("DetailHeader", typeof(RectTransform));
            headerGo.transform.SetParent(parent, false);
            var headerRT = (RectTransform)headerGo.transform;
            headerRT.anchorMin = new Vector2(0, 1);
            headerRT.anchorMax = new Vector2(1, 1);
            headerRT.pivot = new Vector2(0.5f, 1);
            headerRT.sizeDelta = new Vector2(0, 92);
            headerRT.anchoredPosition = new Vector2(0, 0);

            // Accent stripe — top-left, full height of header, 6px wide
            var stripeGo = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
            stripeGo.transform.SetParent(headerGo.transform, false);
            var stripeRT = (RectTransform)stripeGo.transform;
            stripeRT.anchorMin = new Vector2(0, 0);
            stripeRT.anchorMax = new Vector2(0, 1);
            stripeRT.pivot = new Vector2(0, 0.5f);
            stripeRT.offsetMin = new Vector2(0, 8);
            stripeRT.offsetMax = new Vector2(6, -8);
            stripeRT.sizeDelta = new Vector2(6, 0);
            _accentStripe = stripeRT;
            var stripeImg = stripeGo.GetComponent<Image>();
            stripeImg.color = new Color(0.55f, 0.45f, 0.30f, 1f);

            // Name + version row
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(headerGo.transform, false);
            _nameText = nameGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontHeader != null) _nameText.font = FFNativeAssets.FontHeader;
            _nameText.text = "";
            _nameText.fontSize = 24;
            _nameText.color = FFNativeAssets.TextPrimary;
            _nameText.alignment = TextAlignmentOptions.BottomLeft;
            var nameRT = _nameText.rectTransform;
            nameRT.anchorMin = new Vector2(0, 1);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.pivot = new Vector2(0, 1);
            nameRT.sizeDelta = new Vector2(0, 32);
            nameRT.anchoredPosition = new Vector2(20, -4);

            // Version (right-aligned, dim)
            var verGo = new GameObject("Version", typeof(RectTransform));
            verGo.transform.SetParent(headerGo.transform, false);
            _versionText = verGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) _versionText.font = FFNativeAssets.FontBody;
            _versionText.text = "";
            _versionText.fontSize = 12;
            _versionText.fontStyle = FontStyles.SmallCaps;
            _versionText.color = FFNativeAssets.TextDim;
            _versionText.alignment = TextAlignmentOptions.BottomRight;
            var verRT = _versionText.rectTransform;
            verRT.anchorMin = new Vector2(0, 1);
            verRT.anchorMax = new Vector2(1, 1);
            verRT.pivot = new Vector2(1, 1);
            verRT.sizeDelta = new Vector2(0, 32);
            verRT.anchoredPosition = new Vector2(-8, -8);

            // Description
            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(headerGo.transform, false);
            _descText = descGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) _descText.font = FFNativeAssets.FontBody;
            _descText.text = "";
            _descText.fontSize = 13;
            _descText.fontStyle = FontStyles.Italic | FontStyles.SmallCaps;
            _descText.color = FFNativeAssets.TextDim;
            _descText.enableWordWrapping = true;
            _descText.alignment = TextAlignmentOptions.TopLeft;
            var descRT = _descText.rectTransform;
            descRT.anchorMin = new Vector2(0, 0);
            descRT.anchorMax = new Vector2(1, 1);
            descRT.pivot = new Vector2(0, 1);
            descRT.offsetMin = new Vector2(20, 8);
            descRT.offsetMax = new Vector2(-12, -36);

            // Body — scrollable list of category sections
            var scrollGo = new GameObject("DetailScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRT = (RectTransform)scrollGo.transform;
            scrollRT.anchorMin = new Vector2(0, 0);
            scrollRT.anchorMax = new Vector2(1, 1);
            scrollRT.offsetMin = new Vector2(0, 0);
            scrollRT.offsetMax = new Vector2(0, -100);
            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.scrollSensitivity = 30f;
            _scrollRect = sr;

            // Visible right-side scrollbar (auto-hide when content fits)
            SettingsCanvas.AttachVerticalScrollbar(sr, scrollRT);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRT = (RectTransform)viewportGo.transform;
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            sr.viewport = vpRT;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 0);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(8, 8, 8, 16);
            vlg.spacing = 12;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            // Outer VLG must NOT control height — sections size themselves via
            // ContentSizeFitter, and childControlHeight=true here would fight
            // the fitter and collapse sections to LayoutElement.minHeight,
            // making category groups look like one flat alphabetical list.
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            sr.content = _content;
        }

        public void ShowMod(string? modId)
        {
            _currentModId = modId;

            // Blur any focused TMP_InputField and clear the EventSystem
            // selection BEFORE destroying the old content. Destroying a field
            // while it's the EventSystem's currentSelectedGameObject wedges
            // TMP's caret/selection rendering for every field built afterward
            // — input still routes (typing/Del work) but the caret and the
            // selection highlight stop drawing. This is why the fields render
            // a caret on first open but go invisible after the first rebuild
            // (mod switch or setting change → ShowMod). Deactivating + clearing
            // selection lets the destroyed field clean up its caret subsystem.
            var es = EventSystem.current;
            if (es != null)
            {
                var sel = es.currentSelectedGameObject;
                if (sel != null)
                {
                    var input = sel.GetComponent<TMP_InputField>();
                    if (input != null) input.DeactivateInputField();
                }
                es.SetSelectedGameObject(null);
            }

            // Clear existing category sections. Detach FIRST (instant), then
            // Destroy (queued for end-of-frame). Without the detach, freshly-
            // built sections render alongside the soon-to-be-destroyed old
            // ones for a frame, and lingering TMP_InputFields can show stale
            // text — which broke the reset arrow on text fields.
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                var child = _content.GetChild(i);
                child.SetParent(null, false);
                UnityEngine.Object.Destroy(child.gameObject);
            }

            if (string.IsNullOrEmpty(modId) || !SettingsRegistry.Mods.TryGetValue(modId!, out var info))
            {
                _nameText.text = "";
                _versionText.text = "";
                _descText.text = "Select a mod from the left to configure.";
                _accentStripe.GetComponent<Image>().color = new Color(0.40f, 0.36f, 0.30f, 1f);
                return;
            }

            _nameText.text = info.DisplayName ?? modId;
            _versionText.text = string.IsNullOrEmpty(info.Version) ? "" : "v" + info.Version;
            _descText.text = info.Description ?? "";
            _accentStripe.GetComponent<Image>().color = info.AccentColor ?? new Color(0.55f, 0.45f, 0.30f, 1f);

            bool onlyChanged = OnlyChangedProvider?.Invoke() ?? false;

            // Build a section per category. Skip categories whose entries are
            // all unchanged when "only changed" is on.
            foreach (var grp in SettingsRegistry.ByCategory(modId!))
            {
                var entries = grp.OrderBy(e => e.Meta.Order).ThenBy(e => e.Key);
                var visible = onlyChanged
                    ? entries.Where(IsChanged).ToList()
                    : entries.ToList();
                if (visible.Count == 0) continue;
                BuildSection(grp.Key, visible);
            }
        }

        /// <summary>Re-render the current mod's detail view (used when the
        /// "only changed" filter toggles from outside, or after a reset).
        /// Preserves the scroll position so a Reset deep in the list doesn't
        /// snap the user back to the top.</summary>
        public void Refresh()
        {
            float scrollPos = _scrollRect != null ? _scrollRect.verticalNormalizedPosition : 1f;
            ShowMod(_currentModId);
            if (_scrollRect != null)
            {
                // Force a complete canvas-wide layout sync so every section's
                // ContentSizeFitter has computed before we set position.
                // ForceRebuildLayoutImmediate alone only does one pass on the
                // target rect, missing nested fitters/VLGs.
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(scrollPos);
                // Second pass: setting position may have shifted things; rebuild
                // once more to settle visuals in the same frame.
                Canvas.ForceUpdateCanvases();
            }
        }

        private static bool IsChanged(SettingEntryRecord e)
        {
            try { return !Equals(e.Get(), e.DefaultValue); } catch { return false; }
        }

        private void BuildSection(string categoryName, System.Collections.Generic.List<SettingEntryRecord> entries)
        {
            // Section container
            var secGo = new GameObject("Section:" + categoryName, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            secGo.transform.SetParent(_content, false);
            var secImg = secGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(secImg, FFNativeAssets.PanelBorderLight, FFNativeAssets.PanelTintDefault);

            var le = secGo.GetComponent<LayoutElement>();
            le.minHeight = 64;
            var fitter = secGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = secGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 8, 8);
            vlg.spacing = 2;
            vlg.childControlWidth = true;
            // childControlHeight MUST be true — otherwise Unity ignores
            // LayoutElement.preferredHeight and falls back to RectTransform
            // sizeDelta which defaults to 100, making every row ~100px tall.
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Section header — category name + entry count
            var hdrGo = new GameObject("CategoryHeader", typeof(RectTransform), typeof(LayoutElement));
            hdrGo.transform.SetParent(secGo.transform, false);
            var hdrLE = hdrGo.GetComponent<LayoutElement>();
            hdrLE.minHeight = 28;
            hdrLE.preferredHeight = 28;
            var hdrTxt = hdrGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontHeader != null) hdrTxt.font = FFNativeAssets.FontHeader;
            hdrTxt.text = categoryName;
            hdrTxt.fontSize = 16;
            hdrTxt.color = FFNativeAssets.TextPrimary;
            hdrTxt.alignment = TextAlignmentOptions.MidlineLeft;

            // Phase 2C placeholder line listing the entries (with restart-required marker)
            foreach (var e in entries)
            {
                BuildEntryRow(secGo.transform, e);
            }
        }

        private void BuildEntryRow(Transform parent, SettingEntryRecord e)
        {
            // Skip rows whose VisibleWhen predicate says hide.
            if (e.Meta.VisibleWhen != null)
            {
                bool ok = false;
                try { ok = e.Meta.VisibleWhen(); } catch { ok = true; }
                if (!ok) return;
            }

            // Row container — fixed height, holds: [restart icon | label | control | reset]
            // 44 tall (was 28) to accommodate the bigger slider hitbox.
            var rowGo = new GameObject("Entry:" + e.Key, typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(parent, false);
            var rowLE = rowGo.GetComponent<LayoutElement>();
            rowLE.minHeight = 44;
            rowLE.preferredHeight = 44;

            const float ICON_W = 22f;
            const float LABEL_W = 260f;
            const float RESET_W = 28f;
            float indent = Mathf.Max(0, e.Meta.Indent); // left padding for nested rows

            // Restart-required indicator (left column, 22px wide). Indent shifts
            // it right so the whole row visually nests under its master toggle.
            var iconGo = new GameObject("RestartIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rowGo.transform, false);
            var iconRT = (RectTransform)iconGo.transform;
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.pivot = new Vector2(0, 0.5f);
            iconRT.anchoredPosition = new Vector2(indent, 0);
            iconRT.sizeDelta = new Vector2(18, 18);
            var iconImg = iconGo.GetComponent<Image>();
            if (e.Meta.RestartRequired && FFNativeAssets.IconWarning != null)
            {
                // Amber: a full game restart is required (e.g. one-time writes at mod init).
                iconImg.sprite = FFNativeAssets.IconWarning;
                iconImg.color = FFNativeAssets.WarningAmber;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = true;
                var hover = iconGo.AddComponent<HoverTooltip>();
                hover.Body = "Requires a full game restart to take effect";
            }
            else if (e.Meta.ReloadRequired && FFNativeAssets.IconWarning != null)
            {
                // Cyan: takes effect on the next save reload / building placement, not live.
                iconImg.sprite = FFNativeAssets.IconWarning;
                iconImg.color = new Color(0.45f, 0.75f, 0.95f, 1f);
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = true;
                var hover = iconGo.AddComponent<HoverTooltip>();
                hover.Body = "Not live — reload your save (or place a new building) to apply";
            }
            else
            {
                iconImg.color = new Color(0, 0, 0, 0); // invisible spacer
            }

            // Label column
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRT = (RectTransform)labelGo.transform;
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(0, 1);
            labelRT.pivot = new Vector2(0, 0.5f);
            labelRT.offsetMin = new Vector2(ICON_W + 6 + indent, 0);
            labelRT.offsetMax = new Vector2(0, 0);
            labelRT.sizeDelta = new Vector2(LABEL_W, 0);
            var labelTxt = labelGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) labelTxt.font = FFNativeAssets.FontBody;
            labelTxt.text = e.Meta.Label ?? e.VanillaDisplayName ?? e.Key;
            labelTxt.fontSize = 13;
            labelTxt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            labelTxt.color = FFNativeAssets.TextPrimary;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            labelTxt.enableWordWrapping = false;
            labelTxt.overflowMode = TextOverflowModes.Ellipsis;

            // Hover tooltip on the label — uses Meta.Tooltip if set, falls
            // back to the entry's vanilla MelonPreferences description.
            string? tip = !string.IsNullOrEmpty(e.Meta.Tooltip) ? e.Meta.Tooltip
                       : !string.IsNullOrEmpty(e.VanillaDescription) ? e.VanillaDescription
                       : null;
            if (!string.IsNullOrEmpty(tip))
            {
                labelTxt.raycastTarget = true;
                var hover = labelGo.AddComponent<HoverTooltip>();
                hover.Body = tip;
            }
            else
            {
                labelTxt.raycastTarget = false;
            }

            // Control column (between label and reset button)
            var controlGo = new GameObject("Control", typeof(RectTransform));
            controlGo.transform.SetParent(rowGo.transform, false);
            var controlRT = (RectTransform)controlGo.transform;
            controlRT.anchorMin = new Vector2(0, 0);
            controlRT.anchorMax = new Vector2(1, 1);
            controlRT.offsetMin = new Vector2(ICON_W + 6 + LABEL_W + 12 + indent, 4);
            controlRT.offsetMax = new Vector2(-(RESET_W + 8), -4);

            // Reset button — uses FF's BTN_BackRound01_UP (the circular
            // orange reload-arrow icon used as the back button on the New
            // Settlement screen). Visible only when the entry differs from
            // its default.
            var resetBtn = SettingsCanvas.NewSpriteButton("Reset",
                rowGo.transform,
                FFNativeAssets.ResetIcon,
                () => ResetEntry(e));
            var resetRT = resetBtn.GetComponent<RectTransform>();
            resetRT.anchorMin = new Vector2(1, 0.5f);
            resetRT.anchorMax = new Vector2(1, 0.5f);
            resetRT.pivot = new Vector2(1, 0.5f);
            resetRT.sizeDelta = new Vector2(28, 28);
            resetRT.anchoredPosition = new Vector2(-2, 0);
            resetBtn.SetActive(IsChanged(e));

            // Build the actual control widget. Pass back a callback that
            // refreshes the reset button visibility.
            Action onChanged = () =>
            {
                if (resetBtn != null) resetBtn.SetActive(IsChanged(e));
            };
            SettingControls.Build(e, controlRT, onChanged);
        }

        private void ResetEntry(SettingEntryRecord e)
        {
            object? before = null;
            try { before = e.Get(); } catch { }

            // If the user had typed into a TMP_InputField and never tabbed
            // away, force-commit the edit so the deselect's onEndEdit can't
            // overwrite the value we're about to set.
            var es = EventSystem.current;
            if (es != null && es.currentSelectedGameObject != null)
            {
                var input = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
                if (input != null) input.DeactivateInputField();
            }

            // Revert behavior: if a session-start value is captured for this
            // entry, restore THAT (the user's saved value at panel-open). Fall
            // back to the factory default only if no session-start exists.
            object? target = e.DefaultValue;
            if (SettingsRegistry.TryGetSessionStartValue(e.ModId, e.Key, out var sessionStart))
                target = sessionStart;

            try
            {
                e.Set(target);
                SettingsRegistry.NotifyChanged(e, target);
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"[Settings/2D] Reset failed for {e.Key}: {ex.Message}");
            }

            object? after = null;
            try { after = e.Get(); } catch { }
            try { FFUIOverhaulMod.Log.Msg($"[Settings] Reset clicked: {e.ModId}::{e.Key}  before={before}  sessionStart={sessionStart}  factoryDefault={e.DefaultValue}  after={after}"); } catch { }

            Refresh();
        }
    }

}
