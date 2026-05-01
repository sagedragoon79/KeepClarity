using System.Collections.Generic;
using FFUIOverhaul.TechTree;
using FFUIOverhaul.UI;
using FFUIOverhaul.Utils;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    // Note: we used to patch TechTreeNode's IPointerClickHandler.OnPointerClick to
    // handle right-click → queue toggle. That's an explicit interface impl, and
    // patching it crashed the tech tree. Now we poll Input + raycast in
    // FFUIOverhaulMod.OnUpdate (see TechQueueInput) — no patching needed.

    /// <summary>
    /// When KP is awarded (monthly tick from buildings, bonuses, etc.), try to
    /// spend it on the queue. Skips if no queue is set.
    /// </summary>
    [HarmonyPatch(typeof(TechTreeManager), "AddKnowledgePoints")]
    static class TechAutoSpendPatch
    {
        static void Postfix()
        {
            try { TechAutoQueue.TrySpendAll(); }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[TechQueue] Auto-spend failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// When the tech tree window opens, attach a numbered pin to every node so
    /// queued items show their position. Also injects a header strip at the top
    /// of the window listing the queue.
    /// </summary>
    [HarmonyPatch(typeof(UITechTreeOverview), "Open")]
    static class TechTreeOpenPatch
    {
        static void Postfix(UITechTreeOverview __instance)
        {
            try
            {
                var nodes = __instance.GetComponentsInChildren<TechTreeNode>(includeInactive: true);
                foreach (var node in nodes)
                {
                    if (node == null) continue;
                    TechNodePinWidget.Attach(node);
                }
                TechNodePinWidget.RefreshAll();
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[TechTree] Failed to inject queue UI: {e.Message}\n{e.StackTrace}");
            }
        }
    }

    // Note: previously had a TechQueueStrip / Close patch that lived inside the
    // tech tree window. It never positioned reliably against the tree's nested
    // viewport/scrollRect/canvas hierarchy. Replaced with TechQueueMainOverlay,
    // an always-visible main-screen panel modeled on PinnedResourceOverlay.
    // The class below is kept only as a no-op stub so any stale callers compile.
    public static class TechQueueStrip
    {
        public static void EnsureBuilt(UITechTreeOverview window) { }
        public static void RefreshText() => FFUIOverhaulMod.Instance?.RefreshTechQueueOverlay();
        public static void SetVisible(bool visible) { }
    }

    /// <summary>
    /// Enhance the KP scroll icon's hover tooltip on the top bar to list the
    /// auto-research queue. Reads the queue state directly from TechAutoQueue.
    /// </summary>
    [HarmonyPatch(typeof(UITopBar), "UpdateResourceValues")]
    static class KnowledgePointTooltipPatch
    {
        static void Postfix(UITopBar __instance)
        {
            try
            {
                var kpText = ReflectionHelper.GetPrivateField("knowledgePointText", __instance) as TextMeshProUGUI;
                if (kpText == null) return;
                var entry = kpText.transform.parent.gameObject;
                var tooltip = entry.GetComponent<GenericTooltipDataProvider>();
                if (tooltip == null) return;

                tooltip.toolTipRowKeyNames.Clear();
                tooltip.toolTipRowValues.Clear();

                var gm = UnitySingleton<GameManager>.Instance;
                var tm = gm?.techTreeManager;
                if (tm == null) return;

                tooltip.AddKeyValue("Knowledge Points", tm.knowledgePoints.ToString());

                if (TechAutoQueue.Count == 0)
                {
                    tooltip.AddKeyValue("", "");
                    tooltip.AddKeyValue("Right-click a tech to queue it", "");
                    return;
                }

                tooltip.AddKeyValue("", "");
                tooltip.AddKeyValue("Auto-research queue:", "");
                int pos = 1;
                foreach (int id in TechAutoQueue.Queue)
                {
                    string name = ResolveTechName(tm, id);
                    string state = ResolveStateAbbrev(tm, id);
                    tooltip.AddKeyValue($"  {pos}. {name}", state);
                    pos++;
                }
            }
            catch (System.Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[TechQueue] KP tooltip failed: {e.Message}");
            }
        }

        private static string ResolveTechName(TechTreeManager tm, int id)
        {
            // The decompile doesn't expose a name lookup, but the tooltip body
            // is the localized description. The id alone is OK as a fallback.
            // Future: walk techTreeNodeData and pick out a display name.
            var entry = GlobalAssets.itemSetupData?.GetItemEntry(id.ToString());
            if (entry != null && !string.IsNullOrEmpty(entry.name)) return entry.name;
            return $"Tech #{id}";
        }

        private static string ResolveStateAbbrev(TechTreeManager tm, int id)
        {
            if (!tm.GetTechTreeNodeData(id, out _, out _, out var state, out int numRanks, out int curRank, out _, out _))
                return "?";
            return state switch
            {
                TechTreeNodeData.State.PrereqsMet => $"{curRank}/{numRanks}",
                TechTreeNodeData.State.Active     => "✓",
                TechTreeNodeData.State.Unlocked   => "(prereq)",
                TechTreeNodeData.State.Unknown    => "(locked)",
                _ => state.ToString()
            };
        }
    }
}
