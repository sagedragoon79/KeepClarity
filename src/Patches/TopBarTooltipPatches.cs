using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using FFUIOverhaul.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    [HarmonyPatch(typeof(UITopBar), "Start")]
    public class PatchUITopBarStart
    {
        public static void Postfix(UITopBar __instance)
        {
            // Order matters: each call appends just before the parent's last
            // child, so this is the left-to-right display order. After brick:
            //   Sand → Glass → Coal → Iron
            FFUIOverhaulMod.UITopBarSandEntry  ??= AddItemEntry(__instance, "ItemSand",  "Sand Resource Entry");
            FFUIOverhaulMod.UITopBarGlassEntry ??= AddItemEntry(__instance, "ItemGlass", "Glass Resource Entry");
            FFUIOverhaulMod.UITopBarCoalEntry  ??= AddItemEntry(__instance, "ItemCoal",  "Coal Resource Entry");
            FFUIOverhaulMod.UITopBarIronEntry  ??= AddItemEntry(__instance, "ItemIron",  "Iron Resource Entry");
        }

        /// <summary>
        /// Clone the brick entry (known-good icon path), swap its icon to the
        /// given itemId's sprite, and insert the clone just before the parent's
        /// last child (so it ends up right after the previous resource entry).
        /// </summary>
        private static GameObject? AddItemEntry(UITopBar __instance, string itemId, string entryName)
        {
            var brickText = (TextMeshProUGUI)ReflectionHelper.GetPrivateField("brickValueText", __instance);
            if (brickText == null) return null;

            var brickEntry = ((TMP_Text)brickText).transform.parent.gameObject;
            var parent = brickEntry.transform.parent;

            var lastChild = parent.GetChild(parent.childCount - 1);
            lastChild.parent = null;

            var entry = UnityEngine.Object.Instantiate(brickEntry);
            entry.name = entryName;
            entry.transform.parent = parent;

            lastChild.parent = parent;

            var sprite = GlobalAssets.itemSetupData?.GetItemEntry(itemId)?.icon;
            if (sprite != null)
            {
                var iconImage = entry.transform.GetChild(0).GetChild(0).GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = sprite;
            }

            var valueText = entry.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (valueText != null) ((TMP_Text)valueText).text = "0";

            var tooltipProvider = entry.GetComponent<GenericTooltipDataProvider>();
            var rowKeys = (List<string>)ReflectionHelper.GetPrivateField("tooltipRowKeyLocalizationTags", tooltipProvider);
            rowKeys?.Clear();

            return entry;
        }

        // Resource tooltip enhancement happens in UpdateResourceValues postfix.
    }

    [HarmonyPatch(typeof(UITopBar), "UpdateResourceValues")]
    public class PatchUITopBarUpdateValues
    {
        public static void Postfix(UITopBar __instance)
        {
            UpdateIronValue();
            EnhanceTooltipValues(__instance);
        }

        private static void UpdateIronValue()
        {
            // Update value text on every custom entry we added.
            UpdateCustomEntryValue(FFUIOverhaulMod.UITopBarIronEntry,  "ItemIron");
            UpdateCustomEntryValue(FFUIOverhaulMod.UITopBarSandEntry,  "ItemSand");
            UpdateCustomEntryValue(FFUIOverhaulMod.UITopBarGlassEntry, "ItemGlass");
            UpdateCustomEntryValue(FFUIOverhaulMod.UITopBarCoalEntry,  "ItemCoal");
        }

        private static void UpdateCustomEntryValue(GameObject? entry, string itemId)
        {
            if (entry == null) return;
            var rm = UnitySingleton<GameManager>.Instance?.resourceManager;
            if (rm == null) return;
            var info = rm.GetItemInfo(itemId);
            if (info == null) return;

            var valueText = entry.transform.GetChild(1).GetComponent<TextMeshProUGUI>();
            if (valueText != null) ((TMP_Text)valueText).text = info.unusedCount.ToString();
        }

        private static void EnhanceTooltipValues(UITopBar __instance)
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return;

            var rm = gm.resourceManager;
            if (rm == null) return;

            EnhanceResourceTooltip(__instance, "logsValueText", "ItemLogs", rm);
            EnhanceResourceTooltip(__instance, "planksValueText", "ItemWoodPlanks", rm);
            EnhanceResourceTooltip(__instance, "firewoodValueText", "ItemFirewood", rm);
            EnhanceResourceTooltip(__instance, "stoneValueText", "ItemStone", rm);
            EnhanceResourceTooltip(__instance, "clayValueText", "ItemClay", rm);
            EnhanceResourceTooltip(__instance, "brickValueText", "ItemBrick", rm);

            // Custom entries we added: tooltip provider lives on each cloned entry.
            EnhanceCustomEntryTooltip(FFUIOverhaulMod.UITopBarIronEntry,  "ItemIron",  rm);
            EnhanceCustomEntryTooltip(FFUIOverhaulMod.UITopBarSandEntry,  "ItemSand",  rm);
            EnhanceCustomEntryTooltip(FFUIOverhaulMod.UITopBarGlassEntry, "ItemGlass", rm);
            EnhanceCustomEntryTooltip(FFUIOverhaulMod.UITopBarCoalEntry,  "ItemCoal",  rm);
        }

        private static void EnhanceCustomEntryTooltip(GameObject? entry, string itemId, ResourceManager rm)
        {
            if (entry == null) return;
            var tooltip = entry.GetComponent<GenericTooltipDataProvider>();
            if (tooltip != null) PopulateProducerTooltip(tooltip, itemId, rm);
        }

        /// <summary>
        /// UITopBar only stores the per-resource value TMP as a private field — the
        /// GenericTooltipDataProvider lives on the entry's parent GameObject (same
        /// pattern as the iron entry we instantiate). So we walk: TMP → parent → provider.
        /// Silently no-ops if the field name doesn't match this game version.
        /// </summary>
        private static void EnhanceResourceTooltip(UITopBar __instance, string valueTextFieldName,
            string itemId, ResourceManager rm)
        {
            var valueText = ReflectionHelper.GetPrivateField(valueTextFieldName, __instance) as TextMeshProUGUI;
            if (valueText == null) return;
            var tooltipProvider = valueText.transform.parent.gameObject.GetComponent<GenericTooltipDataProvider>();
            if (tooltipProvider == null) return;
            PopulateProducerTooltip(tooltipProvider, itemId, rm);
        }

        /// <summary>
        /// Rewrites a tooltip's row list to show:
        ///   Production    ~N / mo  (12-month rolling avg)
        ///   Producers:
        ///     <BuildingName>    <count>
        ///     ...
        ///
        /// "Active producers" = buildings whose 12-month tally for this item is > 0.
        /// New buildings won't appear until they make their first batch — that's a
        /// reasonable signal: shows what's actually contributing right now.
        /// </summary>
        // Cache the actual key used in monthlyProductionTallies for each itemId,
        // resolved on first non-zero hit. Different production paths use different
        // keys (e.g. ProduceItems uses item.name, special handlers like
        // ReportGatheredFruit hardcode "ItemFruit") — we resolve by probing.
        private static readonly Dictionary<string, string> _resolvedTallyKey = new();
        private static FieldInfo? _talliesField;

        // Hardcoded fallback for known mismatches between item ID and the actual
        // tally key the production code path stores. Discovered via the diagnostic
        // dump (see Diagnostic logs in MelonLoader/Latest.log).
        private static readonly Dictionary<string, string> _itemIdToTallyKey = new()
        {
            { "ItemWoodPlanks", "ItemPlanks" }, // SawPitBuilding stores under ItemPlanks
        };

        private static void PopulateProducerTooltip(GenericTooltipDataProvider provider,
            string itemId, ResourceManager rm)
        {
            provider.toolTipRowKeyNames.Clear();
            provider.toolTipRowValues.Clear();

            // Use a hardcoded strip word per resource — entry.name from
            // ItemSetupData turned out to NOT match the building displayName
            // prefix in practice (e.g. for ItemFirewood the strip didn't
            // happen, leaving "Firewood Splitter2" smushed). The set is small
            // and stable; prefer determinism over indirection here.
            string? displayName = _resourceStripWord.TryGetValue(itemId, out var w)
                ? w
                : GlobalAssets.itemSetupData?.GetItemEntry(itemId)?.name;

            uint annualTotal = 0;
            var counts = new Dictionary<string, int>();
            foreach (var b in rm.allBuildingsRO)
            {
                if (b == null) continue;
                uint tally = ResolveTally(b, itemId, displayName);
                if (tally == 0) continue;
                annualTotal += tally;
                var name = string.IsNullOrEmpty(b.displayName) ? b.GetType().Name : b.displayName;
                name = StripResourcePrefix(name, displayName);
                counts.TryGetValue(name, out int prev);
                counts[name] = prev + 1;
            }

            uint monthlyAvg = annualTotal / 12u;
            provider.AddKeyValue("Production (avg)", $"~{monthlyAvg}/mo");

            if (counts.Count == 0)
            {
                provider.AddKeyValue("No active producers", "");
                return;
            }

            provider.AddKeyValue("", "");
            provider.AddKeyValue("Producers:", "");
            foreach (var kv in counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
                provider.AddKeyValue($"  {kv.Key}", kv.Value.ToString());
        }

        /// <summary>
        /// Find the actual tally for `itemId` on this building. Tries:
        /// 1. cached key (if previously resolved for this itemId),
        /// 2. itemId itself (e.g. "ItemFruit"),
        /// 3. display name (e.g. "Wood Planks"),
        /// 4. dict scan via reflection — match any key that looks plausibly related,
        ///    e.g. itemId stripped of "Item" prefix, or displayName without spaces.
        /// First non-zero result wins and the key is cached.
        /// </summary>
        private static uint ResolveTally(Building b, string itemId, string? displayName)
        {
            // Cached path
            if (_resolvedTallyKey.TryGetValue(itemId, out string cachedKey))
                return b.GetProductionItemTally(cachedKey);

            // Hardcoded mismatch map first (e.g. ItemWoodPlanks → ItemPlanks)
            if (_itemIdToTallyKey.TryGetValue(itemId, out var mappedKey))
            {
                uint mv = b.GetProductionItemTally(mappedKey);
                if (mv > 0) { _resolvedTallyKey[itemId] = mappedKey; return mv; }
            }

            uint v = b.GetProductionItemTally(itemId);
            if (v > 0) { _resolvedTallyKey[itemId] = itemId; return v; }

            if (!string.IsNullOrEmpty(displayName) && displayName != itemId)
            {
                v = b.GetProductionItemTally(displayName);
                if (v > 0) { _resolvedTallyKey[itemId] = displayName; return v; }
            }

            // Reflect into the dict to find a plausible key. Only do this when
            // we still haven't resolved — once cached we skip this branch.
            if (_talliesField == null)
            {
                _talliesField = typeof(Building).GetField("monthlyProductionTallies",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }
            var dict = _talliesField?.GetValue(b) as System.Collections.IDictionary;
            if (dict == null || dict.Count == 0) return 0;

            string stripped = itemId.StartsWith("Item", StringComparison.Ordinal)
                ? itemId.Substring(4) : itemId;
            string compact = displayName?.Replace(" ", "") ?? string.Empty;

            foreach (string key in dict.Keys)
            {
                if (string.Equals(key, stripped, StringComparison.OrdinalIgnoreCase) ||
                    (compact.Length > 0 && string.Equals(key, compact, StringComparison.OrdinalIgnoreCase)))
                {
                    v = b.GetProductionItemTally(key);
                    if (v > 0) { _resolvedTallyKey[itemId] = key; return v; }
                }
            }
            return 0;
        }

        /// <summary>
        /// Compact a building name for tooltip rows:
        /// 1. Strip the resource's display word as prefix ("Firewood Splitter…" → "Splitter…")
        /// 2. Strip generic suffixes (" Workshop", " Building", " Lodge")
        /// The tooltip uses fixed columns; long names smush into the value column
        /// because the layout doesn't auto-pad. Compact names dodge the issue.
        /// </summary>
        private static string StripResourcePrefix(string buildingName, string? resourceWord)
        {
            if (string.IsNullOrEmpty(buildingName)) return buildingName;
            string s = buildingName;
            if (!string.IsNullOrEmpty(resourceWord) &&
                s.StartsWith(resourceWord + " ", StringComparison.OrdinalIgnoreCase))
            {
                s = s.Substring(resourceWord!.Length + 1).TrimStart();
            }
            foreach (var suffix in _stripSuffixes)
            {
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suffix.Length).TrimEnd();
                    break;
                }
            }
            return s.Length > 0 ? s : buildingName;
        }

        private static readonly string[] _stripSuffixes = { " Workshop", " Building", " Lodge" };

        private static readonly Dictionary<string, string> _resourceStripWord = new()
        {
            { "ItemLogs", "Logs" },
            { "ItemWoodPlanks", "Wood Plank" },
            { "ItemFirewood", "Firewood" },
            { "ItemStone", "Stone" },
            { "ItemClay", "Clay" },
            { "ItemBrick", "Brick" },
            { "ItemIron", "Iron" },
            { "ItemSand", "Sand" },
            { "ItemGlass", "Glass" },
            { "ItemCoal", "Coal" },
        };

    }

    [HarmonyPatch(typeof(UITopBar), "UpdateResourceAlerts")]
    public class PatchUITopBarResourceAlerts
    {
        public static void Postfix(UITopBar __instance)
        {
            var rm = UnitySingleton<GameManager>.Instance?.resourceManager;
            if (rm == null) return;
            UpdateAlertFor(FFUIOverhaulMod.UITopBarIronEntry,  "ItemIron",  rm);
            UpdateAlertFor(FFUIOverhaulMod.UITopBarSandEntry,  "ItemSand",  rm);
            UpdateAlertFor(FFUIOverhaulMod.UITopBarGlassEntry, "ItemGlass", rm);
            UpdateAlertFor(FFUIOverhaulMod.UITopBarCoalEntry,  "ItemCoal",  rm);
        }

        private static void UpdateAlertFor(GameObject? entry, string itemId, ResourceManager rm)
        {
            if (entry == null) return;
            var info = rm.GetItemInfo(itemId);
            if (info == null) return;
            var alert = entry.transform
                .GetChild(0).GetChild(0).GetChild(0)
                .GetComponent<UIResourceAlert>();
            alert?.UpdateAlert(info.unusedCount);
        }
    }
}
