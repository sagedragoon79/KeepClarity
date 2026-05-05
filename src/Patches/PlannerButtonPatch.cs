using System.Collections.Generic;
using FFUIOverhaul.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Adds a "PLAN" button to the right side of the top bar that opens
    /// SageDragoon's Farthest Frontier Planner in the player's browser.
    /// Clones the brick entry for visual consistency, swaps the value text to
    /// the label, and wires Application.OpenURL on the icon's Button.
    /// </summary>
    [HarmonyPatch(typeof(UITopBar), "Start")]
    public class PatchUITopBarPlannerButton
    {
        private const string PlannerUrl = "https://sagedragoon79.github.io/FarthestFrontierPlanner/";

        public static void Postfix(UITopBar __instance)
        {
            // Optional opt-out: some players already know the planner URL and
            // would rather reclaim the top-bar space. Restart-required because
            // re-adding mid-session would mean re-running the brick-clone
            // insertion at a different sibling index than the original.
            if (FFUIOverhaulMod.ShowPlannerButton != null && !FFUIOverhaulMod.ShowPlannerButton.Value) return;
            if (FFUIOverhaulMod.UITopBarPlannerButton != null) return;

            // Clone brick entry for the visual structure (icon + text + tooltip).
            var brickText = ReflectionHelper.GetPrivateField("brickValueText", __instance) as TextMeshProUGUI;
            if (brickText == null) return;
            var brickEntry = brickText.transform.parent.gameObject;

            // ...but parent it to the LEFT-side container, right after the Food
            // entry. That way it sits between Food and the calendar/weather
            // widget instead of out on the right with the resource icons.
            var foodText = ReflectionHelper.GetPrivateField("foodValueText", __instance) as TextMeshProUGUI;
            if (foodText == null) return;
            var foodEntry = foodText.transform.parent.gameObject;
            var leftParent = foodEntry.transform.parent;

            var entry = Object.Instantiate(brickEntry, leftParent);
            entry.name = "FFUI_PlannerButton";
            entry.transform.SetSiblingIndex(foodEntry.transform.GetSiblingIndex() + 1);

            // Use ItemPaper icon (looks document-y) — falls through if missing.
            Sprite? sprite = null;
            foreach (var probe in new[] { "ItemPaper", "ItemBlueprint", "ItemBook", "ItemBrick" })
            {
                sprite = GlobalAssets.itemSetupData?.GetItemEntry(probe)?.icon;
                if (sprite != null) break;
            }
            var iconImage = entry.transform.GetChild(0).GetChild(0).GetComponent<Image>();
            if (iconImage != null && sprite != null) iconImage.sprite = sprite;

            // Repurpose the value text as the label.
            var valueText = entry.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (valueText != null)
            {
                valueText.text = "PLAN";
                valueText.fontStyle = FontStyles.Bold;
            }

            // Put the Button on the ENTRY ROOT (not on the icon child) so it
            // doesn't sit between the pointer and the GenericTooltipDataProvider
            // on the same root — otherwise hover events get absorbed and the
            // tooltip never fires. Clicks on the icon still bubble up here.
            if (iconImage != null) iconImage.raycastTarget = true;
            var btn = entry.GetComponent<Button>() ?? entry.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            if (iconImage != null) btn.targetGraphic = iconImage;
            btn.onClick.AddListener(OnPlannerClicked);

            FFUIOverhaulMod.UITopBarPlannerButton = entry;
            RefreshTooltip(entry);
        }

        /// <summary>
        /// Set the tooltip rows. Called from this patch's Postfix and re-called
        /// every frame from the UpdateResourceValues postfix below — the game's
        /// UI refresh otherwise resets toolTipRowKeyNames to whatever
        /// tooltipRowKeyLocalizationTags resolves to, which is empty here.
        /// </summary>
        public static void RefreshTooltip(GameObject entry)
        {
            var tooltip = entry.GetComponent<GenericTooltipDataProvider>();
            if (tooltip == null) return;

            // Only rebuild if rows aren't already what we set — avoids per-frame churn.
            if (tooltip.toolTipRowKeyNames.Count == 4
                && tooltip.toolTipRowKeyNames[0] == "Farthest Frontier Planner")
                return;

            var rowKeys = ReflectionHelper.GetPrivateField("tooltipRowKeyLocalizationTags", tooltip) as List<string>;
            rowKeys?.Clear();
            tooltip.toolTipRowKeyNames.Clear();
            tooltip.toolTipRowValues.Clear();
            tooltip.AddKeyValue("Farthest Frontier Planner", "");
            tooltip.AddKeyValue("", "");
            tooltip.AddKeyValue("Click to open in browser", "");
            tooltip.AddKeyValue("by SageDragoon", "");
        }

        private static void OnPlannerClicked()
        {
            Application.OpenURL(PlannerUrl);
        }
    }

    /// <summary>
    /// Re-applies the planner button's tooltip rows after each UI refresh.
    /// </summary>
    [HarmonyPatch(typeof(UITopBar), "UpdateResourceValues")]
    public class PatchUITopBarPlannerButtonUpdate
    {
        public static void Postfix(UITopBar __instance)
        {
            var entry = FFUIOverhaulMod.UITopBarPlannerButton;
            if (entry == null) return;
            PatchUITopBarPlannerButton.RefreshTooltip(entry);
        }
    }
}
