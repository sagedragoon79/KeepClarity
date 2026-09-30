using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Workers
{
    /// <summary>
    /// "+12% Mastery" on each filled worker slot in a building's window: the
    /// worker's Essential Provisions Workplace Mastery in the job they hold
    /// there, so you can judge them before replacing them.
    ///
    /// LAYOUT (OccupantSlotSlim prefab, sharedassets0): a 262x55 slot with the
    /// portrait on the left, the name over the health bar on top (y +17), the
    /// status text in the middle (y -4) and the alert icons on the bottom line
    /// (y -22), laid out from the left. The label sits at the right end of that
    /// bottom line, which stays empty unless a villager has five or more alerts.
    ///
    /// Refreshed when the slot is bound (SetOccupant) and on the game's own
    /// round-robin UpdateStatus (one slot a second); hidden by the three paths
    /// that empty or switch off a slot. Only a worker of THIS building gets a
    /// label: residences list residents in the same slots, and patients are
    /// visitors.
    /// </summary>
    internal static class SlotMastery
    {
        private const string LabelName = "KC Slot Mastery";
        private const float FontSize = 12f;

        private static readonly Color ValueColor = new Color32(0x7f, 0xbf, 0x7f, 0xff);   // KC's bonus green
        private static readonly Color DimColor = new Color(0.91f, 0.87f, 0.79f, 0.45f);

        private static FieldInfo? _villager;    // protected Villager villagerComp
        private static FieldInfo? _assigned;    // protected IHasAssignedSlots hasAssignedSlots
        private static FieldInfo? _context;     // protected GameObject contextObject
        private static FieldInfo? _statusText;  // private TextMeshProUGUI residentStatusText
        private static bool _loggedError;

        public static void Initialize(HarmonyLib.Harmony h)
        {
            var t = typeof(UIVillagerWindowResident);
            _villager = AccessTools.Field(t, "villagerComp");
            _assigned = AccessTools.Field(t, "hasAssignedSlots");
            _context = AccessTools.Field(t, "contextObject");
            _statusText = AccessTools.Field(t, "residentStatusText");

            var refresh = new HarmonyMethod(typeof(SlotMastery), nameof(RefreshPostfix));
            h.Patch(AccessTools.Method(t, "SetOccupant"), postfix: refresh);
            h.Patch(AccessTools.Method(t, "UpdateStatus"), postfix: refresh);

            var hide = new HarmonyMethod(typeof(SlotMastery), nameof(HidePostfix));
            foreach (var name in new[] { "UserDisableSlot", "InvalidateSlot", "DisableSlot" })
            {
                var m = AccessTools.Method(t, name);
                if (m != null) h.Patch(m, postfix: hide);
            }
        }

        private static void RefreshPostfix(UIVillagerWindowResident __instance)
        {
            try
            {
                if (__instance == null) return;
                var worker = WorkerHere(__instance);
                bool show = worker != null && FFUIOverhaulMod.EnableSlotMastery.Value && EpMastery.Active;

                var label = FindLabel(__instance);
                if (!show)
                {
                    if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
                    return;
                }

                if (label == null) label = CreateLabel(__instance);
                if (label == null) return;

                float pct = EpMastery.Current(worker!);
                string text = Format(pct);
                if (label.text != text) label.text = text;     // UpdateStatus runs every second; skip the re-mesh
                label.color = pct >= 0.05f ? ValueColor : DimColor;
                if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            }
            catch (Exception e) { LogOnce(e.Message); }
        }

        private static void HidePostfix(UIVillagerWindowResident __instance)
        {
            try
            {
                var label = __instance != null ? FindLabel(__instance) : null;
                if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
            }
            catch { }
        }

        /// <summary>The villager in this slot, if the slot currently shows
        /// someone who works at the slot's building; otherwise null.</summary>
        private static Villager? WorkerHere(UIVillagerWindowResident slot)
        {
            if (slot.isVisitor) return null;
            if (!(_context?.GetValue(slot) is GameObject ctx) || ctx == null) return null;   // emptied / switched off
            if (!(_villager?.GetValue(slot) is Villager v) || v == null) return null;
            if (!(_assigned?.GetValue(slot) is Building b) || b == null) return null;
            if (!ReferenceEquals(v.placeOfWork, b)) return null;
            var occ = b.employmentOccupation;
            if (occ == VillagerOccupation.Occupation.None
                || occ == VillagerOccupation.Occupation.Soldier
                || occ == VillagerOccupation.Occupation.TransitionToSoldier) return null;
            return v;
        }

        private static string Format(float pct)
        {
            string value = (pct >= 0.05f ? pct : 0f).ToString("0.#");
            string template = Localization.KcLoc.Tr("KeepClarity/slot/mastery", "+{0}% Mastery");
            try { return string.Format(template, value); }
            catch (FormatException) { return "+" + value + "% Mastery"; }
        }

        private static TextMeshProUGUI? FindLabel(UIVillagerWindowResident slot)
        {
            var t = slot.transform.Find(LabelName);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private static TextMeshProUGUI? CreateLabel(UIVillagerWindowResident slot)
        {
            var go = new GameObject(LabelName, typeof(RectTransform));
            go.transform.SetParent(slot.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-6f, -20f);   // right end of the alert line
            rt.sizeDelta = new Vector2(120f, 16f);

            go.AddComponent<LayoutElement>().ignoreLayout = true;

            var t = go.AddComponent<TextMeshProUGUI>();
            if (_statusText?.GetValue(slot) is TextMeshProUGUI src && src != null)
            {
                t.font = src.font;
                t.fontSharedMaterial = src.fontSharedMaterial;
                t.fontStyle = src.fontStyle;
            }
            t.fontSize = FontSize;
            t.enableAutoSizing = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.MidlineRight;
            t.richText = false;
            t.raycastTarget = false;   // clicks go to the slot underneath
            t.text = "";
            return t;
        }

        private static void LogOnce(string msg)
        {
            if (_loggedError) return;
            _loggedError = true;
            FFUIOverhaulMod.Log.Warning("[WorkerPicker] slot mastery: " + msg);
        }
    }
}
