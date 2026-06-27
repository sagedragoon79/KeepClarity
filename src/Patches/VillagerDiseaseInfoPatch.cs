using System;
using System.Reflection;
using HarmonyLib;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Appends a sick villager's cure odds to the disease hover tooltip in the
    /// villager info window. Read reflectively from Essential Provisions'
    /// DiseaseInfoApi (soft-dep, same pattern as WorkInfoApi) — EP owns the
    /// numbers, KC owns the wording/placement.
    ///
    /// FF's UIVillagerDiseaseInfo shows each active disease as an icon whose
    /// GenericTooltipDataProvider lists the disease name + description, rebuilt on
    /// every hover via the private UpdateDiseaseInfoTooltip. We postfix that and add
    /// the cure picture, one of four states: the live "18% to recover (5% per check)"
    /// when under care; "Treatment: Death" + "#GoneTooSoon" for a genuinely incurable
    /// one (e.g. vanilla rabies); "Resolves on its own" for a self-resolving ailment;
    /// or, using EP's cure-path getters, a two-row "Treatment: &lt;action&gt;" +
    /// "Recovery: &lt;best&gt;% If Treated" for a gated/untreated disease. Renders nothing
    /// if EP (≥1.4.0) isn't loaded; degrades to odds-only if the cure-path getters are absent.
    /// </summary>
    internal static class VillagerDiseaseInfoPatch
    {
        private const string Amber = "#d8a93a"; // recovery numbers
        private const string Teal  = "#7fbfd8"; // actionable "Treatment:" hint
        private const string Red   = "#c08080"; // grim / no cure
        private const string Green = "#7fbf7f"; // resolves on its own
        private const string Gray  = "#8f8a7a"; // wry aside

        private static bool _initialized;
        private static bool _loggedError;

        // EP reflective binding (EssentialProvisions.DiseaseInfoApi, all (Villager)).
        private static bool _epResolved;
        private static MethodInfo? _epNames;      // GetActiveDiseaseNames     -> string[]
        private static MethodInfo? _epOverall;    // GetOverallRecoveryPercents -> float[]
        private static MethodInfo? _epPerCheck;    // GetCureChancePercents       -> float[]
        private static MethodInfo? _epCureMethods; // GetCureMethods              -> string[]  (EP 1.4.0+, optional)
        private static MethodInfo? _epBestCase;    // GetBestCaseRecoveryPercents -> float[]   (EP 1.4.0+, optional)

        // UIVillagerDiseaseInfo private fields + Disease.GetLocalizedDisplayName.
        private static bool _fieldsResolved;
        private static FieldInfo? _diseaseField;
        private static FieldInfo? _providerField;
        private static MethodInfo? _getDiseaseName;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var t = AccessTools.TypeByName("UIVillagerDiseaseInfo");
                if (t == null) { FFUIOverhaulMod.Log.Msg("[VillagerDiseaseInfo] UIVillagerDiseaseInfo not found — disabled."); return; }
                var m = AccessTools.Method(t, "UpdateDiseaseInfoTooltip");
                if (m == null) { FFUIOverhaulMod.Log.Warning("[VillagerDiseaseInfo] UpdateDiseaseInfoTooltip not found — disabled."); return; }

                var h = new HarmonyLib.Harmony("FFUIOverhaul.VillagerDiseaseInfo");
                h.Patch(m, postfix: new HarmonyMethod(typeof(VillagerDiseaseInfoPatch), nameof(Postfix)));
                FFUIOverhaulMod.Log.Msg("[VillagerDiseaseInfo] patched UIVillagerDiseaseInfo.UpdateDiseaseInfoTooltip.");
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[VillagerDiseaseInfo] init failed: " + e.Message); }
        }

        private static void Postfix(object __instance)
        {
            try
            {
                EnsureFieldsResolved(__instance.GetType());
                if (_diseaseField?.GetValue(__instance) is not DiseaseComponent dc || dc.diseaseDef == null) return;
                if (_providerField?.GetValue(__instance) is not GenericTooltipDataProvider provider) return;

                // DiseaseComponent shares the villager's GameObject (it grabs its
                // ImmuneSystemComponent via GetComponent), so the villager is local.
                var villager = dc.GetComponent<Villager>();
                if (villager == null) return;

                EnsureEpResolved();
                if (_epNames == null || _epOverall == null || _epPerCheck == null) return;

                if (_epNames.Invoke(null, new object[] { villager }) is not string[] names || names.Length == 0) return;
                if (_epOverall.Invoke(null, new object[] { villager }) is not float[] overall) return;
                if (_epPerCheck.Invoke(null, new object[] { villager }) is not float[] perCheck) return;

                // Match this icon's disease to EP's parallel arrays by localized name.
                string thisName = GetLocalizedName(dc);
                int idx = string.IsNullOrEmpty(thisName) ? -1 : Array.IndexOf(names, thisName);
                // Single active disease → it must be this one; don't let an EP-vs-FF
                // localized-name mismatch drop the odds in the common case.
                if (idx < 0 && names.Length == 1) idx = 0;
                if (idx < 0 || idx >= overall.Length || idx >= perCheck.Length) return;

                float o = overall[idx], p = perCheck[idx];

                // Optional cure-path getters (EP 1.4.0+). Without them we degrade to
                // odds-only: show the live recovery line, stay silent on 0.
                string method = "";
                if (_epCureMethods?.Invoke(null, new object[] { villager }) is string[] cm && idx < cm.Length) method = cm[idx] ?? "";
                float best = -1f;
                if (_epBestCase?.Invoke(null, new object[] { villager }) is float[] bc && idx < bc.Length) best = bc[idx];

                // Intrinsic cure paths take priority over the (possibly high) live number.
                if (method == "Incurable") { AddRow(provider, Red, "Treatment: Death"); AddRow(provider, Gray, "#GoneTooSoon"); return; }
                if (method == "SelfResolving") { AddRow(provider, Green, "Resolves on its own"); return; }

                // Actively recovering under current care — show the live odds, but only
                // when they actually round to >=1%. A healer-curable disease left UNTREATED
                // sits a hair above 0 (base cure score 1), which passes a ">0" test yet
                // renders the useless "0% to recover (0% per check)". Round first, and if it
                // would show 0% fall through to the treatment nudge instead.
                int oPct = (int)Math.Round(o, MidpointRounding.AwayFromZero);
                int pPct = (int)Math.Round(p, MidpointRounding.AwayFromZero);
                if (oPct >= 1 || pPct >= 1)
                {
                    AddRow(provider, Amber, oPct + "% to recover (" + pPct + "% per check)");
                    return;
                }

                // 0 current odds: if EP gave a cure path, turn it into guidance;
                // otherwise stay silent (never over-claim "no cure" — that's the scurvy trap).
                // "Age" is a non-actionable gate (the player can't treat aging), so we stay
                // silent there too rather than nudge a treatment that doesn't exist.
                if (method.Length == 0 || method == "Age") return;
                // Every gated/healer disease warrants treatment. Untreated, even the
                // "non-lethal" ones bedridden the villager for the disease's full duration
                // AND can progress to a deadly follow-on (broken bone → infection), so we
                // always show the treatment nudge here — no "Minor" de-emphasis.
                AddRow(provider, Teal, "Treatment: " + ActionFor(method));
                if (best >= 0f) AddRow(provider, Amber, "Recovery: " + best.ToString("0") + "% If Treated");
            }
            catch (Exception e)
            {
                if (!_loggedError) { _loggedError = true; FFUIOverhaulMod.Log.Warning("[VillagerDiseaseInfo] postfix: " + e.Message); }
            }
        }

        private static void AddRow(GenericTooltipDataProvider provider, string color, string text)
            => provider.toolTipRowKeyNames.Add("<color=" + color + ">" + text + "</color>");

        // KC owns the action wording for EP's cure-path tokens; unknown/raw "DF_*"
        // tokens fall through to a generic prompt.
        private static string ActionFor(string method) => method switch
        {
            "Diet"     => "Eat Fruit & Vegetables",
            "Water"    => "Provide Clean Water",
            "Thirst"   => "Provide Access To Water",
            "Warmth"   => "Heat Their Shelter",
            "Soap"     => "Provide Soap",         // residences must stock soap (hygiene gate)
            "Healer"   => "Healer's House",      // curable at a basic (T1) Healer's House
            "Hospital" => "Hospital + Medicine", // needs the T2 Hospital + stocked Medicine
            _          => "Needs Treatment",
        };

        private static string GetLocalizedName(DiseaseComponent dc)
        {
            try
            {
                if (_getDiseaseName == null) _getDiseaseName = AccessTools.Method(dc.diseaseDef.GetType(), "GetLocalizedDisplayName");
                var n = _getDiseaseName?.Invoke(dc.diseaseDef, null) as string;
                // Mirror EP's DisplayName(): fall back to the def name if localization is
                // empty, so multi-disease name-matching stays aligned with EP's array.
                return string.IsNullOrEmpty(n) ? (dc.diseaseDef.name ?? "") : n;
            }
            catch { return ""; }
        }

        private static void EnsureFieldsResolved(Type t)
        {
            if (_fieldsResolved) return;
            _fieldsResolved = true;
            _diseaseField = AccessTools.Field(t, "diseaseComponent");
            _providerField = AccessTools.Field(t, "villagerDiseaseTooltipDataProvider");
        }

        private static void EnsureEpResolved()
        {
            if (_epResolved) return;
            _epResolved = true;
            var t = FindType("EssentialProvisions.DiseaseInfoApi");
            if (t == null) { FFUIOverhaulMod.Log.Msg("[VillagerDiseaseInfo] EP DiseaseInfoApi not found — cure odds disabled (EP <1.4.0?)."); return; }

            const BindingFlags F = BindingFlags.Public | BindingFlags.Static;
            var one = new[] { typeof(Villager) };
            _epNames = t.GetMethod("GetActiveDiseaseNames", F, null, one, null);
            _epOverall = t.GetMethod("GetOverallRecoveryPercents", F, null, one, null);
            _epPerCheck = t.GetMethod("GetCureChancePercents", F, null, one, null);
            _epCureMethods = t.GetMethod("GetCureMethods", F, null, one, null);            // EP 1.4.0+
            _epBestCase = t.GetMethod("GetBestCaseRecoveryPercents", F, null, one, null);  // EP 1.4.0+

            if (_epNames != null && _epOverall != null && _epPerCheck != null)
                FFUIOverhaulMod.Log.Msg("[VillagerDiseaseInfo] EP DiseaseInfoApi bound" + (_epCureMethods != null && _epBestCase != null ? " (with cure-path)." : " (odds only)."));
            else
                FFUIOverhaulMod.Log.Msg("[VillagerDiseaseInfo] EP DiseaseInfoApi incomplete — cure odds disabled.");
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
