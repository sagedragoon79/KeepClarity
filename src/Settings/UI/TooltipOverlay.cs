using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// Singleton tooltip overlay. Lives INSIDE the main settings Canvas (not its
    /// own Canvas) so z-order is fully deterministic — same-Canvas hierarchy
    /// rule is "last sibling renders on top," and we call SetAsLastSibling()
    /// every Show() to enforce that.
    /// </summary>
    public class TooltipOverlay : MonoBehaviour
    {
        private static TooltipOverlay? _instance;
        private RectTransform _root = null!;
        private TMP_Text _text = null!;
        private RectTransform _canvasRT = null!; // host canvas's RectTransform, used for screen→local
        private const float MAX_WIDTH = 360f;
        private const float MOUSE_OFFSET_X = 14f;
        private const float MOUSE_OFFSET_Y = -14f;

        public static TooltipOverlay EnsureInstance(Transform parent)
        {
            if (_instance != null) return _instance;

            var go = new GameObject("TooltipOverlay", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            _instance = go.AddComponent<TooltipOverlay>();
            _instance._canvasRT = (RectTransform)parent;
            _instance.Build();
            _instance.gameObject.SetActive(false);
            return _instance;
        }

        public static void Show(string text)
        {
            if (_instance == null || string.IsNullOrEmpty(text)) return;
            _instance.gameObject.SetActive(true);
            // Pop to top of the host Canvas's child list every show — last
            // sibling renders on top in same-Canvas hierarchy ordering.
            _instance.transform.SetAsLastSibling();
            _instance._text.text = text;
            _instance._text.ForceMeshUpdate();
            float pref = Mathf.Min(_instance._text.preferredWidth + 24, MAX_WIDTH);
            float h = _instance._text.preferredHeight + 16;
            _instance._root.sizeDelta = new Vector2(pref, h);
            _instance.Reposition();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.gameObject.SetActive(false);
        }

        private void Build()
        {
            // Stretch to fill the host Canvas so anchored coordinates from the
            // mouse map cleanly into our space.
            var rt = (RectTransform)transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
            _root = (RectTransform)bgGo.transform;
            // Anchor at parent CENTER. ScreenPointToLocalPointInRectangle
            // returns coordinates relative to the rect's pivot (canvas center
            // for our overlay), so to put the tooltip at the mouse we need
            // anchored coords also relative to center.
            _root.pivot = new Vector2(0, 1);
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(200, 40);
            var bg = bgGo.GetComponent<Image>();
            SettingsCanvas.ApplyBorderSprite(bg, FFNativeAssets.PanelBorder, FFNativeAssets.PanelTintHeader);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(bgGo.transform, false);
            _text = txtGo.AddComponent<TextMeshProUGUI>();
            if (FFNativeAssets.FontBody != null) _text.font = FFNativeAssets.FontBody;
            _text.fontSize = 12;
            _text.fontStyle = FontStyles.Bold | FontStyles.SmallCaps;
            _text.color = FFNativeAssets.TextPrimary;
            _text.enableWordWrapping = true;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.raycastTarget = false;
            var trt = _text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12, 8);
            trt.offsetMax = new Vector2(-12, -8);
        }

        private void Update()
        {
            if (gameObject.activeSelf) Reposition();
        }

        private void Reposition()
        {
            var screen = (Vector2)Input.mousePosition;
            Vector2 localPoint;
            // worldCamera = null for ScreenSpaceOverlay canvases.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screen, null, out localPoint);

            float x = localPoint.x + MOUSE_OFFSET_X;
            float y = localPoint.y + MOUSE_OFFSET_Y;

            var sd = _root.sizeDelta;
            var cr = _canvasRT.rect;
            float halfW = cr.width * 0.5f;
            float halfH = cr.height * 0.5f;
            if (x + sd.x > halfW) x = localPoint.x - sd.x - MOUSE_OFFSET_X;
            if (y - sd.y < -halfH) y = localPoint.y + sd.y + MOUSE_OFFSET_X;

            _root.anchoredPosition = new Vector2(x, y);
        }
    }

    /// <summary>
    /// Attach to any UI element to show <see cref="Body"/> in the singleton
    /// tooltip overlay while hovered.
    /// </summary>
    public class HoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Body = "";
        public void OnPointerEnter(PointerEventData _)
        {
            if (!string.IsNullOrEmpty(Body)) TooltipOverlay.Show(Body);
        }
        public void OnPointerExit(PointerEventData _) { TooltipOverlay.Hide(); }
    }
}
