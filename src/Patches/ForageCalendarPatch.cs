using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Appends a live "In season: Herbs, Greens, Berries" line to the top bar's
    /// clickable date/weather popup ("Early Summer - Year 5" / "Hot Temp, Light
    /// Breeze"). UITopBar.UpdateSeasonBarText fully reassigns the popup text on
    /// every invocation (day change / weather change / hover / init), so a
    /// postfix append is idempotent by construction — no guard needed. The line
    /// itself comes from ForageSeasonData, recomputed at most once per in-game day.
    /// </summary>
    internal static class ForageCalendarPatch
    {
        private const string Green = "#a8d060";

        private static bool _initialized;
        private static FieldInfo? _seasonBarTextField;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                var m = AccessTools.Method(typeof(UITopBar), "UpdateSeasonBarText");
                if (m == null) { FFUIOverhaulMod.Log.Warning("[ForageCalendar] UITopBar.UpdateSeasonBarText not found — readout disabled."); return; }
                _seasonBarTextField = AccessTools.Field(typeof(UITopBar), "seasonBarText");
                if (_seasonBarTextField == null) { FFUIOverhaulMod.Log.Warning("[ForageCalendar] UITopBar.seasonBarText not found — readout disabled."); return; }

                var h = new HarmonyLib.Harmony("FFUIOverhaul.ForageCalendar");
                h.Patch(m, postfix: new HarmonyMethod(typeof(ForageCalendarPatch), nameof(Postfix)));
                FFUIOverhaulMod.Log.Msg("[ForageCalendar] patched UITopBar.UpdateSeasonBarText.");
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[ForageCalendar] init failed: " + e.Message); }
        }

        private static void Postfix(UITopBar __instance)
        {
            try
            {
                if (!FFUIOverhaulMod.EnableInSeasonReadout.Value) return;
                if (_seasonBarTextField?.GetValue(__instance) is not List<Text> texts) return;

                string? line = UI.ForageSeasonData.GetInSeasonLine();
                if (line == null) return;

                string suffix = Environment.NewLine + "<color=" + Green + ">" + line + "</color>";
                foreach (var t in texts)
                    if (t != null) t.text += suffix;
            }
            catch { /* never break FF's top bar over a readout line */ }
        }
    }
}
