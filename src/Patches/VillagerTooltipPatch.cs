using HarmonyLib;
using FFUIOverhaul.Utils;

namespace FFUIOverhaul.Patches
{
    [HarmonyPatch(typeof(UITopBar), "UpdateVillagerTooltip")]
    public class PatchVillagerTooltip
    {
        public static void Postfix(UITopBar __instance)
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return;

            var rm = gm.resourceManager;
            int totalPop = rm.villagersRO.Count;
            if (totalPop == 0) return;

            var locMgr = gm.localizationManager;
            var provider = ReflectionHelper.GetPrivateField("villagerToolTipDataProvider", __instance) as GenericTooltipDataProvider;
            var provider2 = ReflectionHelper.GetPrivateField("villagerToolTipDataProvider2", __instance) as GenericTooltipDataProvider;
            if (provider == null) return;

            // Housing count: add surplus percentage
            EnhanceHousingCount(provider, provider2, locMgr, totalPop);

            // Educated: show absolute count alongside percentage
            EnhanceEducated(provider, provider2, locMgr, totalPop);

            // Fit to work: show percentage
            EnhanceFitToWork(provider, provider2, locMgr, totalPop);

            // Add livestock counts
            AddLivestockInfo(provider, provider2, rm);

            // Add laborer count
            AddLaborerInfo(provider, provider2, rm, totalPop);
        }

        private static void EnhanceHousingCount(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            LocalizationManager loc, int totalPop)
        {
            string key = loc.Localize("ResourcesHUD_HousingCount");
            int idx = p1.toolTipRowKeyNames.IndexOf(key);
            if (idx == -1) return;

            string val = p1.toolTipRowValues[idx];
            if (int.TryParse(val, out int housingCount))
            {
                int surplus = housingCount - totalPop;
                int pct = totalPop > 0 ? (100 * surplus / totalPop) : 0;
                string sign = pct >= 0 ? "+" : "";
                string enhanced = $"{val} ({sign}{pct}%)";
                p1.toolTipRowValues[idx] = enhanced;
                if (p2 != null && p2.toolTipRowValues.Count > idx)
                    p2.toolTipRowValues[idx] = enhanced;
            }
        }

        private static void EnhanceEducated(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            LocalizationManager loc, int totalPop)
        {
            string key = loc.Localize("ResourcesHUD_PctEducated");
            int idx = p1.toolTipRowKeyNames.IndexOf(key);
            if (idx == -1) return;

            string pctStr = p1.toolTipRowValues[idx].TrimEnd('%');
            if (int.TryParse(pctStr, out int pct))
            {
                int count = (int)((double)(pct * totalPop / 100) + 0.5);
                string enhanced = $"{count} ({pct}%)";
                p1.toolTipRowValues[idx] = enhanced;
                if (p2 != null && p2.toolTipRowValues.Count > idx)
                    p2.toolTipRowValues[idx] = enhanced;
            }
        }

        private static void EnhanceFitToWork(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            LocalizationManager loc, int totalPop)
        {
            string key = loc.Localize("ResourcesHUD_FitToWorkCount");
            int idx = p1.toolTipRowKeyNames.IndexOf(key);
            if (idx == -1) return;

            string val = p1.toolTipRowValues[idx];
            if (int.TryParse(val, out int fitCount))
            {
                int pct = totalPop > 0 ? (100 * fitCount / totalPop) : 0;
                string enhanced = $"{val} ({pct}%)";
                p1.toolTipRowValues[idx] = enhanced;
                if (p2 != null && p2.toolTipRowValues.Count > idx)
                    p2.toolTipRowValues[idx] = enhanced;
            }
        }

        private static void AddLivestockInfo(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            ResourceManager rm)
        {
            // Only add once (check if already present)
            if (p1.toolTipRowKeyNames.Contains("Cattle")) return;

            p1.AddKeyValue("--- Livestock ---", "");
            p2?.AddKeyValue("--- Livestock ---", "");

            AddLivestockRow(p1, p2, rm, "Cattle", "ItemCattle");
            AddLivestockRow(p1, p2, rm, "Goats", "ItemGoat");
            AddLivestockRow(p1, p2, rm, "Chickens", "ItemChicken");
        }

        private static void AddLivestockRow(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            ResourceManager rm, string label, string itemId)
        {
            var info = rm.GetItemInfo(itemId);
            string count = info != null ? info.unusedCount.ToString() : "0";
            p1.AddKeyValue(label, count);
            p2?.AddKeyValue(label, count);
        }

        private static void AddLaborerInfo(GenericTooltipDataProvider p1, GenericTooltipDataProvider? p2,
            ResourceManager rm, int totalPop)
        {
            if (p1.toolTipRowKeyNames.Contains("Free Laborers")) return;

            // These counts may need to be fetched via reflection if not public
            try
            {
                var employed = ReflectionHelper.GetPrivateField("employedCount", rm);
                var fitToWork = ReflectionHelper.GetPrivateField("fitToWorkCount", rm);
                if (employed != null && fitToWork != null)
                {
                    int emp = (int)employed;
                    int fit = (int)fitToWork;
                    int laborers = fit - emp;

                    p1.AddKeyValue("--- Workforce ---", "");
                    p1.AddKeyValue("Employed", $"{emp} / {fit}");
                    p1.AddKeyValue("Free Laborers", laborers.ToString());

                    p2?.AddKeyValue("--- Workforce ---", "");
                    p2?.AddKeyValue("Employed", $"{emp} / {fit}");
                    p2?.AddKeyValue("Free Laborers", laborers.ToString());
                }
            }
            catch { }
        }
    }
}
