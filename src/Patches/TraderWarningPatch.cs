using System;
using System.Reflection;
using HarmonyLib;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// When a trader is N days from leaving (configurable), we re-raise the
    /// arrival event so FF builds a fresh notification card. The card's text
    /// is normally pulled from "Blurb_TraderArrived_Warning_*" loc tags — i.e.
    /// "Trader has arrived" — which is wrong for a departure warning.
    ///
    /// To clone the popup with new text we set a one-shot flag carrying the
    /// title/desc we want, then raise the arrival event as usual. The
    /// `UICriticalBlurb.Init` patch below picks the flag up, swaps in our
    /// strings via the existing titleOverride/descOverride parameters (which
    /// the game already supports — the arrival path uses them too), and
    /// clears the flag so the next real arrival isn't affected.
    /// </summary>
    [HarmonyPatch(typeof(TradeWagon), "OnDayPassed")]
    public class PatchTraderWarning
    {
        // Track which trader our staged override applies to, so genuine
        // arrival blurbs for OTHER wagons aren't accidentally relabeled.
        internal static TradeWagon? PendingWagon;
        internal static string? PendingTitle;
        internal static string? PendingDesc;

        public static void Postfix(TradeWagon __instance)
        {
            if (__instance.structureInsideOf == null) return;

            int warningDays = FFUIOverhaulMod.TraderWarningDays.Value;
            int daysLeft = __instance.GetDaysUntilDeparture();
            if (daysLeft != warningDays) return;

            // Stage the override strings + wagon. The GetLocalizedTitle /
            // GetLocalizedDescription patches below replace the displayed
            // text for THIS wagon's blurb container only, so genuine arrival
            // blurbs for other traders are unaffected.
            string dayWord = daysLeft == 1 ? "day" : "days";
            PendingWagon = __instance;
            PendingTitle = "Trader Departing";
            PendingDesc = $"{__instance.merchantName} leaves in {daysLeft} {dayWord}";

            var gm = UnitySingleton<GameManager>.Instance;
            // LeftEvent first to clear the prior arrival card; then a fresh
            // arrival event so the game rebuilds the card with our overrides.
            //
            // Leave silenceArrivedBlurbs = FALSE so InstantiateNewBlurb runs.
            // Setting it true suppressed not just the toast but also kept the
            // persistent UICriticalBlurb from registering with the blurb
            // manager (the registration sits inside the silenced branch).
            // The transient toast text is still wrong; we fix that separately
            // by patching whichever component renders the BlurbDefinition.
            gm.eventManager.Raise(new TradeWagonLeftEvent(__instance.merchantDefinition, __instance));
            gm.eventManager.Raise(new TradeWagonArrivedEvent(
                __instance.merchantName, __instance,
                __instance.merchantDefinition.blurbSprite, false));
        }
    }

    /// <summary>
    /// UICriticalBlurb pulls its displayed text dynamically from
    /// <c>blurbContainer.GetLocalizedTitle()</c> / GetLocalizedDescription()
    /// each refresh — not from the titleOverride/descOverride parameters
    /// passed to Init. So we patch the source instead: when the rendered
    /// container is a TradeWagonBlurbContainer wrapping our staged wagon,
    /// the staged strings replace the localized "Trader has arrived" text.
    ///
    /// Scoping by wagon means a real arrival of a DIFFERENT trader at the
    /// same time isn't mislabeled. The staged strings persist across all
    /// reads until a new warning overwrites them or the wagon leaves.
    /// </summary>
    [HarmonyPatch(typeof(BlurbContainer), "GetLocalizedTitle")]
    public class PatchBlurbGetLocalizedTitle
    {
        private static readonly FieldInfo? _wagonField = typeof(TradeWagonBlurbContainer)
            .GetField("singleTradeWagon", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(BlurbContainer __instance, ref string __result)
        {
            if (PatchTraderWarning.PendingTitle == null || PatchTraderWarning.PendingWagon == null) return;
            if (!(__instance is TradeWagonBlurbContainer twbc)) return;
            var wagon = _wagonField?.GetValue(twbc) as TradeWagon;
            if (wagon != PatchTraderWarning.PendingWagon) return;
            __result = PatchTraderWarning.PendingTitle;
        }
    }

    [HarmonyPatch(typeof(BlurbContainer), "GetLocalizedDescription")]
    public class PatchBlurbGetLocalizedDescription
    {
        private static readonly FieldInfo? _wagonField = typeof(TradeWagonBlurbContainer)
            .GetField("singleTradeWagon", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Postfix(BlurbContainer __instance, ref string __result)
        {
            if (PatchTraderWarning.PendingDesc == null || PatchTraderWarning.PendingWagon == null) return;
            if (!(__instance is TradeWagonBlurbContainer twbc)) return;
            var wagon = _wagonField?.GetValue(twbc) as TradeWagon;
            if (wagon != PatchTraderWarning.PendingWagon) return;
            __result = PatchTraderWarning.PendingDesc;
        }
    }
}
