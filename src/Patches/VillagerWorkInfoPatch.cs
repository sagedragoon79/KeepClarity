using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Shows a villager's Essential Provisions work-rate bonuses on the
    /// always-accessible selected-villager info window:
    ///   - Workplace Mastery → appended to the OCCUPATION line ("+X% Mastery
    ///     Bonus"), right next to the job title where it reads clearly.
    ///   - Learned Hands education → appended to the EDUCATION line ("+X% Output
    ///     Bonus"); that line already names the source (education), so the suffix
    ///     names the effect.
    ///
    /// Soft-dep: KC reads EP's public WorkInfoApi reflectively (no hard reference).
    /// Prefers the split numeric getters (GetEducationBonusPercent /
    /// GetMasteryBonusPercent); falls back to the legacy combined
    /// GetVillagerWorkSummary string (on the education line) for older EP builds.
    /// Renders nothing if EP isn't loaded.
    ///
    /// There are TWO villager window classes: the modern UIVillagerWindow_New
    /// (parameterless UpdateText, private fields) — the one actually shown — and
    /// the legacy UIVillagerWindow (UpdateText takes a UIObjects with public
    /// fields). We patch UpdateText on BOTH; one postfix reads villager +
    /// professionValue + educationValue from whichever shape it got, with per-type
    /// field caches so the two classes don't cross-wire.
    /// </summary>
    internal static class VillagerWorkInfoPatch
    {
        private const string Green = "#7fbf7f";

        private static bool _initialized;
        private static bool _loggedError;

        // EP reflective binding.
        private static bool _epResolved;
        private static MethodInfo? _epEdu;       // GetEducationBonusPercent(Villager) -> float
        private static MethodInfo? _epMastery;   // GetMasteryBonusPercent(Villager) -> float
        private static MethodInfo? _epSummary;   // GetVillagerWorkSummary(Villager) -> string  (legacy fallback)

        private static readonly Dictionary<Type, FieldInfo?> _villagerField = new Dictionary<Type, FieldInfo?>();
        private static readonly Dictionary<Type, FieldInfo?> _eduField = new Dictionary<Type, FieldInfo?>();
        private static readonly Dictionary<Type, FieldInfo?> _profField = new Dictionary<Type, FieldInfo?>();

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
                var villager = GetField(it, __instance, "villager", _villagerField) as Villager;
                if (villager == null) return;

                EnsureEpResolved();

                // The TMP fields live on the UIObjects arg (legacy overload) or the
                // window itself (modern parameterless overload).
                object host; Type hostType;
                if (__args != null && __args.Length > 0 && __args[0] != null) { host = __args[0]; hostType = host.GetType(); }
                else { host = __instance; hostType = it; }

                var edu  = GetField(hostType, host, "educationValue", _eduField) as TMP_Text;
                var prof = GetField(hostType, host, "professionValue", _profField) as TMP_Text;

                if (_epEdu != null && _epMastery != null)
                {
                    // Split API: mastery → job line, education → education line.
                    if (prof != null)
                    {
                        float m = InvokeFloat(_epMastery, villager);
                        if (m > 0f) prof.text = Append(prof.text, m, "Mastery Bonus");
                    }
                    if (edu != null)
                    {
                        float e = InvokeFloat(_epEdu, villager);
                        if (e > 0f) edu.text = Append(edu.text, e, "Output Bonus");
                    }
                }
                else if (_epSummary != null && edu != null)
                {
                    // Legacy EP: keep the combined summary on the education line.
                    string summary = InvokeString(_epSummary, villager);
                    if (!string.IsNullOrEmpty(summary))
                        edu.text = edu.text + "  <color=" + Green + ">(" + summary + ")</color>";
                }
            }
            catch (Exception e)
            {
                if (!_loggedError) { _loggedError = true; FFUIOverhaulMod.Log.Warning("[VillagerWorkInfo] postfix: " + e.Message); }
            }
        }

        private static string Append(string baseText, float pct, string label)
            => baseText + "  <color=" + Green + ">(+" + pct.ToString("0.#") + "% " + label + ")</color>";

        private static void EnsureEpResolved()
        {
            if (_epResolved) return;
            _epResolved = true;
            var t = FindType("EssentialProvisions.WorkInfoApi");
            if (t == null) { FFUIOverhaulMod.Log.Msg("[VillagerWorkInfo] EP WorkInfoApi not found — line disabled (EP not installed?)."); return; }

            const BindingFlags F = BindingFlags.Public | BindingFlags.Static;
            var oneVillager = new[] { typeof(Villager) };
            _epEdu     = t.GetMethod("GetEducationBonusPercent", F, null, oneVillager, null);
            _epMastery = t.GetMethod("GetMasteryBonusPercent", F, null, oneVillager, null);
            _epSummary = t.GetMethod("GetVillagerWorkSummary", F, null, oneVillager, null);

            if (_epEdu != null && _epMastery != null)
                FFUIOverhaulMod.Log.Msg("[VillagerWorkInfo] EP split API bound (education + mastery).");
            else if (_epSummary != null)
                FFUIOverhaulMod.Log.Msg("[VillagerWorkInfo] EP split API absent — using legacy combined summary.");
            else
                FFUIOverhaulMod.Log.Msg("[VillagerWorkInfo] EP WorkInfoApi found but exposes no usable methods.");
        }

        private static float InvokeFloat(MethodInfo m, Villager v)
        {
            try { var r = m.Invoke(null, new object[] { v }); return r is float f ? f : 0f; }
            catch { return 0f; }
        }

        private static string InvokeString(MethodInfo m, Villager v)
        {
            try { return m.Invoke(null, new object[] { v }) as string ?? ""; }
            catch { return ""; }
        }

        private static object? GetField(Type hostType, object host, string name, Dictionary<Type, FieldInfo?> cache)
        {
            if (!cache.TryGetValue(hostType, out var f)) { f = AccessTools.Field(hostType, name); cache[hostType] = f; }
            return f?.GetValue(host);
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
