using HarmonyLib;

namespace FFUIOverhaul.Patches
{
    [HarmonyPatch(typeof(TradeWagon), "OnDayPassed")]
    public class PatchTraderWarning
    {
        public static void Postfix(TradeWagon __instance)
        {
            if (__instance.structureInsideOf == null) return;

            int warningDays = FFUIOverhaulMod.TraderWarningDays.Value;
            if (__instance.GetDaysUntilDeparture() != warningDays) return;

            var gm = UnitySingleton<GameManager>.Instance;
            gm.eventManager.Raise(new TradeWagonLeftEvent(__instance.merchantDefinition, __instance));
            gm.eventManager.Raise(new TradeWagonArrivedEvent(
                __instance.merchantName, __instance,
                __instance.merchantDefinition.blurbSprite, false));

            FFUIOverhaulMod.Log.Msg($"Trader '{__instance.merchantName}' departing in {warningDays} days");
        }
    }
}
