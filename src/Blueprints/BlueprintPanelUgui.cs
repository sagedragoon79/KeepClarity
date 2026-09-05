using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FFUIOverhaul.Settings.UI;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// The blueprint library, in FF's own visual language — panel chrome, fonts
    /// and button sprites pulled from FFNativeAssets so it sits alongside the mod
    /// manager and the other Keep Clarity overlays instead of looking bolted on.
    ///
    /// Replaces the IMGUI panel that carried M1–M3. That one was deliberate
    /// scaffolding: it let the library and the stamp workflow be built and tested
    /// before any effort went into chrome, and because presentation was confined
    /// to one file, this swap touches nothing in BlueprintStore, BlueprintCapture
    /// or BlueprintStamp.
    ///
    /// The IMGUI panel is kept as a runtime fallback (see BlueprintPanel): if this
    /// canvas fails to build on some machine, the feature degrades to a plain
    /// window rather than disappearing.
    ///
    /// Structure, mirroring BuildQueueOverlay's idiom:
    ///   Canvas (ScreenSpaceOverlay, ConstantPixelSize)
    ///     └ Panel                    FF border chrome, draggable by its header
    ///        ├ Header                title + close
    ///        ├ Capture summary       what's on the clipboard
    ///        ├ Name row              input + Save
    ///        ├ Copy / Paste row      arms capture / stamp
    ///        ├ ScrollRect            saved blueprints, one row each
    ///        └ Footer                status line + folder/refresh
    /// </summary>
    internal class BlueprintPanelUgui
    {
        // Above FF's window canvas (10) so the library isn't buried, below KC's
        // settings canvas (30) so the mod manager still wins.
        private const int CanvasSortingOrder = 20;
        private const float PanelWidth = 420f;
        private const float PanelHeight = 520f;
        private const float RowHeight = 46f;

        private static readonly Color PanelBg = new Color(0.09f, 0.09f, 0.10f, 0.96f);
        private static readonly Color Ink = new Color(0.93f, 0.90f, 0.82f, 1f);
        private static readonly Color InkDim = new Color(0.72f, 0.69f, 0.62f, 1f);
        private static readonly Color Accent = new Color(0.98f, 0.82f, 0.42f, 1f);
        private static readonly Color ButtonNormal = new Color(0.20f, 0.19f, 0.17f, 1f);
        private static readonly Color RowBg = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color RowBgSelected = new Color(0.98f, 0.82f, 0.42f, 0.16f);

        private GameObject? _canvasRoot;
        private GameObject? _panel;
        private GameObject? _listContent;
        private TMP_InputField? _nameInput;
        private TextMeshProUGUI? _captureLabel;
        private TextMeshProUGUI? _statusLabel;
        private TextMeshProUGUI? _copyLabel;
        private TextMeshProUGUI? _pasteLabel;

        private readonly List<GameObject> _rows = new List<GameObject>();
        private string _status = "";
        private float _statusUntil;
        private string? _confirmDelete;
        private Blueprint? _suggestedFor;
        private int _lastListStamp = -1;

        public bool IsOpen => _canvasRoot != null && _canvasRoot.activeSelf;

        /// <summary>The blueprint chosen to stamp.</summary>
        public Blueprint? Selected { get; private set; }

        /// <summary>Cursor over the panel — world clicks must be ignored there.
        /// uGUI does raycast properly, but the capture/stamp modules read raw
        /// mouse input, so they need an explicit test.</summary>
        public bool PointerOverPanel
        {
            get
            {
                if (!IsOpen || _panel == null) return false;
                var rt = (RectTransform)_panel.transform;
                return RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
            }
        }

        // ── lifecycle ───────────────────────────────────────────────────────

        public void Toggle()
        {
            if (IsOpen) { Close(); return; }
            EnsureBuilt();
            if (_canvasRoot == null) return;
            BlueprintStore.Invalidate();
            _confirmDelete = null;
            _canvasRoot.SetActive(true);
            RefreshAll();
        }

        public void Close()
        {
            _confirmDelete = null;
            if (_canvasRoot != null) _canvasRoot.SetActive(false);
        }

        public void Destroy()
        {
            if (_canvasRoot != null) UnityEngine.Object.Destroy(_canvasRoot);
            _canvasRoot = null; _panel = null; _listContent = null;
            _nameInput = null; _captureLabel = null; _statusLabel = null;
            _rows.Clear();
        }

        /// <summary>Per-frame upkeep: pick up a fresh capture, expire the status
        /// line, and keep the Copy/Paste labels honest about armed state.</summary>
        public void Tick()
        {
            if (!IsOpen) return;

            var clip = BlueprintCapture.Clipboard;
            if (clip != null && !ReferenceEquals(clip, _suggestedFor))
            {
                _suggestedFor = clip;
                if (_nameInput != null && string.IsNullOrEmpty(_nameInput.text))
                    _nameInput.text = SuggestName(clip);
                RefreshCaptureLabel();
            }

            if (_copyLabel != null)
                _copyLabel.text = BlueprintCapture.IsArmed ? "Copying…" : "Copy";
            if (_pasteLabel != null)
                _pasteLabel.text = BlueprintStamp.IsArmed ? "Pasting…" : "Paste";

            if (_statusLabel != null && _status.Length > 0 && Time.unscaledTime > _statusUntil)
            {
                _status = "";
                RefreshStatus();
            }
        }

        // ── build ───────────────────────────────────────────────────────────

        private void EnsureBuilt()
        {
            if (_canvasRoot != null) return;

            FFNativeAssets.EnsureProbed();

            _canvasRoot = new GameObject("FFUI_BlueprintLibrary",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_canvasRoot);

            var canvas = _canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortingOrder;
            var scaler = _canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _panel = NewChild(_canvasRoot, "Panel");
            var prt = (RectTransform)_panel.transform;
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            prt.anchoredPosition = new Vector2(-320f, 0f);
            ApplyFFChrome(_panel);

            var vlg = _panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 10);
            vlg.spacing = 6;
            vlg.childForceExpandWidth = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;

            BuildHeader(_panel);
            BuildCaptureRow(_panel);
            BuildNameRow(_panel);
            BuildActionRow(_panel);
            BuildList(_panel);
            BuildFooter(_panel);
        }

        private void BuildHeader(GameObject parent)
        {
            var header = NewChild(parent, "Header");
            header.AddComponent<LayoutElement>().preferredHeight = 30f;
            var hlg = header.AddComponent<HorizontalLayoutGroup>();
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // A transparent graphic gives the header something to receive the drag.
            var grab = header.AddComponent<Image>();
            grab.color = new Color(1f, 1f, 1f, 0.02f);
            grab.raycastTarget = true;
            // DraggablePanel resolves its own canvas via GetComponentInParent;
            // only the move target needs setting.
            var drag = header.AddComponent<UI.DraggablePanel>();
            drag.Target = (RectTransform)_panel!.transform;

            var title = NewText(header, "Title", "Blueprints", 19f, FontStyles.Bold, Accent,
                TextAlignmentOptions.Left);
            title.GetComponent<LayoutElement>().flexibleWidth = 1f;
            if (FFNativeAssets.FontTitle != null) title.font = FFNativeAssets.FontTitle;

            NewButton(header, "Close", "✕", 32f, Close);
        }

        private void BuildCaptureRow(GameObject parent)
        {
            _captureLabel = NewText(parent, "CaptureSummary", "", 13f, FontStyles.Italic, InkDim,
                TextAlignmentOptions.Left);
            _captureLabel.GetComponent<LayoutElement>().preferredHeight = 18f;
        }

        private void BuildNameRow(GameObject parent)
        {
            var row = NewChild(parent, "NameRow");
            row.AddComponent<LayoutElement>().preferredHeight = 30f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var fieldGo = NewChild(row, "NameField");
            var fle = fieldGo.AddComponent<LayoutElement>();
            fle.flexibleWidth = 1f;
            var fieldBg = fieldGo.AddComponent<Image>();
            fieldBg.color = new Color(0f, 0f, 0f, 0.45f);
            if (FFNativeAssets.PanelBorderSimple != null)
            {
                fieldBg.sprite = FFNativeAssets.PanelBorderSimple;
                fieldBg.type = Image.Type.Sliced;
                fieldBg.color = new Color(1f, 1f, 1f, 0.85f);
            }

            // TMP_InputField needs a text child and a viewport-ish area; keep it
            // minimal — single line, no placeholder styling beyond dim ink.
            var textArea = NewChild(fieldGo, "Text");
            var tart = (RectTransform)textArea.transform;
            tart.anchorMin = Vector2.zero; tart.anchorMax = Vector2.one;
            tart.offsetMin = new Vector2(8f, 2f); tart.offsetMax = new Vector2(-8f, -2f);
            var textComp = textArea.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 14f;
            textComp.color = Ink;
            textComp.alignment = TextAlignmentOptions.Left;
            textComp.font = GetGameFont();

            var placeholderGo = NewChild(fieldGo, "Placeholder");
            var prt2 = (RectTransform)placeholderGo.transform;
            prt2.anchorMin = Vector2.zero; prt2.anchorMax = Vector2.one;
            prt2.offsetMin = new Vector2(8f, 2f); prt2.offsetMax = new Vector2(-8f, -2f);
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.text = "Blueprint name…";
            placeholder.fontSize = 14f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(0.6f, 0.58f, 0.52f, 0.8f);
            placeholder.alignment = TextAlignmentOptions.Left;
            placeholder.font = GetGameFont();

            _nameInput = fieldGo.AddComponent<TMP_InputField>();
            _nameInput.textViewport = (RectTransform)textArea.transform;
            _nameInput.textComponent = textComp;
            _nameInput.placeholder = placeholder;
            _nameInput.lineType = TMP_InputField.LineType.SingleLine;
            _nameInput.characterLimit = 64;
            _nameInput.onSubmit.AddListener(_ => DoSave());

            NewButton(row, "Save", "Save", 74f, DoSave);
        }

        private void BuildActionRow(GameObject parent)
        {
            var row = NewChild(parent, "ActionRow");
            row.AddComponent<LayoutElement>().preferredHeight = 32f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            _copyLabel = NewButton(row, "Copy", "Copy", 0f, () =>
            {
                BlueprintCapture.ToggleArmed();
                SetStatus(BlueprintCapture.IsArmed
                    ? "Click a corner on the map, then click again."
                    : "Capture cancelled.");
            });

            _pasteLabel = NewButton(row, "Paste", "Paste", 0f, () =>
            {
                BlueprintStamp.Toggle();
                SetStatus(BlueprintStamp.IsArmed
                    ? "Aim on the map. Tab rotates, click places."
                    : "Stamp cancelled.");
            });
        }

        private void BuildList(GameObject parent)
        {
            var scrollGo = NewChild(parent, "ListScroll");
            var sle = scrollGo.AddComponent<LayoutElement>();
            sle.flexibleHeight = 1f;
            var scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = new Color(0f, 0f, 0f, 0.25f);
            scrollGo.AddComponent<RectMask2D>();

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            _listContent = NewChild(scrollGo, "Content");
            var crt = (RectTransform)_listContent.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            // Width comes from the stretch anchors; a code-created RectTransform
            // defaults to 100x100, which would leave the list narrower than its
            // viewport.
            crt.sizeDelta = new Vector2(0f, 0f);

            var clg = _listContent.AddComponent<VerticalLayoutGroup>();
            clg.padding = new RectOffset(4, 4, 4, 4);
            clg.spacing = 4;
            clg.childForceExpandWidth = true;
            clg.childControlWidth = true;
            clg.childForceExpandHeight = false;
            clg.childControlHeight = true;
            var fitter = _listContent.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)scrollGo.transform;
            scroll.content = crt;
        }

        private void BuildFooter(GameObject parent)
        {
            _statusLabel = NewText(parent, "Status", "", 12f, FontStyles.Italic, InkDim,
                TextAlignmentOptions.Left);
            _statusLabel.GetComponent<LayoutElement>().preferredHeight = 18f;

            var row = NewChild(parent, "Footer");
            row.AddComponent<LayoutElement>().preferredHeight = 28f;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.childForceExpandWidth = true;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            NewButton(row, "Folder", "Open Folder", 0f, () =>
            {
                try { Application.OpenURL("file://" + BlueprintStore.Directory()); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[Blueprints] " + e.Message); }
            });
            NewButton(row, "Refresh", "Refresh", 0f, () =>
            {
                BlueprintStore.Invalidate();
                RefreshList();
                SetStatus("Refreshed.");
            });
        }

        // ── content ─────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshCaptureLabel();
            RefreshList();
            RefreshStatus();
        }

        private void RefreshCaptureLabel()
        {
            if (_captureLabel == null) return;
            var clip = BlueprintCapture.Clipboard;
            _captureLabel.text = clip == null
                ? "Nothing captured yet — press Copy, then drag a box on the map."
                : "Captured: " + clip.Summary();
        }

        private void RefreshStatus()
        {
            if (_statusLabel == null) return;
            if (_status.Length > 0) { _statusLabel.text = _status; return; }
            _statusLabel.text = Selected != null
                ? "Ready to stamp: " + Selected.name
                : "Select a blueprint to stamp.";
        }

        private void SetStatus(string s)
        {
            _status = s;
            _statusUntil = Time.unscaledTime + 4f;
            RefreshStatus();
        }

        private void RefreshList()
        {
            if (_listContent == null) return;

            foreach (var r in _rows) if (r != null) UnityEngine.Object.Destroy(r);
            _rows.Clear();

            var all = BlueprintStore.All();
            _lastListStamp = all.Count;

            if (all.Count == 0)
            {
                var empty = NewText(_listContent, "Empty",
                    "No blueprints yet.\nCapture a layout, name it, and press Save.",
                    13f, FontStyles.Italic, InkDim, TextAlignmentOptions.Center);
                empty.GetComponent<LayoutElement>().preferredHeight = 48f;
                _rows.Add(empty.gameObject);
                return;
            }

            foreach (var bp in all) _rows.Add(BuildRow(bp));
        }

        private GameObject BuildRow(Blueprint bp)
        {
            bool selected = Selected != null && Selected.name == bp.name;

            var row = NewChild(_listContent!, "Row_" + bp.name);
            row.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var bg = row.AddComponent<Image>();
            bg.color = selected ? RowBgSelected : RowBg;

            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 6, 4, 4);
            hlg.spacing = 6;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Name + summary stacked, taking the free width.
            var info = NewChild(row, "Info");
            info.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var ivlg = info.AddComponent<VerticalLayoutGroup>();
            ivlg.spacing = 0;
            ivlg.childForceExpandHeight = false;
            ivlg.childControlHeight = true;
            ivlg.childForceExpandWidth = true;
            ivlg.childControlWidth = true;

            var nameText = NewText(info, "Name", (selected ? "▸ " : "") + bp.name,
                15f, selected ? FontStyles.Bold : FontStyles.Normal,
                selected ? Accent : Ink, TextAlignmentOptions.Left);
            nameText.GetComponent<LayoutElement>().preferredHeight = 20f;

            var sub = NewText(info, "Summary", bp.Summary(), 12f, FontStyles.Normal, InkDim,
                TextAlignmentOptions.Left);
            sub.GetComponent<LayoutElement>().preferredHeight = 16f;

            NewButton(row, "Select", selected ? "Selected" : "Select", 80f, () =>
            {
                Selected = bp;
                _confirmDelete = null;
                SetStatus($"'{bp.name}' selected.");
                RefreshList();
            });

            bool confirming = _confirmDelete == bp.name;
            NewButton(row, "Delete", confirming ? "Sure?" : "✕", confirming ? 56f : 30f, () =>
            {
                if (_confirmDelete == bp.name)
                {
                    if (BlueprintStore.Delete(bp.name))
                    {
                        if (Selected != null && Selected.name == bp.name) Selected = null;
                        SetStatus($"Deleted '{bp.name}'.");
                    }
                    _confirmDelete = null;
                }
                else _confirmDelete = bp.name;
                RefreshList();
            });

            return row;
        }

        private void DoSave()
        {
            var clip = BlueprintCapture.Clipboard;
            if (clip == null) { SetStatus("Nothing captured to save."); return; }
            string name = _nameInput != null ? _nameInput.text : "";
            if (string.IsNullOrEmpty(name)) { SetStatus("Give the blueprint a name first."); return; }

            bool overwrite = BlueprintStore.Exists(name);
            if (BlueprintStore.Save(clip, name))
            {
                SetStatus(overwrite ? $"Overwrote '{name}'." : $"Saved '{name}'.");
                if (_nameInput != null) _nameInput.text = "";
                RefreshList();
            }
            else SetStatus("Save failed — see the log.");
        }

        private static string SuggestName(Blueprint bp)
        {
            try
            {
                var counts = new Dictionary<string, int>();
                foreach (var e in bp.entries)
                {
                    counts.TryGetValue(e.id, out int n);
                    counts[e.id] = n + 1;
                }
                string best = ""; int bestN = 0;
                foreach (var kv in counts) if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
                return bestN > 1 ? $"{best} x{bestN}" : best;
            }
            catch { return ""; }
        }

        // ── small builders (same shape as the other KC overlays) ────────────

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
            go.AddComponent<LayoutElement>();
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = fontSize; t.fontStyle = style;
            t.color = color; t.alignment = align; t.raycastTarget = false;
            t.font = GetGameFont();
            try { t.outlineWidth = 0.16f; t.outlineColor = new Color32(0, 0, 0, 210); } catch { }
            return t;
        }

        /// <summary>A button in FF's style. Returns its label so callers can keep
        /// the text live (Copy → "Copying…").</summary>
        private static TextMeshProUGUI NewButton(GameObject parent, string name, string label,
            float width, UnityEngine.Events.UnityAction onClick)
        {
            var go = NewChild(parent, name);
            var le = go.AddComponent<LayoutElement>();
            if (width > 0f) le.preferredWidth = width; else le.flexibleWidth = 1f;
            le.preferredHeight = 26f;

            var bg = go.AddComponent<Image>();
            bg.color = ButtonNormal;
            bg.raycastTarget = true;
            var sprite = FFNativeAssets.ButtonGeneric ?? FFNativeAssets.PanelBorderSimple;
            if (sprite != null)
            {
                bg.sprite = sprite;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = bg;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 0.95f, 0.8f, 1f);
            colors.pressedColor = new Color(0.85f, 0.78f, 0.6f, 1f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var t = NewText(go, "Label", label, 13f, FontStyles.Normal, Ink, TextAlignmentOptions.Center);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            return t;
        }

        private static void ApplyFFChrome(GameObject go)
        {
            var img = go.AddComponent<Image>();
            img.raycastTarget = true;
            var frame = FFNativeAssets.PanelBorderThick ?? FFNativeAssets.PanelBorderDark;
            if (frame != null)
            {
                img.sprite = frame;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.97f);
            }
            else img.color = PanelBg;
        }

        private static TMP_FontAsset? GetGameFont() =>
            FFNativeAssets.FontBody ?? FFNativeAssets.FontTitle;
    }
}
