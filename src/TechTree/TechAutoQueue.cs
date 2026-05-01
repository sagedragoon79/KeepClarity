using System.Collections.Generic;
using FFUIOverhaul.Patches;
using FFUIOverhaul.UI;
using MelonLoader;
using UnityEngine;

namespace FFUIOverhaul.TechTree
{
    /// <summary>
    /// Auto-research queue. The player adds tech node IDs (right-click in the tech
    /// tree); when KP becomes available, we spend on the topmost queued tech.
    ///
    /// Auto-walks prereqs: if the queued tech is in `Unlocked` state (prereqs not
    /// met), we recurse through `prereqNodeIds` until we find an ancestor in
    /// `PrereqsMet` state and spend there. This means queueing a Tier 4 tech with
    /// nothing else researched will auto-research the full chain.
    /// </summary>
    public static class TechAutoQueue
    {
        private static readonly List<int> _queue = new();

        public static IReadOnlyList<int> Queue => _queue;
        public static int Count => _queue.Count;

        public static int IndexOf(int id) => _queue.IndexOf(id);
        public static bool Contains(int id) => _queue.Contains(id);

        /// <summary>
        /// Returns the id of the node that auto-spend would target next — i.e.
        /// the deepest PrereqsMet ancestor in the prereq chain of the topmost
        /// non-completed queue item. Used by pin widgets to show a "currently
        /// being researched" marker on prereq nodes that aren't in the queue.
        /// Returns -1 if no spend target is available (queue empty, target
        /// already Active, or no PrereqsMet ancestor exists).
        /// </summary>
        public static int GetActiveSpendTarget()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            var tm = gm?.techTreeManager;
            if (tm == null) return -1;
            foreach (int id in _queue)
            {
                int found = FindActivePrereqId(tm, id);
                if (found >= 0) return found;
            }
            return -1;
        }

        private static int FindActivePrereqId(TechTreeManager tm, int id)
        {
            if (!tm.GetTechTreeNodeData(id, out _, out _, out var state,
                out int numRanks, out int curRank, out int[] prereqIds, out _)) return -1;
            if (state == TechTreeNodeData.State.Active || curRank >= numRanks) return -1;
            if (state == TechTreeNodeData.State.PrereqsMet) return id;
            if (prereqIds != null)
            {
                foreach (int p in prereqIds)
                {
                    int found = FindActivePrereqId(tm, p);
                    if (found >= 0) return found;
                }
            }
            return -1;
        }

        public static void Load()
        {
            _queue.Clear();
            var raw = FFUIOverhaulMod.TechResearchQueue?.Value ?? "";
            if (string.IsNullOrEmpty(raw)) return;
            foreach (var part in raw.Split(','))
                if (int.TryParse(part.Trim(), out int id)) _queue.Add(id);
        }

        private static void Save()
        {
            if (FFUIOverhaulMod.TechResearchQueue == null) return;
            FFUIOverhaulMod.TechResearchQueue.Value = string.Join(",", _queue);
            MelonPreferences.Save();
        }

        public static void Toggle(int id)
        {
            if (id < 0) return;
            if (_queue.Contains(id))
                _queue.Remove(id);
            else
                _queue.Add(id);
            Save();
            TechNodePinWidget.RefreshAll();
            TechQueueStrip.RefreshText();
        }

        public static void Clear()
        {
            _queue.Clear();
            Save();
            TechNodePinWidget.RefreshAll();
            TechQueueStrip.RefreshText();
        }

        /// <summary>
        /// Spend every available knowledge point on the queue (with prereq walking).
        /// Returns the number of points actually spent.
        /// </summary>
        public static int TrySpendAll()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            var tm = gm?.techTreeManager;
            if (tm == null || _queue.Count == 0) return 0;

            int spent = 0;
            bool changed = true;
            // Each loop: prune completed items, then try to spend one. If we spent,
            // loop again (the queue could now point at the next item).
            while (changed)
            {
                changed = false;
                PruneCompleted(tm);
                if (tm.knowledgePoints <= 0 || _queue.Count == 0) break;

                foreach (int targetId in _queue)
                {
                    if (TrySpendForTarget(tm, targetId))
                    {
                        spent++;
                        changed = true;
                        break;
                    }
                }
            }

            if (spent > 0)
            {
                Save();
                TechNodePinWidget.RefreshAll();
            TechQueueStrip.RefreshText();
            }
            return spent;
        }

        private static void PruneCompleted(TechTreeManager tm)
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                int id = _queue[i];
                if (!tm.GetTechTreeNodeData(id, out _, out _, out var state, out int numRanks, out int curRank, out _, out _))
                {
                    _queue.RemoveAt(i); // node doesn't exist — drop it
                    continue;
                }
                if (state == TechTreeNodeData.State.Active || curRank >= numRanks)
                    _queue.RemoveAt(i);
            }
        }

        private static bool TrySpendForTarget(TechTreeManager tm, int targetId)
        {
            if (tm.knowledgePoints <= 0) return false;
            if (!tm.GetTechTreeNodeData(targetId, out _, out _, out var state,
                out int numRanks, out int curRank, out int[] prereqIds, out _)) return false;

            if (state == TechTreeNodeData.State.Active || curRank >= numRanks) return false;

            if (state == TechTreeNodeData.State.PrereqsMet)
            {
                tm.ActivateTechOrRank(targetId, 1, onLoad: false);
                return true;
            }

            // Prereqs not met — walk back. Spend on the deepest ancestor that's ready.
            if (prereqIds != null)
            {
                foreach (int prereqId in prereqIds)
                    if (TrySpendForTarget(tm, prereqId)) return true;
            }
            return false;
        }
    }
}
