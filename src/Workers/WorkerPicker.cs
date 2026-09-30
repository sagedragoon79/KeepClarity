using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Workers
{
    /// <summary>
    /// Worker Picker: right-click any worker slot in a building's window to
    /// choose who works there — fill an empty slot, or replace the worker
    /// already in it.
    ///
    /// The game has the picker (UIVillagerList) but only opens it from a
    /// fill-vacancy click when no laborers are free; with laborers around, the
    /// click just drafts one. So a right-click opens the same window for any
    /// slot, and confirming runs the game's own hire path (SetOccupation +
    /// Building.RequestToWorkAt) — the same calls UIOccupancyInfo's
    /// OnVillagerSelected makes.
    ///
    /// REPLACING: the current worker is fired first (Villager.FireWorker, the
    /// slot's own X button) so the slot is free, and becomes a laborer. If the
    /// hire then fails they are put straight back.
    ///
    /// SAME-TRADE CANDIDATES: the game's list leaves out villagers who already
    /// hold this building's job, so you can't move a woodcutter from one camp to
    /// another. When KC opens the picker it adds them back (never the building's
    /// own workers). Such a move steps the villager out of the old post first,
    /// so they start clean here rather than finishing a task for the old one.
    ///
    /// Left-click is untouched: a filled slot still selects the villager, an
    /// empty one still drafts a laborer.
    /// </summary>
    internal static class WorkerPicker
    {
        private static bool _initialized;
        private static bool _loggedError;

        // UIVillagerWindowResident (the slot widget)
        private static FieldInfo? _slotAssigned;    // protected IHasAssignedSlots hasAssignedSlots
        private static FieldInfo? _slotVillager;    // protected Villager villagerComp
        private static FieldInfo? _slotButton;      // private Button button
        private static FieldInfo? _slotFillButton;  // private Button fillVacancyButton

        // UIVillagerList (the picker window)
        internal static FieldInfo? ListData;        // List<UIVillagerData> allVillagerData
        internal static FieldInfo? ListSelected;    // UIVillagerData selectedVillagerData
        internal static FieldInfo? ListScroll;      // UIVillagerScrollView villagerScrollView
        internal static FieldInfo? ListMain;        // RectTransform mainTransform
        internal static FieldInfo? ListConfirm;     // Button confirmButton
        internal static readonly List<FieldInfo> ListInvertFlags = new List<FieldInfo>();
        private static FieldInfo? _listTitle;       // TextMeshProUGUI titleText
        private static FieldInfo? _listMaxHeight;   // float maxHeight
        private static FieldInfo? _listInvertName;  // bool invertSortByName
        private static MethodInfo? _listSortByName; // void OnSortByName()

        /// <summary>Set only while KC itself is opening the picker, so the
        /// postfix can tell our session from the game's own.</summary>
        private static Session? _opening;

        private sealed class Session
        {
            public Building Building = null!;
            public Villager? Replace;
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var slotT = typeof(UIVillagerWindowResident);
                _slotAssigned = AccessTools.Field(slotT, "hasAssignedSlots");
                _slotVillager = AccessTools.Field(slotT, "villagerComp");
                _slotButton = AccessTools.Field(slotT, "button");
                _slotFillButton = AccessTools.Field(slotT, "fillVacancyButton");

                var listT = typeof(UIVillagerList);
                ListData = AccessTools.Field(listT, "allVillagerData");
                ListSelected = AccessTools.Field(listT, "selectedVillagerData");
                ListScroll = AccessTools.Field(listT, "villagerScrollView");
                ListMain = AccessTools.Field(listT, "mainTransform");
                _listTitle = AccessTools.Field(listT, "titleText");
                ListConfirm = AccessTools.Field(listT, "confirmButton");
                _listMaxHeight = AccessTools.Field(listT, "maxHeight");
                _listInvertName = AccessTools.Field(listT, "invertSortByName");
                _listSortByName = AccessTools.Method(listT, "OnSortByName");
                foreach (var name in new[] { "invertSortByName", "invertSortByProf", "invertSortByEducation", "invertSortByStatus", "invertSortByCommute" })
                {
                    var f = AccessTools.Field(listT, name);
                    if (f != null) ListInvertFlags.Add(f);
                }

                var h = new HarmonyLib.Harmony("FFUIOverhaul.WorkerPicker");
                h.Patch(AccessTools.Method(slotT, "SetOccupant"),
                    postfix: new HarmonyMethod(typeof(WorkerPicker), nameof(SetOccupantPostfix)));
                h.Patch(AccessTools.Method(listT, "SeekEmploymentForPlaceOfWork"),
                    prefix: new HarmonyMethod(typeof(WorkerPicker), nameof(SeekPrefix)),
                    postfix: new HarmonyMethod(typeof(WorkerPicker), nameof(SeekPostfix)));

                PickerMasteryColumn.Initialize(h);
                SlotMastery.Initialize(h);

                bool fieldsOk = _slotAssigned != null && _slotVillager != null && ListData != null && _listSortByName != null;
                FFUIOverhaulMod.Log.Msg("[WorkerPicker] patched (right-click a worker slot to choose a villager)"
                    + (fieldsOk ? "." : " — some fields missing, feature may be limited."));
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[WorkerPicker] init failed: " + e.Message);
            }
        }

        // ── slot hook ──────────────────────────────────────────────────────

        /// <summary>Slots are pooled and re-bound through SetOccupant, so this
        /// is the one place every worker slot passes through. The click handler
        /// goes on the slot and on both of its buttons: a click is delivered to
        /// the first object up the hierarchy that handles clicks, which is one of
        /// the buttons whenever the pointer is over one.</summary>
        private static void SetOccupantPostfix(UIVillagerWindowResident __instance)
        {
            try
            {
                if (__instance == null) return;
                Attach(__instance.gameObject);
                if (_slotButton?.GetValue(__instance) is Button b) Attach(b.gameObject);
                if (_slotFillButton?.GetValue(__instance) is Button fb) Attach(fb.gameObject);
            }
            catch { }
        }

        private static void Attach(GameObject go)
        {
            if (go != null && go.GetComponent<SlotRightClick>() == null)
                go.AddComponent<SlotRightClick>();
        }

        internal static void OnSlotRightClicked(UIVillagerWindowResident slot)
        {
            if (!FFUIOverhaulMod.EnableWorkerPicker.Value) return;
            try
            {
                if (slot == null || slot.isVisitor) return;
                var building = _slotAssigned?.GetValue(slot) as Building;
                if (building == null || !IsCivilianWorkplace(building)) return;

                Villager? replace = null;
                if (_slotVillager?.GetValue(slot) is Villager occupant)
                {
                    // Residences list their residents in the same rows; only
                    // someone who works HERE can be replaced.
                    if (!ReferenceEquals(occupant.placeOfWork, building)) return;
                    replace = occupant;
                }
                else
                {
                    // An empty row is either an open slot or one the player
                    // switched off with the minus button; only open ones take a
                    // worker. Then the same check and feedback as the game's own
                    // fill-vacancy click.
                    if (_slotButton?.GetValue(slot) is Button btn && !btn.interactable) return;
                    if (!building.CanRecruitAnotherWorker(out var reason))
                    {
                        ShowRecruitFailure(reason);
                        return;
                    }
                }

                Open(building, replace);
            }
            catch (Exception e) { LogOnce("right-click: " + e.Message); }
        }

        private static bool IsCivilianWorkplace(Building b)
        {
            var occ = b.employmentOccupation;
            // Soldiers are recruited through the barracks' own panel.
            return b.HasOccupation()
                && occ != VillagerOccupation.Occupation.Soldier
                && occ != VillagerOccupation.Occupation.TransitionToSoldier;
        }

        private static void Open(Building building, Villager? replace)
        {
            var wm = UnitySingleton<GameManager>.Instance?.uiManager?.windowManager;
            var win = wm?.villagerListWindow;
            if (wm == null || win == null) return;

            if (!win.isOpen) wm.ToggleMenu(win);

            var session = new Session { Building = building, Replace = replace };
            _opening = session;
            try
            {
                win.SeekEmploymentForPlaceOfWork(building, v => OnPicked(session, v));
            }
            finally { _opening = null; }
        }

        // ── picker hooks ───────────────────────────────────────────────────

        /// <summary>Runs before the game fills the list, so the mastery column
        /// is in the right state when the rows first bind.</summary>
        private static void SeekPrefix(UIVillagerList __instance, IPlaceOfWork placeOfWork)
        {
            PickerMasteryColumn.BeforeOpen(__instance, placeOfWork);
        }

        /// <summary>Runs after the game has filled and name-sorted the list, for
        /// every picker: KC's own opens get the same-trade villagers and the
        /// replace title; every open with the mastery column gets the
        /// best-for-this-job-first sort.</summary>
        private static void SeekPostfix(UIVillagerList __instance, IPlaceOfWork placeOfWork)
        {
            var session = _opening;
            bool ours = session != null && ReferenceEquals(session.Building, placeOfWork);
            try
            {
                if (ours) AddSameTradeWorkers(__instance, session!.Building);
                PickerMasteryColumn.SortOnOpen(__instance);
                if (ours && session!.Replace != null) SetReplaceTitle(__instance, session);
            }
            catch (Exception e) { LogOnce("picker list: " + e.Message); }
        }

        /// <summary>Adds villagers who already hold this building's job at a
        /// different building, then re-runs the game's own name sort, selection
        /// and sizing so the list reads exactly like a vanilla one.</summary>
        private static void AddSameTradeWorkers(UIVillagerList win, Building building)
        {
            var data = ListData?.GetValue(win) as List<UIVillagerData>;
            var rm = UnitySingleton<GameManager>.Instance?.resourceManager;
            if (data == null || rm?.villagersRO == null) return;

            var occ = building.employmentOccupation;
            var listed = new HashSet<Villager>();
            foreach (var d in data) if (d?.villager != null) listed.Add(d.villager);

            int added = 0;
            foreach (var v in rm.villagersRO)
            {
                if (v == null || v.isDead || !v.IsOfWorkingAge()) continue;
                if (v.GetOccupation() != occ) continue;                   // every other trade is already listed
                if (ReferenceEquals(v.placeOfWork, building)) continue;   // already works here
                if (listed.Contains(v)) continue;
                data.Add(new UIVillagerData(v, building));
                added++;
            }
            if (added == 0) return;

            // OnSortByName flips its direction flag on every call; reset it so this
            // sorts A-Z just like the call the game made a moment ago.
            _listInvertName?.SetValue(win, false);
            _listSortByName?.Invoke(win, null);

            var first = data.Count > 0 ? data[0] : null;
            ListSelected?.SetValue(win, first);
            if (ListConfirm?.GetValue(win) is Button confirm)
                confirm.interactable = first != null && first.isValid;

            Resize(win, data.Count);
        }

        /// <summary>The game's own height formula from SeekEmploymentForPlaceOfWork,
        /// re-run for the longer list.</summary>
        private static void Resize(UIVillagerList win, int count)
        {
            var sv = ListScroll?.GetValue(win) as UIVillagerScrollView;
            var main = ListMain?.GetValue(win) as RectTransform;
            if (sv == null || main == null) return;
            float maxH = _listMaxHeight?.GetValue(win) is float f ? f : 600f;
            float h = count * sv.cellSize + 150f + (count - 1) * sv.Spacing + sv.PaddingBottom + sv.PaddingTop;
            main.sizeDelta = new Vector2(main.sizeDelta.x, Mathf.Min(h, maxH));
            sv.ScrollTo(0, 0f);
        }

        /// <summary>"Replace Ormond Hale (+3%) at Woodcutter's Camp" — the
        /// current worker's mastery is the number the candidates have to beat.</summary>
        private static void SetReplaceTitle(UIVillagerList win, Session s)
        {
            if (!(_listTitle?.GetValue(win) is TMP_Text title) || s.Replace == null) return;
            string who = s.Replace.villagerName;
            if (PickerMasteryColumn.Visible)
                who += " (+" + EpMastery.Current(s.Replace).ToString("0.#") + "%)";
            string template = Localization.KcLoc.Tr("KeepClarity/picker/replaceTitle", "Replace {0} at {1}");
            try { title.text = string.Format(template, who, s.Building.displayName); }
            catch (FormatException) { title.text = "Replace " + who + " at " + s.Building.displayName; }
        }

        // ── hiring ─────────────────────────────────────────────────────────

        /// <summary>The picker's confirm callback. Returning true closes the
        /// picker, false leaves it open (the game's contract).</summary>
        private static bool OnPicked(Session s, Villager chosen)
        {
            try
            {
                var b = s.Building;
                if (chosen == null || b == null) return false;
                if (ReferenceEquals(chosen.placeOfWork, b)) return true;   // already works here

                // The worker being replaced may have died or left since the
                // picker opened; then this is a plain fill.
                var displaced = s.Replace;
                if (displaced != null && (displaced.isDead || !ReferenceEquals(displaced.placeOfWork, b)))
                    displaced = null;

                if (displaced == null && !b.CanRecruitAnotherWorker(out var reason))
                {
                    ShowRecruitFailure(reason);
                    return false;
                }

                if (displaced != null) displaced.FireWorker(b);   // frees the slot; they become a laborer

                if (Assign(chosen, b))
                {
                    FFUIOverhaulMod.Log.Msg(displaced != null
                        ? $"[WorkerPicker] {chosen.villagerName} replaced {displaced.villagerName} at {b.displayName}."
                        : $"[WorkerPicker] {chosen.villagerName} now works at {b.displayName}.");
                    return true;
                }

                if (displaced != null) Rehire(displaced, b);
                FFUIOverhaulMod.Log.Warning($"[WorkerPicker] {b.displayName} did not accept {chosen.villagerName}; nothing changed.");
                return false;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[WorkerPicker] assign failed: " + e.Message);
                return false;
            }
        }

        /// <summary>UIOccupancyInfo.OnVillagerSelected, with one addition: a
        /// villager already in this trade at another building steps out of that
        /// post first.</summary>
        private static bool Assign(Villager v, Building b)
        {
            var occ = b.employmentOccupation;
            var prevOcc = v.GetOccupation();
            var prevPlace = v.placeOfWork;
            bool hadPlace = !IsNull(prevPlace);

            if (prevOcc == occ && hadPlace) v.FireWorker(prevPlace);

            v.SetOccupation(occ);
            if (b.RequestToWorkAt(v))
            {
                // The game's own picker does this when it pulls a farmer off the
                // fields, so the farm doesn't draft a replacement.
                if (prevOcc == VillagerOccupation.Occupation.Farmer && occ != VillagerOccupation.Occupation.Farmer)
                    UnitySingleton<GameManager>.Instance?.agricultureManager?.DecrementFarmerCount();
                return true;
            }

            v.SetOccupation(prevOcc);
            if (hadPlace) prevPlace.RequestToWorkAt(v);
            return false;
        }

        private static void Rehire(Villager v, Building b)
        {
            v.SetOccupation(b.employmentOccupation);
            if (!b.RequestToWorkAt(v))
                v.SetOccupation(VillagerOccupation.Occupation.Laborer);
        }

        private static void ShowRecruitFailure(string? reason)
        {
            var fb = UnitySingleton<GameManager>.Instance?.uiManager?.directFeedback;
            if (fb == null) return;
            if (reason == "notEnoughGold") fb.AddFeedback(DirectFeedbackDisplay.FeedbackType.NOT_ENOUGH_GOLD);
            else if (reason == "noEducatedVillagers") fb.AddFeedback(DirectFeedbackDisplay.FeedbackType.NO_EDUCATED_VILLAGERS);
        }

        /// <summary>Interface references to destroyed Unity objects aren't
        /// null to C#; this catches both.</summary>
        private static bool IsNull(object? o) => o == null || (o is UnityEngine.Object u && u == null);

        private static void LogOnce(string msg)
        {
            if (_loggedError) return;
            _loggedError = true;
            FFUIOverhaulMod.Log.Warning("[WorkerPicker] " + msg);
        }
    }

    /// <summary>Right-click receiver on a worker slot. Left clicks are ignored
    /// here and reach the slot's own Button as before.</summary>
    internal sealed class SlotRightClick : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right) return;
            var slot = GetComponentInParent<UIVillagerWindowResident>();
            if (slot != null) WorkerPicker.OnSlotRightClicked(slot);
        }
    }
}
