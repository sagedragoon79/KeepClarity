using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// MonoBehaviour attached as a child of each UIToolbarBuildingButton in the
    /// build menu. Shows a count of placed buildings of that type and arrows to
    /// cycle the camera through them.
    ///
    /// Count source: GameManager.buildManager.GetBuildingsGroupedByTag(prefab.tag).
    /// The dictionary is keyed on the GameObject tag (what BuildManager.AddBuilding
    /// uses), so the prefab's .tag matches placed instances directly.
    /// </summary>
    public class BuildingCounterWidget : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;
        private const string ArrowLeft = "◀";  // ◀
        private const string ArrowRight = "▶"; // ▶

        private UIToolbarBuildingButton _button = null!;
        private TextMeshProUGUI _countText = null!;
        private Button _prevButton = null!;
        private Button _nextButton = null!;
        private List<Building>? _buildings;
        private int _cycleIndex;
        private float _refreshTimer;
        private static TMP_FontAsset? _cachedFont;

        public static void Attach(UIToolbarBuildingButton button)
        {
            if (button == null) return;

            // If a previous attach attempt left a partial widget (e.g. an earlier
            // build that errored mid-way), tear it down and rebuild from scratch.
            var existing = button.transform.Find("FFUI_BuildingCounter");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var container = new GameObject("FFUI_BuildingCounter", typeof(RectTransform));
            container.transform.SetParent(button.transform, worldPositionStays: false);

            var rt = (RectTransform)container.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 2f);
            rt.sizeDelta = new Vector2(0f, 16f);

            // Don't let our injected widget participate in the tile's layout.
            var le = container.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;

            var widget = container.AddComponent<BuildingCounterWidget>();
            widget._button = button;
            widget._prevButton = CreateArrowButton(container, ArrowLeft, widget.OnPrev);
            widget._countText = CreateCountText(container);
            widget._nextButton = CreateArrowButton(container, ArrowRight, widget.OnNext);
            widget.UpdateCount();
        }

        private void Update()
        {
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < RefreshSeconds) return;
            _refreshTimer = 0f;
            UpdateCount();
        }

        private void UpdateCount()
        {
            if (_button == null || _button.buildingPrefab == null) return;
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null || gm.buildManager == null) return;

            var grouped = gm.buildManager.GetBuildingsGroupedByTag(_button.buildingPrefab.tag);
            _buildings = grouped?.buildings;
            int count = _buildings?.Count ?? 0;
            _countText.text = count.ToString();

            bool canCycle = count > 0;
            _prevButton.interactable = canCycle;
            _nextButton.interactable = canCycle;
        }

        private void OnPrev()
        {
            if (_buildings == null || _buildings.Count == 0) return;
            _cycleIndex = (_cycleIndex - 1 + _buildings.Count) % _buildings.Count;
            LerpToBuilding(_buildings[_cycleIndex]);
        }

        private void OnNext()
        {
            if (_buildings == null || _buildings.Count == 0) return;
            _cycleIndex = (_cycleIndex + 1) % _buildings.Count;
            LerpToBuilding(_buildings[_cycleIndex]);
        }

        private static void LerpToBuilding(Building b)
        {
            if (b == null) return;
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null || gm.cameraManager == null) return;
            gm.cameraManager.LerpToLocation(b.transform.position, 0f);
        }

        private static TextMeshProUGUI CreateCountText(GameObject parent)
        {
            var go = new GameObject("Count", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 22f;
            le.preferredHeight = 16f;
            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = "0";
            text.fontSize = 13f;
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.font = GetGameFont();
            return text;
        }

        private static Button CreateArrowButton(GameObject parent, string arrow, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject($"Arrow_{arrow}", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 14f;
            le.preferredHeight = 16f;

            // Image acts as the click target; transparent so only the arrow shows.
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.001f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = arrow;
            label.fontSize = 14f;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            label.font = GetGameFont();

            return btn;
        }

        private static TMP_FontAsset? GetGameFont()
        {
            if (_cachedFont != null) return _cachedFont;
            // Borrow the font from any existing TMPro component in the scene.
            // Use FindObjectsOfType (plural) with the inactive flag to find one
            // even when the buildings window is the first TMPro to come online.
            var all = Object.FindObjectsOfType<TextMeshProUGUI>(includeInactive: true);
            foreach (var t in all)
            {
                if (t != null && t.font != null) { _cachedFont = t.font; break; }
            }
            // Last-ditch fallback to TMP's global default.
            if (_cachedFont == null) _cachedFont = TMP_Settings.defaultFontAsset;
            return _cachedFont;
        }
    }
}
