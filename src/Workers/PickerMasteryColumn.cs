using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Workers
{
    /// <summary>
    /// A "Mastery" column in the villager picker: each villager's top three
    /// jobs by Essential Provisions' Workplace Mastery, with the building's own
    /// job highlighted. The picker opens sorted by mastery in that job, best
    /// first; clicking the header reverses it.
    ///
    /// LAYOUT (read from the shipped prefabs in sharedassets1): the header row
    /// "Columns" and every "VillagerCell" row are HorizontalLayoutGroups of
    /// fixed-width columns separated by 10 px "Divider Layout" spacers. The
    /// window (MainPivot, 975 px) stretches the header; each cell is a fixed
    /// 930 px row centred in the list. So the column is a divider plus a
    /// fixed-width element inserted after Education in both rows, and the
    /// window and each cell grow by exactly the width they gained. Every vanilla
    /// column keeps its size and position.
    ///
    /// Soft-dep: EP's WorkInfoApi through <see cref="EpMastery"/>. The column
    /// appears only when EP is loaded AND its Workplace Mastery toggle is on;
    /// otherwise the picker is left exactly as shipped.
    /// </summary>
    internal static class PickerMasteryColumn
    {
        private const float ColumnWidth = 190f;
        private const float PercentWidth = 48f;
        private const float RowHeight = 18f;
        private const float FontSize = 13f;
        private const int Rows = 3;

        private const string ColumnName = "KC Mastery";
        private const string DividerName = "KC Mastery Divider";

        private static readonly Color PercentColor = new Color32(0x7f, 0xbf, 0x7f, 0xff);   // KC's bonus green
        private static readonly Color HighlightColor = new Color32(0xff, 0xd9, 0x66, 0xff);
        private static readonly Color DimColor = new Color(0.68f, 0.59f, 0.48f, 0.45f);
        private static Color _nameColor = new Color(0.68f, 0.59f, 0.48f, 1f);                 // the cell's Education text

        private static bool _visible;
        private static VillagerOccupation.Occupation _targetOcc = VillagerOccupation.Occupation.None;
        private static bool _sortDescending = true;
        private static bool _loggedError;

        /// <summary>True while the column is shown for the current picker.</summary>
        internal static bool Visible => _visible;

        private static readonly Dictionary<string, string> _jobNames = new Dictionary<string, string>();
        private static string _jobNamesLang = "";

        public static void Initialize(HarmonyLib.Harmony h)
        {
            // UIVillagerCell is internal to the game, so it's patched by name.
            var cellT = AccessTools.TypeByName("UIVillagerCell");
            var m = cellT != null ? AccessTools.Method(cellT, "UpdateContent") : null;
            if (m == null)
            {
                FFUIOverhaulMod.Log.Warning("[WorkerPicker] UIVillagerCell.UpdateContent not found — mastery column disabled.");
                return;
            }
            h.Patch(m, postfix: new HarmonyMethod(typeof(PickerMasteryColumn), nameof(CellPostfix)));
        }

        // ── window ─────────────────────────────────────────────────────────

        /// <summary>Called before the game fills the picker. Decides whether the
        /// column shows for this open and adds or removes it from the header,
        /// resizing the window to match.</summary>
        internal static void BeforeOpen(UIVillagerList win, IPlaceOfWork placeOfWork)
        {
            try
            {
                _targetOcc = placeOfWork != null ? placeOfWork.employmentOccupation : VillagerOccupation.Occupation.None;
                _sortDescending = true;
                _visible = ShouldShow();
                ApplyHeader(win, _visible);
            }
            catch (Exception e)
            {
                _visible = false;
                LogOnce("header: " + e.Message);
            }
        }

        private static bool ShouldShow() =>
            FFUIOverhaulMod.EnablePickerMasteryColumn.Value && EpMastery.Active;

        private static void ApplyHeader(UIVillagerList win, bool show)
        {
            var cols = FindDeep(win.transform, "Columns");
            if (cols == null)
            {
                if (show) LogOnce("picker header row not found");
                _visible = false;
                return;
            }

            var header = cols.Find(ColumnName);
            if (header == null)
            {
                if (!show) return;
                header = CreateHeader(win, cols);
                if (header == null) { _visible = false; return; }
            }

            // Set every open: the language may have changed since the last one.
            if (show && header.GetComponent<TMP_Text>() is TMP_Text label)
                label.text = Localization.KcLoc.Tr("KeepClarity/picker/mastery", "Mastery");

            if (header.gameObject.activeSelf == show) return;

            var divider = cols.Find(DividerName);
            header.gameObject.SetActive(show);
            if (divider != null) divider.gameObject.SetActive(show);

            var main = WorkerPicker.ListMain?.GetValue(win) as RectTransform;
            if (main == null) main = FindDeep(win.transform, "MainPivot") as RectTransform;
            if (main != null)
            {
                float delta = AddedWidth(cols, divider);
                main.sizeDelta += new Vector2(show ? delta : -delta, 0f);
            }
        }

        /// <summary>Clone the Education header (and its divider) so the new
        /// column matches the others in font, colour and sort-button art. Built
        /// inactive; ApplyHeader switches it on and widens the window.</summary>
        private static Transform? CreateHeader(UIVillagerList win, Transform cols)
        {
            var edu = cols.Find("Education Text");
            var div = cols.Find("Divider Layout");
            if (edu == null || div == null)
            {
                LogOnce("picker header layout changed; mastery column skipped");
                return null;
            }

            var newDiv = UnityEngine.Object.Instantiate(div.gameObject, cols, false);
            newDiv.name = DividerName;
            newDiv.SetActive(false);
            newDiv.transform.SetSiblingIndex(edu.GetSiblingIndex() + 1);

            var hdr = UnityEngine.Object.Instantiate(edu.gameObject, cols, false);
            hdr.name = ColumnName;
            hdr.SetActive(false);
            hdr.transform.SetSiblingIndex(newDiv.transform.GetSiblingIndex() + 1);

            // The clone carries the game's I2 Localize component, which would
            // rewrite the text back to "Education" on every language event.
            foreach (var mb in hdr.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().FullName == "I2.Loc.Localize")
                    UnityEngine.Object.DestroyImmediate(mb);

            var le = hdr.GetComponent<LayoutElement>();
            if (le == null) le = hdr.AddComponent<LayoutElement>();
            le.minWidth = ColumnWidth;
            le.preferredWidth = ColumnWidth;
            le.flexibleWidth = 0f;

            var btnT = hdr.transform.Find("Education Button");
            var btn = btnT != null ? btnT.GetComponent<Button>() : hdr.GetComponentInChildren<Button>(true);
            if (btn != null)
            {
                btn.gameObject.name = "Mastery Button";
                btn.onClick = new Button.ButtonClickedEvent();   // drop anything copied from Education
                btn.onClick.AddListener(() => SortByMastery(win));
            }
            return hdr.transform;
        }

        // ── rows ───────────────────────────────────────────────────────────

        /// <summary>Runs on every (re)bind of a pooled row. Builds the column on
        /// first sight, matches its visibility (and the row's width) to the
        /// current open, then fills it.</summary>
        private static void CellPostfix(object __instance, UIVillagerData villagerData)
        {
            try
            {
                var root = (__instance as Component)?.transform;
                if (root == null) return;

                var col = root.Find(ColumnName);
                if (col == null)
                {
                    if (!_visible) return;
                    col = CreateCellColumn(root);
                    if (col == null) return;
                }

                if (col.gameObject.activeSelf != _visible)
                {
                    var divider = root.Find(DividerName);
                    col.gameObject.SetActive(_visible);
                    if (divider != null) divider.gameObject.SetActive(_visible);
                    if (root is RectTransform rt)
                    {
                        float delta = AddedWidth(root, divider);
                        rt.sizeDelta += new Vector2(_visible ? delta : -delta, 0f);
                    }
                }

                if (_visible) Fill(col, villagerData != null ? villagerData.villager : null);
            }
            catch (Exception e) { LogOnce("row: " + e.Message); }
        }

        private static Transform? CreateCellColumn(Transform root)
        {
            var edu = root.Find("Education Text");
            var div = root.Find("Divider Layout");
            if (edu == null || div == null)
            {
                LogOnce("picker row layout changed; mastery column skipped");
                return null;
            }

            var newDiv = UnityEngine.Object.Instantiate(div.gameObject, root, false);
            newDiv.name = DividerName;
            newDiv.SetActive(false);
            newDiv.transform.SetSiblingIndex(edu.GetSiblingIndex() + 1);

            var go = new GameObject(ColumnName, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(root, false);
            go.transform.SetSiblingIndex(newDiv.transform.GetSiblingIndex() + 1);

            var le = go.AddComponent<LayoutElement>();
            le.minWidth = ColumnWidth;
            le.preferredWidth = ColumnWidth;
            le.flexibleWidth = 0f;
            le.minHeight = RowHeight;
            le.flexibleHeight = 1f;   // fill the row's height (the row controls child height)

            var src = edu.GetComponent<TextMeshProUGUI>();
            if (src != null) _nameColor = src.color;

            var refs = go.AddComponent<MasteryCellRefs>();
            for (int i = 0; i < Rows; i++)
            {
                refs.Jobs[i] = MakeText(go.transform, src, "Job" + i, isPercent: false);
                refs.Percents[i] = MakeText(go.transform, src, "Pct" + i, isPercent: true);
            }
            return go.transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, TextMeshProUGUI? src, string name, bool isPercent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            if (isPercent)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(PercentWidth, RowHeight);
            }
            else
            {
                // Spans the column minus the percent area on the right.
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(-PercentWidth, RowHeight);
            }

            var t = go.AddComponent<TextMeshProUGUI>();
            if (src != null)
            {
                t.font = src.font;
                t.fontSharedMaterial = src.fontSharedMaterial;
                t.fontStyle = src.fontStyle;
            }
            t.fontSize = FontSize;
            t.enableAutoSizing = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.alignment = isPercent ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            t.richText = false;
            t.raycastTarget = false;
            t.color = _nameColor;
            t.text = "";
            return t;
        }

        private static void Fill(Transform col, Villager? v)
        {
            var refs = col.GetComponent<MasteryCellRefs>();
            if (refs == null) return;

            string[] names = v != null ? EpMastery.TopNames(v, Rows) : new string[0];
            float[] pcts = v != null ? EpMastery.TopPercents(v, Rows) : new float[0];
            int n = Math.Min(Rows, Math.Min(names.Length, pcts.Length));
            // EP lists a job from its first day; below 0.05 it would print "+0%".
            // The list is sorted high to low, so these only ever trim the end.
            while (n > 0 && pcts[n - 1] < 0.05f) n--;
            string target = _targetOcc.ToString();   // EP names jobs by their enum name

            // Centre the block vertically: 3 lines at +18/0/-18, 2 at +9/-9, 1 at 0.
            int lines = Math.Max(n, 1);
            for (int i = 0; i < Rows; i++)
            {
                var job = refs.Jobs[i];
                var pct = refs.Percents[i];
                if (job == null || pct == null) continue;

                float y = ((lines - 1) * 0.5f - i) * RowHeight;
                SetY(job, y);
                SetY(pct, y);

                if (i < n)
                {
                    bool isTarget = names[i] == target;
                    job.text = JobName(names[i]);
                    job.color = isTarget ? HighlightColor : _nameColor;
                    pct.text = "+" + pcts[i].ToString("0.#") + "%";
                    pct.color = isTarget ? HighlightColor : PercentColor;
                }
                else if (i == 0 && n == 0)
                {
                    job.text = "-";   // ASCII: FF's fonts lack some dash glyphs
                    job.color = DimColor;
                    pct.text = "";
                }
                else
                {
                    job.text = "";
                    pct.text = "";
                }
            }
        }

        private static void SetY(TMP_Text t, float y)
        {
            var rt = t.rectTransform;
            rt.anchoredPosition = new Vector2(0f, y);
        }

        // ── sorting ────────────────────────────────────────────────────────

        /// <summary>Called once the game has filled and name-sorted the list:
        /// re-sort best-for-this-job first and pre-select the top villager, the
        /// way the game pre-selects the first row after its own sort. The next
        /// header click then reverses the order.</summary>
        internal static void SortOnOpen(UIVillagerList win)
        {
            if (!_visible) return;
            try
            {
                var data = Sort(win, descending: true);
                if (data == null || data.Count == 0) return;

                var first = data[0];
                WorkerPicker.ListSelected?.SetValue(win, first);
                if (WorkerPicker.ListConfirm?.GetValue(win) is Button confirm)
                    confirm.interactable = first != null && first.isValid;
                if (WorkerPicker.ListScroll?.GetValue(win) is UIVillagerScrollView sv)
                    sv.ScrollTo(0, 0f);
            }
            catch (Exception e) { LogOnce("open sort: " + e.Message); }
        }

        /// <summary>Header click: flip the order. Mirrors the game's own sort
        /// buttons, which keep the selected villager in view.</summary>
        private static void SortByMastery(UIVillagerList win)
        {
            try
            {
                var data = Sort(win, _sortDescending);
                if (data == null) return;
                if (WorkerPicker.ListSelected?.GetValue(win) is UIVillagerData selected
                    && WorkerPicker.ListScroll?.GetValue(win) is UIVillagerScrollView sv)
                {
                    int i = data.IndexOf(selected);
                    if (i >= 0) sv.ScrollTo(i, 0f);
                }
            }
            catch (Exception e) { LogOnce("sort: " + e.Message); }
        }

        /// <summary>Sort by mastery in the building's own job (ties by name),
        /// reset the game's sort-direction flags like its own sort buttons do, and
        /// rebind the rows. Returns the sorted list, or null if the picker isn't
        /// in a state to sort.</summary>
        private static List<UIVillagerData>? Sort(UIVillagerList win, bool descending)
        {
            var data = WorkerPicker.ListData?.GetValue(win) as List<UIVillagerData>;
            var sv = WorkerPicker.ListScroll?.GetValue(win) as UIVillagerScrollView;
            if (data == null || sv == null) return null;

            string target = _targetOcc.ToString();
            var score = new Dictionary<UIVillagerData, float>();
            foreach (var d in data) score[d] = d != null && d.villager != null ? EpMastery.For(d.villager, target) : 0f;

            data.Sort((x, y) =>
            {
                int c = descending ? score[y].CompareTo(score[x]) : score[x].CompareTo(score[y]);
                return c != 0 ? c : string.Compare(x.villagerName, y.villagerName, StringComparison.CurrentCulture);
            });
            _sortDescending = !descending;

            foreach (var f in WorkerPicker.ListInvertFlags) f.SetValue(win, false);
            sv.UpdateData(data);
            return data;
        }

        // ── helpers ────────────────────────────────────────────────────────

        /// <summary>Width a row gains from the column: the column, its divider,
        /// and one more layout-group spacing gap on each side of the pair.</summary>
        private static float AddedWidth(Transform row, Transform? divider)
        {
            float spacing = row.GetComponent<HorizontalLayoutGroup>() is HorizontalLayoutGroup hlg ? hlg.spacing : 8f;
            float divWidth = 10f;
            if (divider != null && divider.GetComponent<LayoutElement>() is LayoutElement dle && dle.minWidth > 0f)
                divWidth = dle.minWidth;
            return ColumnWidth + divWidth + 2f * spacing;
        }

        /// <summary>The game's own job name for an occupation enum name (the key
        /// UIVillagerData uses), cached per language.</summary>
        private static string JobName(string enumName)
        {
            string lang = "";
            try { lang = I2.Loc.LocalizationManager.CurrentLanguage ?? ""; } catch { }
            if (lang != _jobNamesLang) { _jobNames.Clear(); _jobNamesLang = lang; }
            if (_jobNames.TryGetValue(enumName, out var cached)) return cached;

            string display;
            try
            {
                display = I2.Loc.LocalizationManager.TryGetTranslation("VillagerOccupation_" + enumName, out var t) && !string.IsNullOrEmpty(t)
                    ? t
                    : Prettify(enumName);
            }
            catch { display = Prettify(enumName); }
            _jobNames[enumName] = display;
            return display;
        }

        /// <summary>"NightsoilMan" -> "Nightsoil Man".</summary>
        private static string Prettify(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private static Transform? FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static void LogOnce(string msg)
        {
            if (_loggedError) return;
            _loggedError = true;
            FFUIOverhaulMod.Log.Warning("[WorkerPicker] mastery column " + msg);
        }
    }

    /// <summary>Holds a row's mastery text objects so a rebind doesn't search
    /// the hierarchy.</summary>
    internal sealed class MasteryCellRefs : MonoBehaviour
    {
        public TextMeshProUGUI?[] Jobs = new TextMeshProUGUI?[3];
        public TextMeshProUGUI?[] Percents = new TextMeshProUGUI?[3];
    }
}
