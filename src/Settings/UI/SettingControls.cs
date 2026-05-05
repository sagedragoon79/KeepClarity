using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// Stage 2D — produces the right UGUI control for a given <see cref="SettingEntryRecord"/>.
    /// One factory method per type:
    /// <list type="bullet">
    ///   <item><c>bool</c> → toggle (FF checkbox sprite + amber fill)</item>
    ///   <item>numeric with Min/Max → slider + live numeric input</item>
    ///   <item>numeric without range → input field</item>
    ///   <item><c>string</c> with EnumOptions → cycle (←/→) selector</item>
    ///   <item>plain <c>string</c> → input field</item>
    ///   <item><c>KeyCode</c> → "Press a key…" rebind button</item>
    ///   <item><c>enum</c> → cycle selector</item>
    /// </list>
    /// All controls write back through <see cref="SettingEntryRecord.Set"/> and
    /// fire <see cref="SettingsRegistry.NotifyChanged"/> so other observers see
    /// the change live.
    /// </summary>
    public static class SettingControls
    {
        /// <summary>Builds the control area for an entry. Caller has already
        /// laid out the row; this method only adds the interactive widget on
        /// the right side. Returns the root GameObject of the widget.</summary>
        public static GameObject Build(SettingEntryRecord rec, RectTransform controlContainer, Action onChanged)
        {
            var t = rec.ValueType;
            try
            {
                if (t == typeof(bool))   return BuildToggle(rec, controlContainer, onChanged);
                if (t == typeof(int))    return BuildIntControl(rec, controlContainer, onChanged);
                if (t == typeof(float))  return BuildFloatControl(rec, controlContainer, onChanged);
                if (t == typeof(string)) return BuildStringControl(rec, controlContainer, onChanged);
                if (t == typeof(KeyCode)) return BuildKeyBind(rec, controlContainer, onChanged);
                if (t.IsEnum)             return BuildEnumCycle(rec, controlContainer, onChanged);
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"[Settings/2D] Build control failed for {rec.Key}: {ex.Message}");
            }
            return BuildReadonlyValue(rec, controlContainer);
        }

        // ============== Bool — Toggle ==============

        private static GameObject BuildToggle(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            var go = new GameObject("Toggle:" + rec.Key, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(60, 22);
            rt.anchoredPosition = new Vector2(0, 0);

            // Box — IMG_BorderSimpleDark01 at FF's native 20x20
            var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image), typeof(Toggle));
            boxGo.transform.SetParent(go.transform, false);
            var boxRT = (RectTransform)boxGo.transform;
            boxRT.anchorMin = new Vector2(0, 0.5f);
            boxRT.anchorMax = new Vector2(0, 0.5f);
            boxRT.pivot = new Vector2(0, 0.5f);
            boxRT.sizeDelta = new Vector2(20, 20);
            boxRT.anchoredPosition = Vector2.zero;
            var boxImg = boxGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(boxImg, FFNativeAssets.CheckboxBox, FFNativeAssets.PanelTintDefault);

            // Checkmark — FF's native BTN_Checkbox01_DOWN sprite (16x16)
            var markGo = new GameObject("Mark", typeof(RectTransform), typeof(Image));
            markGo.transform.SetParent(boxGo.transform, false);
            var markRT = (RectTransform)markGo.transform;
            markRT.anchorMin = new Vector2(0.5f, 0.5f);
            markRT.anchorMax = new Vector2(0.5f, 0.5f);
            markRT.pivot = new Vector2(0.5f, 0.5f);
            markRT.sizeDelta = new Vector2(16, 16);
            var markImg = markGo.GetComponent<Image>();
            if (FFNativeAssets.CheckboxCheck != null)
            {
                markImg.sprite = FFNativeAssets.CheckboxCheck;
                markImg.type = Image.Type.Simple;
                markImg.preserveAspect = true;
                markImg.color = FFNativeAssets.PanelTintDefault;
            }
            else
            {
                markImg.color = FFNativeAssets.RowActive;
            }

            // On/Off label
            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) lbl.font = FFNativeAssets.FontBody;
            lbl.fontSize = 12;
            lbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = new Vector2(0, 0);
            lblRT.anchorMax = new Vector2(1, 1);
            lblRT.offsetMin = new Vector2(26, 0);
            lblRT.offsetMax = new Vector2(0, 0);

            void Refresh()
            {
                bool on = rec.Get() is bool b && b;
                markImg.enabled = on;
                lbl.text = on ? "On" : "Off";
                lbl.color = on ? FFNativeAssets.TextPrimary : FFNativeAssets.TextDim;
            }

            var toggle = boxGo.GetComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = markImg;
            toggle.isOn = rec.Get() is bool initial && initial;
            toggle.onValueChanged.AddListener(v =>
            {
                rec.Set(v);
                SettingsRegistry.NotifyChanged(rec, v);
                Refresh();
                onChanged?.Invoke();
            });

            Refresh();
            return go;
        }

        // ============== Int / Float ==============

        private static GameObject BuildIntControl(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            if (TryGetMinMax(rec, out int min, out int max))
            {
                return BuildSliderInt(rec, parent, min, max, onChanged);
            }
            return BuildNumberInput(rec, parent, txt =>
            {
                if (int.TryParse(txt, out int v))
                {
                    rec.Set(v);
                    SettingsRegistry.NotifyChanged(rec, v);
                    return true;
                }
                return false;
            }, onChanged);
        }

        private static GameObject BuildFloatControl(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            if (TryGetMinMax(rec, out float min, out float max))
            {
                return BuildSliderFloat(rec, parent, min, max, onChanged);
            }
            return BuildNumberInput(rec, parent, txt =>
            {
                if (float.TryParse(txt, out float v))
                {
                    rec.Set(v);
                    SettingsRegistry.NotifyChanged(rec, v);
                    return true;
                }
                return false;
            }, onChanged);
        }

        private static GameObject BuildSliderInt(SettingEntryRecord rec, RectTransform parent, int min, int max, Action onChanged)
        {
            return BuildSliderInternal(rec, parent, min, max, wholeNumbers: true, onChanged: onChanged,
                format: v => Mathf.RoundToInt(v).ToString(),
                applyValue: v => { var iv = Mathf.RoundToInt(v); rec.Set(iv); SettingsRegistry.NotifyChanged(rec, iv); });
        }

        private static GameObject BuildSliderFloat(SettingEntryRecord rec, RectTransform parent, float min, float max, Action onChanged)
        {
            float range = max - min;
            int decimals = range >= 10 ? 1 : (range >= 1 ? 2 : 3);
            string fmt = "F" + decimals;
            return BuildSliderInternal(rec, parent, min, max, wholeNumbers: false, onChanged: onChanged,
                format: v => v.ToString(fmt),
                applyValue: v => { rec.Set(v); SettingsRegistry.NotifyChanged(rec, v); });
        }

        private static GameObject BuildSliderInternal(SettingEntryRecord rec, RectTransform parent,
            float min, float max, bool wholeNumbers, Action onChanged,
            Func<float, string> format, Action<float> applyValue)
        {
            var go = new GameObject("Slider:" + rec.Key, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Slider takes left ~70% of the control area. 36 tall (was 18) for
            // a bigger click/drag target.
            var sliderGo = new GameObject("SliderControl", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(go.transform, false);
            var sliderRT = (RectTransform)sliderGo.transform;
            sliderRT.anchorMin = new Vector2(0, 0.5f);
            sliderRT.anchorMax = new Vector2(1, 0.5f);
            sliderRT.pivot = new Vector2(0, 0.5f);
            sliderRT.sizeDelta = new Vector2(-80, 36);
            sliderRT.anchoredPosition = Vector2.zero;

            // Background track — 12px tall (was 6) so click target is doubled.
            var trackGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(sliderGo.transform, false);
            var trackRT = (RectTransform)trackGo.transform;
            trackRT.anchorMin = new Vector2(0, 0.5f);
            trackRT.anchorMax = new Vector2(1, 0.5f);
            trackRT.pivot = new Vector2(0.5f, 0.5f);
            trackRT.sizeDelta = new Vector2(0, 12);
            trackRT.anchoredPosition = Vector2.zero;
            var trackImg = trackGo.GetComponent<Image>();
            // FF's slider track — IMG_BorderSimpleDark01 with the same medium-grey
            // tint (#989898) used on the Map Size slider.
            SettingsCanvas.ApplyBorderSprite(trackImg, FFNativeAssets.SliderTrack, new Color(0.60f, 0.60f, 0.60f, 1f));

            // Fill area — matches the 12px-tall track (was 6).
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            var fillAreaRT = (RectTransform)fillAreaGo.transform;
            fillAreaRT.anchorMin = new Vector2(0, 0.5f);
            fillAreaRT.anchorMax = new Vector2(1, 0.5f);
            fillAreaRT.offsetMin = new Vector2(8, -6);
            fillAreaRT.offsetMax = new Vector2(-8, 6);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            var fillRT = (RectTransform)fillGo.transform;
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            if (FFNativeAssets.SliderFill != null)
            {
                fillImg.sprite = FFNativeAssets.SliderFill;
                fillImg.type = Image.Type.Sliced;
            }
            fillImg.color = new Color(1.00f, 0.384f, 0f, 1f); // FF #FF6200

            // Handle
            var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            var haRT = (RectTransform)handleAreaGo.transform;
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.offsetMin = new Vector2(8, 0);
            haRT.offsetMax = new Vector2(-8, 0);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRT = (RectTransform)handleGo.transform;
            // Doubled from 20×20 — 4× click area for grabbing the handle.
            handleRT.sizeDelta = new Vector2(40, 40);
            var handleImg = handleGo.GetComponent<Image>();
            // FF's native slider handle (BTN_Slider01_UP) — the diamond shape
            // used on the Map Size slider in New Settlement. Simple sprite,
            // not 9-sliced, so we don't apply Sliced type.
            if (FFNativeAssets.SliderHandle != null)
            {
                handleImg.sprite = FFNativeAssets.SliderHandle;
                handleImg.type = Image.Type.Simple;
                handleImg.preserveAspect = true;
            }
            else
            {
                SettingsCanvas.ApplyBorderSprite(handleImg, FFNativeAssets.PanelBorder, FFNativeAssets.PanelTintDefault);
            }

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.value = ConvertToFloat(rec.Get());

            // Numeric readout on the right
            var readoutGo = new GameObject("Readout", typeof(RectTransform));
            readoutGo.transform.SetParent(go.transform, false);
            var readout = readoutGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontNumbers != null) readout.font = FFNativeAssets.FontNumbers;
            readout.fontSize = 14;
            readout.color = FFNativeAssets.TextPrimary;
            readout.alignment = TextAlignmentOptions.MidlineRight;
            readout.text = format(slider.value);
            var roRT = readout.rectTransform;
            roRT.anchorMin = new Vector2(1, 0);
            roRT.anchorMax = new Vector2(1, 1);
            roRT.pivot = new Vector2(1, 0.5f);
            roRT.sizeDelta = new Vector2(72, 0);
            roRT.anchoredPosition = new Vector2(-2, 0);

            slider.onValueChanged.AddListener(v =>
            {
                applyValue(v);
                readout.text = format(v);
                onChanged?.Invoke();
            });
            return go;
        }

        // ============== String / numeric without range ==============

        private static GameObject BuildNumberInput(SettingEntryRecord rec, RectTransform parent, Func<string, bool> tryApply, Action onChanged)
        {
            return BuildInputField(parent, rec.Get()?.ToString() ?? "", v =>
            {
                if (tryApply(v)) onChanged?.Invoke();
            }, widthOverride: 100f);
        }

        private static GameObject BuildStringControl(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            if (rec.Meta.EnumOptions != null && rec.Meta.EnumOptions.Length > 0)
            {
                return BuildDropdown(parent, rec.Meta.EnumOptions, rec.Get() as string ?? "",
                    v =>
                    {
                        rec.Set(v);
                        SettingsRegistry.NotifyChanged(rec, v);
                        onChanged?.Invoke();
                    });
            }
            return BuildInputField(parent, rec.Get() as string ?? "",
                v =>
                {
                    rec.Set(v);
                    SettingsRegistry.NotifyChanged(rec, v);
                    onChanged?.Invoke();
                }, widthOverride: 220f);
        }

        private static GameObject BuildInputField(RectTransform parent, string initial, Action<string> onSubmit, float widthOverride = 0f)
        {
            // Match FF's actual working TMP_InputField hierarchy (per the
            // Trading Post buy-input dump):
            //   Input GO (Image + TMP_InputField)
            //     └── Text Area (RectMask2D)
            //           ├── Caret (created by TMP_InputField on first edit)
            //           ├── Placeholder (TMP_Text)
            //           └── Text (TMP_Text)
            //
            // Plus textViewport explicitly set, fontAsset/pointSize on the
            // input itself, caret/selection colors matching FF.
            //
            // CRITICAL: TMP_InputField.OnEnable is what wires up the caret
            // CanvasRenderer and selection mesh. If OnEnable runs while
            // m_TextComponent is null (which happens if we AddComponent
            // before assigning textComponent), the caret subsystem is dead
            // for the lifetime of this input — assigning textComponent
            // afterward does NOT re-init it. We deactivate the GO before
            // adding the component, configure props, then reactivate so
            // OnEnable fires with everything wired.
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.SetActive(false);
            var rt = (RectTransform)go.transform;
            if (widthOverride > 0f)
            {
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(0, 0.5f);
                rt.pivot = new Vector2(0, 0.5f);
                rt.sizeDelta = new Vector2(widthOverride, 22);
                rt.anchoredPosition = new Vector2(0, 0);
            }
            else
            {
                rt.anchorMin = new Vector2(0, 0.5f);
                rt.anchorMax = new Vector2(1, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0, 22);
                rt.anchoredPosition = Vector2.zero;
            }
            var img = go.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(img, FFNativeAssets.PanelBorderSimple, FFNativeAssets.PanelTintDefault);

            // Text Area — viewport with RectMask2D (matches FF's setup).
            var areaGo = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            areaGo.transform.SetParent(go.transform, false);
            var areaRT = (RectTransform)areaGo.transform;
            areaRT.anchorMin = Vector2.zero;
            areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(8, 2);
            areaRT.offsetMax = new Vector2(-8, -2);

            // Placeholder (added BEFORE Text so Text renders on top, matching FF).
            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(areaGo.transform, false);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) ph.font = FFNativeAssets.FontBody;
            ph.fontSize = 13;
            ph.color = FFNativeAssets.TextDim;
            ph.fontStyle = FontStyles.Italic | FontStyles.SmallCaps;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.text = "";
            var phRT = ph.rectTransform;
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = Vector2.zero;
            phRT.offsetMax = Vector2.zero;

            // Text component
            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(areaGo.transform, false);
            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) txt.font = FFNativeAssets.FontBody;
            txt.fontSize = 13;
            txt.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            txt.color = FFNativeAssets.TextPrimary;
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            // Single-line input: wrapping off, rich text off so the input
            // doesn't try to interpret <tag> markup as formatting.
            txt.enableWordWrapping = false;
            txt.richText = false;
            txt.overflowMode = TextOverflowModes.Masking;
            var txtRT = txt.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = areaRT;          // FF sets this; missing it broke caret render
            input.textComponent = txt;
            input.placeholder = ph;
            input.targetGraphic = img;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.richText = false;
            // Some TMP versions need fontAsset on the input itself for its
            // internal caret/selection rendering. FF's working inputs all set it.
            if (txt.font != null)
            {
                input.fontAsset = txt.font;
                input.pointSize = 13;
            }
            // Caret + selection colors copied from FF's working trading post input.
            // customCaretColor MUST be true for caretColor to take effect —
            // when false, TMP falls back to textComponent.color.
            input.customCaretColor = true;
            input.caretColor = new Color(0.84f, 0.84f, 0.82f, 1f);     // FF #D6D5D1
            input.caretWidth = 2;                                       // bumped from 1 — easier to see
            input.caretBlinkRate = 0.85f;
            input.selectionColor = new Color(0.86f, 0.83f, 0.73f, 0.75f); // FF #DBD4B9C0
            input.text = initial;
            input.onEndEdit.AddListener(v => { try { onSubmit(v); } catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings/2D] input submit threw: {ex.Message}"); } });

            // OnEnable fires now with textComponent + textViewport already
            // wired — caret subsystem registers correctly.
            go.SetActive(true);
            return go;
        }

        // ============== KeyCode rebind ==============

        private static GameObject BuildKeyBind(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            var go = new GameObject("KeyBind:" + rec.Key, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(160, 26);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(img, FFNativeAssets.ButtonFocus ?? FFNativeAssets.ButtonGeneric, FFNativeAssets.PanelTintDefault);

            var lblGo = new GameObject("Label", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) lbl.font = FFNativeAssets.FontBody;
            lbl.fontSize = 13;
            lbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            lbl.color = FFNativeAssets.TextPrimary;
            lbl.alignment = TextAlignmentOptions.Center;
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = new Vector2(6, 1);
            lblRT.offsetMax = new Vector2(-6, -1);

            void Render()
            {
                var kc = rec.Get() is KeyCode k ? k : KeyCode.None;
                lbl.text = kc.ToString();
            }
            Render();

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                lbl.text = "Press a key…";
                lbl.color = FFNativeAssets.WarningAmber;
                var listener = go.AddComponent<KeyCaptureListener>();
                listener.OnCaptured = (kc) =>
                {
                    if (kc != KeyCode.Escape)
                    {
                        rec.Set(kc);
                        SettingsRegistry.NotifyChanged(rec, kc);
                    }
                    lbl.color = FFNativeAssets.TextPrimary;
                    Render();
                    onChanged?.Invoke();
                };
            });

            return go;
        }

        // ============== Enum / EnumOptions cycle ==============

        private static GameObject BuildEnumCycle(SettingEntryRecord rec, RectTransform parent, Action onChanged)
        {
            var names = Enum.GetNames(rec.ValueType);
            string current = rec.Get()?.ToString() ?? names[0];
            return BuildDropdown(parent, names, current,
                v =>
                {
                    var parsed = Enum.Parse(rec.ValueType, v);
                    rec.Set(parsed);
                    SettingsRegistry.NotifyChanged(rec, parsed);
                    onChanged?.Invoke();
                });
        }

        /// <summary>
        /// Real TMP_Dropdown matching FF's Settings-menu dropdown style. Built
        /// programmatically (no prefab) using FF's dropdown sprites:
        ///   bg     = IMG_BorderSimpleThickDark01B
        ///   arrow  = IMG_ArrowRightFancy01 (rotated 90° to point down)
        ///   check  = ICN_Checkmark01_Transparent
        ///   mask   = UIMask
        /// </summary>
        private static GameObject BuildDropdown(RectTransform parent, string[] options, string current, Action<string> onChange)
        {
            // Outer container
            var go = new GameObject("Dropdown", typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = new Vector2(260, 26);
            rt.anchoredPosition = Vector2.zero;
            var bgImg = go.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(bgImg, FFNativeAssets.DropdownBg, FFNativeAssets.PanelTintDefault);

            // Caption text — shows currently selected option
            var captionGo = new GameObject("Label", typeof(RectTransform));
            captionGo.transform.SetParent(go.transform, false);
            var caption = captionGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) caption.font = FFNativeAssets.FontBody;
            caption.fontSize = 13;
            caption.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            caption.color = FFNativeAssets.TextPrimary;
            caption.alignment = TextAlignmentOptions.MidlineLeft;
            var capRT = caption.rectTransform;
            capRT.anchorMin = Vector2.zero;
            capRT.anchorMax = Vector2.one;
            capRT.offsetMin = new Vector2(10, 2);
            capRT.offsetMax = new Vector2(-26, -2);

            // Arrow (FF's right-arrow, rotated to point down). Use a CENTER
            // pivot so the -90° rotation doesn't displace the visual — with
            // an edge-aligned pivot, rotating moves the rendered sprite
            // outside the box (it ends up above the top-right corner).
            if (FFNativeAssets.DropdownArrow != null)
            {
                var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
                arrowGo.transform.SetParent(go.transform, false);
                var arrowRT = (RectTransform)arrowGo.transform;
                arrowRT.anchorMin = new Vector2(1, 0.5f);
                arrowRT.anchorMax = new Vector2(1, 0.5f);
                arrowRT.pivot = new Vector2(0.5f, 0.5f);
                arrowRT.sizeDelta = new Vector2(16, 16);
                arrowRT.anchoredPosition = new Vector2(-12, 0); // center 12px from right edge
                arrowRT.localRotation = Quaternion.Euler(0, 0, -90);
                var arrowImg = arrowGo.GetComponent<Image>();
                arrowImg.sprite = FFNativeAssets.DropdownArrow;
                arrowImg.preserveAspect = true;
                arrowImg.color = FFNativeAssets.TextPrimary;
                arrowImg.raycastTarget = false; // bg image catches the click
            }
            caption.raycastTarget = false; // bg image catches the click

            // Template hierarchy (popup list — inactive until clicked).
            // Anchor/pivot values mirror Unity's stock TMP_Dropdown template
            // exactly so item positioning matches what TMP_Dropdown expects.
            var templateGo = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(go.transform, false);
            var templateRT = (RectTransform)templateGo.transform;
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.anchoredPosition = new Vector2(0, 2);
            templateRT.sizeDelta = new Vector2(0, Mathf.Min(options.Length * 22 + 8, 200));
            var templateImg = templateGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(templateImg, FFNativeAssets.DropdownBg, FFNativeAssets.PanelTintDefault);
            var templateSR = templateGo.GetComponent<ScrollRect>();
            templateSR.horizontal = false;
            templateSR.vertical = true;
            templateSR.scrollSensitivity = 22f; // each item-height per wheel notch

            // Viewport — Unity stock uses pivot (0, 1) top-LEFT, anchors stretch.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            var vpRT = (RectTransform)viewportGo.transform;
            vpRT.anchorMin = new Vector2(0, 0);
            vpRT.anchorMax = new Vector2(1, 1);
            vpRT.pivot = new Vector2(0, 1);
            vpRT.offsetMin = new Vector2(2, 2);
            vpRT.offsetMax = new Vector2(-2, -2);
            var vpImg = viewportGo.GetComponent<Image>();
            if (FFNativeAssets.DropdownMask != null)
            {
                vpImg.sprite = FFNativeAssets.DropdownMask;
                vpImg.type = Image.Type.Sliced;
            }
            var vpMask = viewportGo.GetComponent<Mask>();
            vpMask.showMaskGraphic = false;
            templateSR.viewport = vpRT;

            // Content — Unity stock: anchored TOP, pivot top, sizeDelta = one
            // item height (TMP_Dropdown grows it as items are added).
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRT = (RectTransform)contentGo.transform;
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 22);
            templateSR.content = contentRT;

            // Item template — Unity stock: middle-anchored, sizeDelta height = item height.
            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRT = (RectTransform)itemGo.transform;
            itemRT.anchorMin = new Vector2(0, 0.5f);
            itemRT.anchorMax = new Vector2(1, 0.5f);
            itemRT.pivot = new Vector2(0.5f, 0.5f);
            itemRT.sizeDelta = new Vector2(0, 22);

            // Item background
            var itemBgGo = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
            itemBgGo.transform.SetParent(itemGo.transform, false);
            var itemBgRT = (RectTransform)itemBgGo.transform;
            itemBgRT.anchorMin = Vector2.zero;
            itemBgRT.anchorMax = Vector2.one;
            itemBgRT.offsetMin = Vector2.zero;
            itemBgRT.offsetMax = Vector2.zero;
            var itemBgImg = itemBgGo.GetComponent<Image>();
            itemBgImg.color = new Color(1f, 1f, 1f, 0f);

            // Item checkmark (shown for the currently-selected item)
            var checkGo = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
            checkGo.transform.SetParent(itemGo.transform, false);
            var checkRT = (RectTransform)checkGo.transform;
            checkRT.anchorMin = new Vector2(0, 0.5f);
            checkRT.anchorMax = new Vector2(0, 0.5f);
            checkRT.pivot = new Vector2(0.5f, 0.5f);
            checkRT.sizeDelta = new Vector2(14, 14);
            checkRT.anchoredPosition = new Vector2(14, 0);
            var checkImg = checkGo.GetComponent<Image>();
            if (FFNativeAssets.IconCheckmark != null)
            {
                checkImg.sprite = FFNativeAssets.IconCheckmark;
                checkImg.preserveAspect = true;
                checkImg.color = new Color(1f, 0.65f, 0.20f, 1f);
            }

            // Item label
            var itemLblGo = new GameObject("Item Label", typeof(RectTransform));
            itemLblGo.transform.SetParent(itemGo.transform, false);
            var itemLbl = itemLblGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) itemLbl.font = FFNativeAssets.FontBody;
            itemLbl.fontSize = 13;
            itemLbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            itemLbl.color = FFNativeAssets.TextPrimary;
            itemLbl.alignment = TextAlignmentOptions.MidlineLeft;
            itemLbl.raycastTarget = false;
            var itemLblRT = itemLbl.rectTransform;
            itemLblRT.anchorMin = Vector2.zero;
            itemLblRT.anchorMax = Vector2.one;
            itemLblRT.offsetMin = new Vector2(28, 1);
            itemLblRT.offsetMax = new Vector2(-8, -1);

            var itemToggle = itemGo.GetComponent<Toggle>();
            itemToggle.targetGraphic = itemBgImg;
            itemToggle.graphic = checkImg;

            // Wire the dropdown
            var dd = go.GetComponent<TMP_Dropdown>();
            dd.template = templateRT;
            dd.captionText = caption;
            dd.itemText = itemLbl;
            dd.targetGraphic = bgImg;
            dd.options = options.Select(o => new TMP_Dropdown.OptionData(o)).ToList();
            int idx = Array.IndexOf(options, current);
            if (idx < 0) idx = 0;
            dd.value = idx;
            dd.RefreshShownValue();

            // Template MUST be inactive in the hierarchy; TMP_Dropdown clones it on open.
            templateGo.SetActive(false);

            dd.onValueChanged.AddListener(i =>
            {
                if (i >= 0 && i < options.Length)
                    onChange(options[i]);
            });

            return go;
        }

        private static GameObject BuildCycleSelector(RectTransform parent, string[] options, string current, Action<string> onChange)
        {
            var go = new GameObject("Cycle", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            int idx = Array.IndexOf(options, current);
            if (idx < 0) idx = 0;

            // Left (◀) button
            var leftBtn = SettingsCanvas.NewButton("Left", go.transform, "◀", () => { }, fancy: false);
            var leftRT = leftBtn.GetComponent<RectTransform>();
            leftRT.anchorMin = new Vector2(0, 0.5f);
            leftRT.anchorMax = new Vector2(0, 0.5f);
            leftRT.pivot = new Vector2(0, 0.5f);
            leftRT.sizeDelta = new Vector2(28, 24);
            leftRT.anchoredPosition = Vector2.zero;

            // Label (current option)
            var lblGo = new GameObject("Current", typeof(RectTransform));
            lblGo.transform.SetParent(go.transform, false);
            var lbl = lblGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) lbl.font = FFNativeAssets.FontBody;
            lbl.fontSize = 13;
            lbl.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            lbl.color = FFNativeAssets.TextPrimary;
            lbl.alignment = TextAlignmentOptions.Center;
            lbl.text = options[idx];
            var lblRT = lbl.rectTransform;
            lblRT.anchorMin = new Vector2(0, 0);
            lblRT.anchorMax = new Vector2(1, 1);
            lblRT.offsetMin = new Vector2(34, 0);
            lblRT.offsetMax = new Vector2(-34, 0);

            // Right (▶) button
            var rightBtn = SettingsCanvas.NewButton("Right", go.transform, "▶", () => { }, fancy: false);
            var rightRT = rightBtn.GetComponent<RectTransform>();
            rightRT.anchorMin = new Vector2(1, 0.5f);
            rightRT.anchorMax = new Vector2(1, 0.5f);
            rightRT.pivot = new Vector2(1, 0.5f);
            rightRT.sizeDelta = new Vector2(28, 24);
            rightRT.anchoredPosition = Vector2.zero;

            // Wire actions (replace listeners)
            var leftButton = leftBtn.GetComponent<Button>();
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(() =>
            {
                idx = (idx - 1 + options.Length) % options.Length;
                lbl.text = options[idx];
                onChange(options[idx]);
            });

            var rightButton = rightBtn.GetComponent<Button>();
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(() =>
            {
                idx = (idx + 1) % options.Length;
                lbl.text = options[idx];
                onChange(options[idx]);
            });

            return go;
        }

        // ============== Read-only fallback ==============

        private static GameObject BuildReadonlyValue(SettingEntryRecord rec, RectTransform parent)
        {
            var go = new GameObject("Readonly:" + rec.Key, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) txt.font = FFNativeAssets.FontBody;
            txt.fontSize = 13;
            txt.fontStyle = FontStyles.Italic | FontStyles.SmallCaps;
            txt.color = FFNativeAssets.TextDim;
            try { txt.text = rec.Get()?.ToString() ?? "null"; } catch { txt.text = "?"; }
            txt.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = txt.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(8, 0);
            rt.offsetMax = new Vector2(-8, 0);
            return go;
        }

        // ============== Helpers ==============

        private static bool TryGetMinMax(SettingEntryRecord rec, out int min, out int max)
        {
            min = max = 0;
            try
            {
                if (rec.Meta.Min == null || rec.Meta.Max == null) return false;
                min = Convert.ToInt32(rec.Meta.Min);
                max = Convert.ToInt32(rec.Meta.Max);
                return max > min;
            }
            catch { return false; }
        }

        private static bool TryGetMinMax(SettingEntryRecord rec, out float min, out float max)
        {
            min = max = 0f;
            try
            {
                if (rec.Meta.Min == null || rec.Meta.Max == null) return false;
                min = Convert.ToSingle(rec.Meta.Min);
                max = Convert.ToSingle(rec.Meta.Max);
                return max > min;
            }
            catch { return false; }
        }

        private static float ConvertToFloat(object? v)
        {
            try { return Convert.ToSingle(v); } catch { return 0f; }
        }
    }

    /// <summary>
    /// Captures the next non-modifier keypress and reports it back via <see cref="OnCaptured"/>.
    /// Used by the keybind rebind button.
    /// </summary>
    public class KeyCaptureListener : MonoBehaviour
    {
        public Action<KeyCode>? OnCaptured;
        private bool _waitedOneFrame;

        private void Update()
        {
            // Skip the first frame so the click that opened us doesn't register.
            if (!_waitedOneFrame) { _waitedOneFrame = true; return; }
            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                if (kc == KeyCode.Mouse0 || kc == KeyCode.Mouse1) continue;
                if (Input.GetKeyDown(kc))
                {
                    try { OnCaptured?.Invoke(kc); } catch { }
                    Destroy(this);
                    return;
                }
            }
        }
    }
}
