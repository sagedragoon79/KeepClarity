using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Custom population cap. The game's slider only offers fixed stops
    /// (200, 500, 1000, 1500, 2000, 2500, unlimited), but SettingsManager.popCap
    /// accepts any int, and every enforcement point reads it. When KC's
    /// <see cref="FFUIOverhaulMod.CustomPopulationCap"/> is &gt; 0, KC keeps
    /// popCap at that value and makes it a hard cap.
    ///
    /// WHY IT KEPT SLIPPING (fixed 2026-09-27): popCap is a global PlayerPrefs
    /// value, and UISettingsPanel.SetInitialState — run by the panel's Start and
    /// by EVERY UIPauseWindow.Open — looks the value up among the slider stops.
    /// A custom value isn't one, so it resets the cap to the default stop (500)
    /// and saves that. One press of Esc undid the override until the next load.
    ///
    /// WHY THE TOWN COULD PASS THE CAP EVEN WHEN IT HELD: the game only caps
    /// births and the seasonal-immigration roll. It applies the tech tree's
    /// immigration group-size bonus AFTER that check, and immigrant groups
    /// (random events and immigration quests) never check the cap at all —
    /// accepting one adds the whole group. The patches below close all three,
    /// only while a custom cap is set; the game's own slider behaves as shipped.
    /// Divine Hands' villager spawner is untouched: it calls
    /// SpawnVillagerImmigration outside the population tick.
    /// </summary>
    internal static class PopulationCapOverride
    {
        /// <summary>The custom cap, or 0 when the player uses the game's slider.</summary>
        internal static int Cap => FFUIOverhaulMod.CustomPopulationCap?.Value ?? 0;

        private static float _nextCheck;
        private static int _loggedCap;

        /// <summary>Called on each map load.</summary>
        public static void ApplyIfSet() => Apply(Cap);

        /// <summary>Safety net from OnUpdate: re-asserts the cap every two
        /// seconds, which also makes a change in KC's settings apply live.</summary>
        public static void Tick()
        {
            if (Cap <= 0 || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 2f;
            Apply(Cap);
        }

        internal static void Apply(int wanted)
        {
            if (wanted <= 0) return;
            try
            {
                var sm = UnitySingletonPersistent<SettingsManager>.Instance;
                if (sm == null || sm.popCap == wanted) return;
                int was = sm.popCap;
                sm.popCap = wanted;
                // Log once per value; the pause menu resets it on every open.
                if (_loggedCap != wanted)
                {
                    _loggedCap = wanted;
                    FFUIOverhaulMod.Log.Msg($"[PopCap] Population cap set to {wanted} (was {was}).");
                }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[PopCap] Override failed: {e.Message}");
            }
        }

        internal static int VillagerCount()
        {
            try { return (int)(UnitySingleton<GameManager>.Instance?.resourceManager?.villagerCount ?? 0u); }
            catch { return 0; }
        }

        private static FieldInfo? _pendingField;

        /// <summary>VillagerPopulationManager.potentialImmigrants: the group
        /// walking in or waiting on the accept / turn away decision.</summary>
        internal static List<PotentialImmigrant>? Pending(VillagerPopulationManager vpm)
        {
            if (_pendingField == null)
                _pendingField = AccessTools.Field(typeof(VillagerPopulationManager), "potentialImmigrants");
            return _pendingField?.GetValue(vpm) as List<PotentialImmigrant>;
        }
    }

    /// <summary>
    /// Keep the custom cap when the game's settings panel initializes (its
    /// Start, and every pause-menu open). The panel has just reset popCap to a
    /// slider stop; put the custom value back, lock the slider, and label it so
    /// the player can see where the number comes from.
    /// </summary>
    [HarmonyPatch(typeof(UISettingsPanel), "SetInitialState")]
    public class PatchSettingsPanelKeepsCustomCap
    {
        private static FieldInfo? _slider;
        private static FieldInfo? _text;
        private static bool _loggedError;

        public static void Postfix(UISettingsPanel __instance)
        {
            try
            {
                if (_slider == null) _slider = AccessTools.Field(typeof(UISettingsPanel), "popCapSlider");
                if (_text == null) _text = AccessTools.Field(typeof(UISettingsPanel), "popCapText");
                var slider = _slider?.GetValue(__instance) as UnityEngine.UI.Slider;

                int cap = PopulationCapOverride.Cap;
                if (cap <= 0)
                {
                    if (slider != null && !slider.interactable) slider.interactable = true;
                    return;
                }

                PopulationCapOverride.Apply(cap);
                if (slider != null) slider.interactable = false;
                if (_text?.GetValue(__instance) is TMP_Text label) label.text = cap + " (Keep Clarity)";
            }
            catch (Exception e)
            {
                if (_loggedError) return;
                _loggedError = true;
                FFUIOverhaulMod.Log.Warning($"[PopCap] Settings panel patch failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Births and seasonal immigrants: count what the population tick adds and
    /// stop at the cap. The game caps its seasonal roll, then applies the tech
    /// tree's group-size bonus on top, so a group near the cap can overshoot.
    /// The budget is only live inside UpdatePopulation, so other callers of
    /// the spawn methods (Divine Hands' spawner, cheats) are never limited.
    /// </summary>
    [HarmonyPatch(typeof(VillagerPopulationManager), "UpdatePopulation")]
    public class PatchPopulationTickBudget
    {
        internal static bool Active;
        internal static int Budget;
        internal static int Blocked;

        public static void Prefix()
        {
            int cap = PopulationCapOverride.Cap;
            Active = cap > 0;
            Budget = Active ? cap - PopulationCapOverride.VillagerCount() : 0;
            Blocked = 0;
        }

        public static Exception? Finalizer(Exception? __exception)
        {
            if (Active && Blocked > 0)
                FFUIOverhaulMod.Log.Msg($"[PopCap] Cap {PopulationCapOverride.Cap} reached: {Blocked} arrival(s) held back this season.");
            Active = false;
            return __exception;
        }

        /// <summary>True if one more villager fits this tick (and counts it).</summary>
        internal static bool TakeSlot()
        {
            if (!Active) return true;
            if (Budget > 0) { Budget--; return true; }
            Blocked++;
            return false;
        }
    }

    [HarmonyPatch(typeof(VillagerPopulationManager), "SpawnVillagerImmigration")]
    public class PatchCapSeasonalImmigrant
    {
        public static bool Prefix(ref Villager? __result)
        {
            if (PatchPopulationTickBudget.TakeSlot()) return true;
            __result = null;   // the tick's spawn loop ignores the return value
            return false;
        }
    }

    [HarmonyPatch(typeof(VillagerPopulationManager), "SpawnVillagerBirth")]
    public class PatchCapBirth
    {
        public static bool Prefix(ref bool __result)
        {
            if (PatchPopulationTickBudget.TakeSlot()) return true;
            __result = false;  // "no birth": the caller then skips the mother's childbirth roll
            return false;
        }
    }

    /// <summary>
    /// No random immigration event while the town is full: a group that
    /// arrives only to be turned away still pauses the game and opens the
    /// decision window. Random events carry no quest state, so skipping one is
    /// safe (the game rolls again next time). Cheat-spawned groups pass.
    /// </summary>
    [HarmonyPatch(typeof(VillagerPopulationManager), "SpawnRandomImmigrationImmigrants")]
    public class PatchSkipRandomImmigrationWhenFull
    {
        public static bool Prefix(bool isCheat)
        {
            int cap = PopulationCapOverride.Cap;
            if (isCheat || cap <= 0) return true;
            return PopulationCapOverride.VillagerCount() < cap;
        }
    }

    /// <summary>
    /// Immigrant groups (random events and immigration quests) never check the
    /// cap. Each member registers here from its Start() the frame after the
    /// group spawns, so this is where a group is trimmed to the room left: the
    /// extras turn around and leave. At least one always stays, because an
    /// immigration quest only closes after the player accepts or turns the
    /// group away — trim a quest group to zero and the game never offers
    /// another quest.
    /// </summary>
    [HarmonyPatch(typeof(VillagerPopulationManager), "AddPotentialImmigrant")]
    public class PatchCapArrivingImmigrants
    {
        public static void Postfix(VillagerPopulationManager __instance, PotentialImmigrant potentialImmigrant)
        {
            int cap = PopulationCapOverride.Cap;
            if (cap <= 0 || potentialImmigrant == null) return;
            try
            {
                var pending = PopulationCapOverride.Pending(__instance);
                if (pending == null) return;
                int room = Math.Max(cap - PopulationCapOverride.VillagerCount(), 1);
                if (pending.Count <= room) return;
                pending.Remove(potentialImmigrant);
                potentialImmigrant.LeaveVillage();
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[PopCap] Immigrant trim failed: {e.Message}"); }
        }
    }

    /// <summary>
    /// Last check when the player accepts a group: only as many join as fit
    /// under the cap (the town may have grown while they walked in); the rest
    /// leave. The game then naturalizes whoever is still in the list.
    /// </summary>
    [HarmonyPatch(typeof(VillagerPopulationManager), "AcceptImmigrants")]
    public class PatchCapAcceptedImmigrants
    {
        public static void Prefix(VillagerPopulationManager __instance)
        {
            int cap = PopulationCapOverride.Cap;
            if (cap <= 0) return;
            try
            {
                var pending = PopulationCapOverride.Pending(__instance);
                if (pending == null) return;
                int room = Math.Max(cap - PopulationCapOverride.VillagerCount(), 0);
                int kept = 0, turnedAway = 0;
                for (int i = 0; i < pending.Count; i++)
                {
                    var p = pending[i];
                    if (p == null || p.gameObject == null) continue;
                    if (kept < room) { kept++; continue; }
                    p.LeaveVillage();
                    pending.RemoveAt(i);
                    i--;
                    turnedAway++;
                }
                if (turnedAway > 0)
                    FFUIOverhaulMod.Log.Msg($"[PopCap] Cap {cap} reached: {kept} immigrant(s) joined, {turnedAway} turned away.");
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[PopCap] Accept trim failed: {e.Message}"); }
        }
    }

    /// <summary>
    /// Skip the population requirement on building upgrade gating. Stashes
    /// the upgrade requirement's population value to 0 around vanilla's
    /// CheckForUpgradeAvailability call, then restores it. The line we're
    /// neutralising is:
    ///
    ///   bool flag5 = currentPopCount &gt;= upgradeRequirement.population;
    ///
    /// With population = 0 during the check, flag5 is always true. Restoring
    /// after the check means UI widgets that read upgradeRequirement.population
    /// for display elsewhere see the original value.
    ///
    /// Single-threaded (Unity main thread): the prefix→method→postfix pair
    /// runs without interleaving from other building checks.
    /// </summary>
    [HarmonyPatch(typeof(BuildingUpgradeInfo), "CheckForUpgradeAvailability")]
    public class PatchIgnoreUpgradePopRequirement
    {
        private static FieldInfo? _reqField; // _upgradeRequirements on BuildingUpgradeInfo
        private static FieldInfo? _populationField; // population on BuildingUpgradeRequirements

        public static void Prefix(BuildingUpgradeInfo __instance, out int __state)
        {
            __state = -1; // sentinel: no stash performed
            if (FFUIOverhaulMod.IgnoreUpgradePopulationRequirement == null
                || !FFUIOverhaulMod.IgnoreUpgradePopulationRequirement.Value)
                return;

            try
            {
                var req = ResolveRequirement(__instance);
                if (req == null) return;
                if (_populationField == null) return;
                __state = (int)(_populationField.GetValue(req) ?? 0);
                if (__state == 0) return; // already 0; no swap needed
                _populationField.SetValue(req, 0);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[IgnorePopReq] Prefix failed: {e.Message}");
            }
        }

        public static void Postfix(BuildingUpgradeInfo __instance, int __state)
        {
            if (__state <= 0) return;
            try
            {
                var req = ResolveRequirement(__instance);
                if (req == null || _populationField == null) return;
                _populationField.SetValue(req, __state);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[IgnorePopReq] Postfix restore failed: {e.Message}");
            }
        }

        private static BuildingUpgradeRequirements? ResolveRequirement(BuildingUpgradeInfo info)
        {
            // upgradeRequirement is a property on BuildingUpgradeInfo that
            // returns the private _upgradeRequirements ScriptableObject. We
            // mutate via reflection on the underlying field to avoid setter
            // side effects (none currently, but defensive).
            if (_reqField == null)
                _reqField = typeof(BuildingUpgradeInfo).GetField(
                    "_upgradeRequirements", BindingFlags.Instance | BindingFlags.NonPublic);
            var req = _reqField?.GetValue(info) as BuildingUpgradeRequirements;
            if (req == null) return null;

            if (_populationField == null)
                _populationField = typeof(BuildingUpgradeRequirements).GetField(
                    "population", BindingFlags.Instance | BindingFlags.Public);
            return req;
        }
    }
}
