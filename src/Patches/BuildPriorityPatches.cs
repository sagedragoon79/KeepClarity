using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Build Priority (Option A — "full hijack"). Repurposes each build site's
    /// "builders assigned" up/down control as a 1-9 PRIORITY, and makes builders
    /// serve higher-priority sites first while every site can take its full
    /// (engine-designed) number of simultaneous builders ("swarm").
    ///
    /// How it works (all gated on Config.EnableBuildPriority, live — no restart):
    ///   1. get_maxBuilders -> PriorityMax(9). One patch fixes the whole UI: the
    ///      setter clamp, all 3 increment-button bounds, and the "X/9" display.
    ///   2. set_userDefinedBuilders -> floor at 1 (priority 1..9).
    ///   3. CanAcceptAnotherWorker -> cap by the REAL maxSimultaneousBuilders
    ///      (read from constructionData), NOT by the now-repurposed value. Swarm.
    ///   4. VillagerAutoSwapOccupationManager.IsBuildSiteValid -> request builders
    ///      up to the real max too, so priority never throttles builder count.
    ///   5. ConstructionService.ScoreByGroupAndDistanceFromQuerierTest -> add
    ///      priority * weight to each build site's work-search score, so higher
    ///      priority outranks distance (the score model is -distance + baseScore).
    ///
    /// Edge note: the hijacked field is the same one the game saves as builder
    /// count. Turning the feature OFF after setting priorities can leave inflated
    /// builder counts until readjusted — intended to be left on.
    /// </summary>
    internal static class BuildPriority
    {
        public const int PriorityMin = 1;
        public const int PriorityMax = 9;

        private static bool Enabled => FFUIOverhaulMod.EnableBuildPriority.Value;
        private static float Weight => FFUIOverhaulMod.BuildPriorityWeight.Value;

        private static FieldInfo? _cdField;     // BuildSiteResource.constructionData (protected)
        private static FieldInfo? _addlField;   // BuildSiteResource._additionalBuilders (protected)

        internal static int RealMax(BuildSiteResource bsr)
        {
            try
            {
                if (_cdField == null) _cdField = AccessTools.Field(typeof(BuildSiteResource), "constructionData");
                var cd = (ConstructionData)_cdField.GetValue(bsr);
                return cd.maxSimultaneousBuilders;
            }
            catch (System.Exception e)
            {
                if (!_loggedRealMaxFail)
                {
                    _loggedRealMaxFail = true;
                    FFUIOverhaulMod.Log.Warning("[BuildPriority] RealMax reflection failed (using fallback 3) — possible game version mismatch: " + e.Message);
                }
                return 3; // safe fallback to the common default
            }
        }
        private static bool _loggedRealMaxFail;

        private static int AdditionalBuilders(BuildSiteResource bsr)
        {
            try
            {
                if (_addlField == null) _addlField = AccessTools.Field(typeof(BuildSiteResource), "_additionalBuilders");
                return (int)_addlField.GetValue(bsr);
            }
            catch { return 0; }
        }

        // 1. maxBuilders getter -> 9 (drives setter clamp, increment bounds, "/9").
        [HarmonyPatch(typeof(BuildSiteResource), "get_maxBuilders")]
        static class MaxBuildersPatch
        {
            static void Postfix(ref int __result)
            {
                if (Enabled) __result = PriorityMax;
            }
        }

        // 2. Floor priority at 1 so a site never drops to 0 (= never built).
        [HarmonyPatch(typeof(BuildSiteResource), "set_userDefinedBuilders")]
        static class SetPriorityPatch
        {
            static void Prefix(ref int value)
            {
                if (!Enabled) return;
                if (value < PriorityMin) value = PriorityMin;
                if (value > PriorityMax) value = PriorityMax;
            }
        }

        // 3. Swarm: cap concurrent builders by the real engine max, not priority.
        [HarmonyPatch(typeof(BuildSiteResource), nameof(BuildSiteResource.CanAcceptAnotherWorker))]
        static class CanAcceptPatch
        {
            static bool Prefix(BuildSiteResource __instance, ISimpleReserver objLookingForWork,
                Func<ISimpleReserver, bool> canBeIncludedFunc, bool playerOnly, ref bool __result)
            {
                if (!Enabled)
                {
                    // Feature OFF: behave exactly like vanilla, with ONE safety net —
                    // a save made with the feature ON stored priority (1-9) in this
                    // field; if that leftover value exceeds the real builder max it
                    // would over-assign builders. So intervene ONLY when
                    // userDefinedBuilders > maxBuilders (here maxBuilders is the real,
                    // un-patched value because MaxBuildersPatch is gated on Enabled).
                    // Common case = one cheap compare, then vanilla runs (zero added cost).
                    if (__instance.userDefinedBuilders <= __instance.maxBuilders) return true;
                    __result = __instance.GetNumberOfBuilders(objLookingForWork, canBeIncludedFunc, playerOnly)
                        < __instance.maxBuilders + AdditionalBuilders(__instance);
                    return false;
                }
                int cap = RealMax(__instance) + AdditionalBuilders(__instance);
                __result = __instance.GetNumberOfBuilders(objLookingForWork, canBeIncludedFunc, playerOnly) < cap;
                return false; // skip vanilla
            }
        }

        // 4. Request builders up to the real max (so priority doesn't throttle count).
        // Intentionally overrides BOTH of vanilla IsBuildSiteValid's branches with
        // Min(reservers+1, realMax). The native "reservers>0" fallback branch used
        // exactly `reservers`; we add the +1 there too so the swarm can keep pulling
        // builders up to the real max in every valid case (consistent with the
        // feature's goal). We only touch sites that already returned a positive
        // count, so genuinely-idle/no-work sites are untouched.
        [HarmonyPatch(typeof(VillagerAutoSwapOccupationManager), "IsBuildSiteValid")]
        static class NeededBuildersPatch
        {
            static void Postfix(BuildSite buildSite, ref int neededBuilderCount, bool __result)
            {
                if (!Enabled || !__result) return;
                if (buildSite == null || buildSite.buildSiteResource == null) return;
                if (neededBuilderCount <= 0) return; // leave the "no work needed" path alone
                int reservers = buildSite.buildSiteResource.storage.GetNumberOfReservers();
                neededBuilderCount = Mathf.Min(reservers + 1, RealMax(buildSite.buildSiteResource));
            }
        }

        // 5. Priority -> work-search score (score model is -distance + baseScore).
        [HarmonyPatch(typeof(ConstructionService), "ScoreByGroupAndDistanceFromQuerierTest")]
        static class ScorePatch
        {
            static void Postfix(ref ScoreEntry nextToConsider, ScoreTestResult __result)
            {
                if (!Enabled || __result != ScoreTestResult.Scored) return;
                if (nextToConsider.queryEntry.workObj is BuildSiteResource bsr)
                    nextToConsider.score += bsr.userDefinedBuilders * Weight;
            }
        }
    }
}
