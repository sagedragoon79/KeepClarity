using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Shows a villager's Essential Provisions work-rate bonuses (education from
    /// Learned Hands + Workplace Mastery) inline on the education line of the
    /// always-accessible selected-villager info window — so you don't have to dig
    /// into the conditional worker-slot picker to see them.
    ///
    /// Soft-dep: KC reads EP's public WorkInfoApi.GetVillagerWorkSummary(Villager)
    /// reflectively. No hard reference; renders nothing if EP isn't loaded.
    ///
    /// There are TWO villager window classes: the modern UIVillagerWindow_New
    /// (UISelectedObjectInfoWindow, parameterless UpdateText, private fields) — the
    /// one actually shown — and the legacy UIVillagerWindow (UpdateText takes a
    /// UIObjects with public fields). We patch UpdateText on BOTH; one postfix
    /// reads villager/educationValue from whichever shape it got, with per-type
    /// field caches so the two classes don't cross-wire.
    /// </summary>
    internal static class VillagerWorkInfoPatch
    {
        private static bool _initialized;
        private static MethodInfo? _epSummary;
        private static bool _epResolved;
        private static readonly Dictionary<Type, FieldInfo?> _villagerField = new Dictionary<Type, FieldInfo?>();
        private static readonly Dictionary<Type, FieldInfo?> _eduField = new Dictionary<Type, FieldInfo?>();
        private static bool _loggedError;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var h = new HarmonyLib.Harmony("FFUIOverhaul.VillagerWorkInfo");
                var post = new HarmonyMethod(typeof(VillagerWorkInfoPatch), nameof(Postfix));
                int n = 0;
                foreach (var typeName in new[] { "UIVillagerWindow_New", "UIVillagerWindow" })
                {
                    var t = AccessTools.TypeByName(typeName);
                    if (t == null) continue;
                    foreach (var m in AccessTools.GetDeclaredMethods(t))
                    {
                        if (m.Name != "UpdateText") continue;
                        try { h.Patch(m, postfix: post); n++; }
                        catch (Exception inner) { FFUIOverhaulMod.Log.Warning($"[VillagerWorkInfo] patch {typeName}.{m} failed: {inner.Message}"); }
                    }
                }
                FFUIOverhaulMod.Log.Msg($"[VillagerWorkInfo] patched {n} UpdateText method(s) across villager window classes.");
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[VillagerWorkInfo] init failed: " + e.Message); }
        }

        private static void Postfix(object __instance, object[] __args)
        {
            try
            {
                var it = __instance.GetType();
                if (!_villagerField.TryGetValue(it, out var vf)) { vf = AccessTools.Field(it, "villager"); _villagerField[it] = vf; }
                var villager = vf?.GetValue(__instance) as Villager;
                if (villager == null) return;

                string summary = GetSummary(villager);
                if (string.IsNullOrEmpty(summary)) return;

                // educationValue: on the UIObjects arg (legacy overload) or the
                // window itself (modern parameterless overload).
                TMP_Text? edu = null;
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    var uiObjs = __args[0];
                    var ut = uiObjs.GetType();
                    if (!_eduField.TryGetValue(ut, out var f)) { f = AccessTools.Field(ut, "educationValue"); _eduField[ut] = f; }
                    edu = f?.GetValue(uiObjs) as TMP_Text;
                }
                else
                {
                    if (!_eduField.TryGetValue(it, out var f)) { f = AccessTools.Field(it, "educationValue"); _eduField[it] = f; }
                    edu = f?.GetValue(__instance) as TMP_Text;
                }
                if (edu == null) return;

                edu.text = edu.text + "  <color=#7fbf7f>(" + summary + ")</color>";
            }
            catch (Exception e)
            {
                if (!_loggedError) { _loggedError = true; FFUIOverhaulMod.Log.Warning("[VillagerWorkInfo] postfix: " + e.Message); }
            }
        }

        private static string GetSummary(Villager v)
        {
            if (!_epResolved)
            {
                _epResolved = true;
                var t = FindType("EssentialProvisions.WorkInfoApi");
                _epSummary = t?.GetMethod("GetVillagerWorkSummary", BindingFlags.Public | BindingFlags.Static);
                if (_epSummary == null) FFUIOverhaulMod.Log.Msg("[VillagerWorkInfo] EP WorkInfoApi not found — line disabled (EP not installed?).");
            }
            if (_epSummary == null) return "";
            try { return _epSummary.Invoke(null, new object[] { v }) as string ?? ""; }
            catch { return ""; }
        }

        private static Type? FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t;
                try { t = a.GetType(fullName); } catch { continue; }
                if (t != null) return t;
            }
            return null;
        }
    }
}
