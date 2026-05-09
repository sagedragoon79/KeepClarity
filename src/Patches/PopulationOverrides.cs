using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Custom population cap. The game ships with fixed slider stops (200,
    /// 500, 1000, 1500, 2000, 2500, ~unlimited) but the underlying popCap
    /// setter on SettingsManager accepts any int. When KC's
    /// <see cref="FFUIOverhaulMod.CustomPopulationCap"/> is &gt; 0, we write
    /// it to SettingsManager.popCap on every Frontier scene init so the
    /// player's chosen value wins regardless of what the slider was last set
    /// to.
    /// </summary>
    internal static class PopulationCapOverride
    {
        public static void ApplyIfSet()
        {
            int wanted = FFUIOverhaulMod.CustomPopulationCap?.Value ?? 0;
            if (wanted <= 0) return;

            try
            {
                var sm = UnitySingletonPersistent<SettingsManager>.Instance;
                if (sm == null) return;
                if (sm.popCap == wanted) return;
                sm.popCap = wanted;
                FFUIOverhaulMod.Log.Msg($"[PopCap] Overrode population cap to {wanted}");
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[PopCap] Override failed: {e.Message}");
            }
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
