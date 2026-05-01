using System.Collections.Generic;
using FFUIOverhaul.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Adds a "Laborers / Builders" entry to the top bar, placed right after the
    /// villager count entry. Numbers turn red when below the game's recommended
    /// thresholds (read from ResourceManager.GetRecommendedLaborers/Builders).
    /// Cloned from the villager entry's GameObject so layout and styling match.
    /// </summary>
    [HarmonyPatch(typeof(UITopBar), "Start")]
    public class PatchUITopBarLaborerBuilderStart
    {
        public static void Postfix(UITopBar __instance)
        {
            if (FFUIOverhaulMod.UITopBarLaborerBuilderEntry != null) return;

            // Clone the BRICK entry (resource-style with icon child path
            // GetChild(0).GetChild(0)) — its layout matches what we want and
            // its icon swap is a known-good code path. Then move the clone
            // to sit next to the villager entry.
            var brickText = ReflectionHelper.GetPrivateField("brickValueText", __instance) as TextMeshProUGUI;
            var villagersText = ReflectionHelper.GetPrivateField("villagersValueText", __instance) as TextMeshProUGUI;
            if (brickText == null || villagersText == null) return;

            var brickEntry = brickText.transform.parent.gameObject;
            var villagerEntry = villagersText.transform.parent.gameObject;
            var villagerSiblings = villagerEntry.transform.parent;

            var clone = Object.Instantiate(brickEntry, villagerSiblings);
            clone.name = "FFUI_LaborerBuilderEntry";
            clone.transform.SetSiblingIndex(villagerEntry.transform.GetSiblingIndex() + 1);

            // Replace the icon (Heavy Tools sprite — work-related theme).
            var iconImage = clone.transform.GetChild(0).GetChild(0).GetComponent<Image>();
            Sprite? toolSprite = null;
            foreach (var probeId in new[] { "ItemHeavyTools", "ItemHeavyTool", "ItemTool" })
            {
                toolSprite = GlobalAssets.itemSetupData?.GetItemEntry(probeId)?.icon;
                if (toolSprite != null) break;
            }
            if (toolSprite != null && iconImage != null) iconImage.sprite = toolSprite;

            var valueText = clone.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (valueText != null) valueText.text = "0/0";

            var tooltip = clone.GetComponent<GenericTooltipDataProvider>();
            if (tooltip != null)
            {
                var rowKeys = ReflectionHelper.GetPrivateField("tooltipRowKeyLocalizationTags", tooltip) as List<string>;
                rowKeys?.Clear();
            }

            FFUIOverhaulMod.UITopBarLaborerBuilderEntry = clone;
        }
    }

    [HarmonyPatch(typeof(UITopBar), "UpdateResourceValues")]
    public class PatchUITopBarLaborerBuilderUpdate
    {
        public static void Postfix(UITopBar __instance)
        {
            var entry = FFUIOverhaulMod.UITopBarLaborerBuilderEntry;
            if (entry == null) return;

            var gm = UnitySingleton<GameManager>.Instance;
            var rm = gm?.resourceManager;
            if (rm == null) return;

            // Use the same accessor the Professions panel reads from — our
            // earlier villagersRO + GetOccupation iteration counted 0 in the
            // user's save even though the panel showed 27 laborers / 12 builders.
            int laborers = rm.GetProfessionAssignedCount(VillagerOccupation.Occupation.Laborer);
            int builders = rm.GetProfessionAssignedCount(VillagerOccupation.Occupation.Builder);

            // Builder count via Occupation is often 0 (builders are dynamically
            // promoted from laborers per build site). The "12" the user expects
            // is the desired-max from villagerAutoSwapOccupationManager.
            int desiredBuilders = 0;
            try { desiredBuilders = gm.villagerAutoSwapOccupationManager.maxBuildersDesired; }
            catch { }
            if (builders == 0 && desiredBuilders > 0) builders = desiredBuilders;

            int recLaborers = rm.GetRecommendedLaborers();
            int recBuilders = rm.GetRecommendedBuilders();
            bool laborersLow = laborers < recLaborers;
            bool buildersLow = builders < recBuilders;

            var valueText = entry.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (valueText != null)
            {
                string lTag = laborersLow ? "<color=#FF6B6B>" : "";
                string lEnd = laborersLow ? "</color>" : "";
                string bTag = buildersLow ? "<color=#FF6B6B>" : "";
                string bEnd = buildersLow ? "</color>" : "";
                valueText.text = $"{lTag}{laborers}{lEnd}/{bTag}{builders}{bEnd}";
            }

            var tooltip = entry.GetComponent<GenericTooltipDataProvider>();
            if (tooltip != null)
            {
                tooltip.toolTipRowKeyNames.Clear();
                tooltip.toolTipRowValues.Clear();
                tooltip.AddKeyValue("Laborers", $"{laborers}  (rec: {recLaborers})");
                tooltip.AddKeyValue("Builders", $"{builders}  (rec: {recBuilders})");
                tooltip.AddKeyValue("", "");
                tooltip.AddKeyValue("Red = below recommended", "");
            }
        }
    }
}
