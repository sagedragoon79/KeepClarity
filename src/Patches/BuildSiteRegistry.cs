using System.Collections.Generic;
using HarmonyLib;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Live registry of active build sites, maintained via BuildSiteResource's
    /// Awake/OnDestroy. Lets the Build Queue overlay read the current build sites
    /// WITHOUT FindObjectsOfType (a full-scene scan that caused a per-refresh
    /// stutter in large settlements). Add/Remove are O(1) and fire only when a
    /// build site spawns or is destroyed — negligible cost.
    /// </summary>
    internal static class BuildSiteRegistry
    {
        private static readonly HashSet<BuildSiteResource> _sites = new HashSet<BuildSiteResource>();

        public static void Add(BuildSiteResource b) { if (b != null) _sites.Add(b); }
        public static void Remove(BuildSiteResource b) { if (b != null) _sites.Remove(b); }

        /// <summary>Fill a caller-owned buffer with live build sites (Unity-null
        /// filtered) — no per-call allocation.</summary>
        public static void SnapshotInto(List<BuildSiteResource> buffer)
        {
            buffer.Clear();
            foreach (var s in _sites)
                if (s != null) buffer.Add(s); // Unity '== null' catches destroyed
        }
    }

    [HarmonyPatch(typeof(BuildSiteResource), "Awake")]
    static class BuildSiteAwakePatch
    {
        static void Postfix(BuildSiteResource __instance) => BuildSiteRegistry.Add(__instance);
    }

    [HarmonyPatch(typeof(BuildSiteResource), "OnDestroy")]
    static class BuildSiteDestroyPatch
    {
        static void Prefix(BuildSiteResource __instance) => BuildSiteRegistry.Remove(__instance);
    }
}
