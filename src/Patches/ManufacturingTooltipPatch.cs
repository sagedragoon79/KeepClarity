using System.Collections.Generic;
using HarmonyLib;
using FFUIOverhaul.Utils;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    [HarmonyPatch(typeof(UIBuildingInfoWindowSubWidgetManufacturingSliders), "PopulateManufacturingInfo")]
    public class PatchManufacturingPopulate
    {
        public static void Postfix(UIBuildingInfoWindowSubWidgetManufacturingSliders __instance)
        {
            AddWorkRequiredToTooltips(__instance);
        }

        internal static void AddWorkRequiredToTooltips(UIBuildingInfoWindowSubWidgetManufacturingSliders instance)
        {
            var building = ReflectionHelper.GetPrivateField("cachedBuilding", instance) as Building;
            var defs = building?.manufactureDefinitions;
            if (defs == null) return;

            var container = ((Component)instance).transform.GetChild(1).GetChild(3).GetChild(1);
            if (container.childCount <= 0) return;

            for (int i = 0; i < defs.Count && i < container.childCount; i++)
            {
                int workUnits = defs[i].workUnitsForProduction;
                var child = container.GetChild(i);
                var tooltip = child.gameObject.GetComponent<GenericTooltipDataProvider>();
                child.gameObject.SetActive(true);

                if (tooltip.toolTipRowKeyNames.Count > 1)
                    tooltip.toolTipRowKeyNames.RemoveAt(0);

                tooltip.toolTipRowKeyNames.Insert(0, $"Work Required: {workUnits}s");
            }
        }
    }

    [HarmonyPatch(typeof(UIBuildingInfoWindowSubWidgetManufacturingSliders), "UpdateWidgets")]
    public class PatchManufacturingUpdate
    {
        public static void Postfix(UIBuildingInfoWindowSubWidgetManufacturingSliders __instance)
        {
            PatchManufacturingPopulate.AddWorkRequiredToTooltips(__instance);
        }
    }
}
