using System.Collections.Generic;
using System.Reflection;
using FFUIOverhaul.TechTree;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Small numbered pin overlay attached to each TechTreeNode. Visible only when
    /// the node's id is in TechAutoQueue; the number shows queue position (1-based).
    /// Attached on UITechTreeOverview.Open postfix; persists across opens but
    /// gets re-numbered when the queue changes.
    /// </summary>
    public class TechNodePinWidget : MonoBehaviour
    {
        // Queued items: solid gold, dark text, position number.
        private static readonly Color PinBg = new(0.83f, 0.63f, 0.19f, 0.95f);
        private static readonly Color PinText = new(0.10f, 0.07f, 0.04f, 1f);
        // Active auto-spend target (prereq being researched on the way to a
        // queued item): dimmer brown, lighter text, "•" marker.
        private static readonly Color ActivePinBg = new(0.30f, 0.22f, 0.10f, 0.95f);
        private static readonly Color ActivePinText = new(0.95f, 0.85f, 0.60f, 1f);
        private static readonly List<TechNodePinWidget> _all = new();
        private static TMP_FontAsset? _cachedFont;

        private TechTreeNode _node = null!;
        private TextMeshProUGUI _numberText = null!;
        private Image _bgImage = null!;

        public static void Attach(TechTreeNode node)
        {
            if (node == null) return;

            // The TechTreeNode's RectTransform is much larger than the visible
            // icon (it includes label area, connector hit-zone, etc.) so anchoring
            // a pin to its corners puts the pin in dead space. Instead, parent
            // to the icon child Image via reflection — that's the rect we
            // actually see.
            var parent = ResolveIconTransform(node);
            if (parent.Find("FFUI_QueuePin") != null) return;

            var go = new GameObject("FFUI_QueuePin", typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);

            // Pin's top-right corner sits AT the icon's top-right, extending into
            // the icon. Anchor + pivot at (1,1) so the offset is the inset from
            // the corner; (-2, -2) pulls it 2px inside.
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.anchoredPosition = new Vector2(-2, -2);
            rt.sizeDelta = new Vector2(30, 30);

            var img = go.AddComponent<Image>();
            img.color = PinBg;
            img.raycastTarget = false;
            // _bgImage assigned below after widget creation

            var textGo = new GameObject("Number", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "1";
            text.fontSize = 22;
            text.fontStyle = FontStyles.Bold;
            text.color = PinText;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.font = GetGameFont();

            var widget = go.AddComponent<TechNodePinWidget>();
            widget._node = node;
            widget._numberText = text;
            widget._bgImage = img;
            widget.Refresh();
            _all.Add(widget);
        }

        public static void RefreshAll()
        {
            _all.RemoveAll(p => p == null);
            foreach (var p in _all) p.Refresh();
        }

        private void Refresh()
        {
            if (_node == null) { gameObject.SetActive(false); return; }
            int id = _node.GetId();

            int idx = TechAutoQueue.IndexOf(id);
            if (idx >= 0)
            {
                // Queued: numbered gold pin
                gameObject.SetActive(true);
                _bgImage.color = PinBg;
                _numberText.color = PinText;
                _numberText.text = (idx + 1).ToString();
                return;
            }

            int activeId = TechAutoQueue.GetActiveSpendTarget();
            if (activeId == id)
            {
                // Auto-walk target: dimmer pin with "•" marker
                gameObject.SetActive(true);
                _bgImage.color = ActivePinBg;
                _numberText.color = ActivePinText;
                _numberText.text = "•";
                return;
            }

            gameObject.SetActive(false);
        }

        private static FieldInfo? _borderImageField;
        private static FieldInfo? _currentImageField;

        /// <summary>
        /// Get the actual icon RectTransform inside the TechTreeNode. Prefer
        /// currentImage — that's the icon sprite Image, sized to the visible icon.
        /// borderImage is too big (stretched to fill the whole TechTreeNode rect),
        /// which placed pins far from the icon's actual location.
        /// </summary>
        private static Transform ResolveIconTransform(TechTreeNode node)
        {
            if (_currentImageField == null)
                _currentImageField = typeof(TechTreeNode).GetField("currentImage",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            if (_borderImageField == null)
                _borderImageField = typeof(TechTreeNode).GetField("borderImage",
                    BindingFlags.Instance | BindingFlags.NonPublic);

            var current = _currentImageField?.GetValue(node) as Image;
            if (current != null) return current.transform;
            var border = _borderImageField?.GetValue(node) as Image;
            if (border != null) return border.transform;
            return node.transform;
        }

        private static TMP_FontAsset? GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            var all = Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }
    }
}
