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

        // Persistence format (per-save scoped):
        //   savefile1=id1,id2|savefile2=id3,id4|...
        // Without the per-save key, queue entries leaked across saves — the
        // pref is global to the mod install, not the save.
        private static string _loadedForSave = "__never__";

        private static string CurrentSaveName()
        {
            // Empty until the player loads or starts a save (main menu).
            return SaveManager.activeSaveFileName ?? "";
        }

        public static void EnsureLoadedForCurrentSave()
        {
            var name = CurrentSaveName();
            // Don't load (or clear) while the save name is still blank — right
            // after a load it's empty for a beat, and loading then would wipe the
            // queue to 0 until the real name resolves (markers/spend flicker off).
            if (string.IsNullOrEmpty(name)) return;
            if (name == _loadedForSave) return;
            Load();
        }

        public static void Load()
        {
            _queue.Clear();
            _loadedForSave = CurrentSaveName();
            var raw = FFUIOverhaulMod.TechResearchQueue?.Value ?? "";
            if (string.IsNullOrEmpty(raw)) return;

            // Legacy format: flat CSV "id1,id2,id3" with no per-save key.
            // Drop it on first load — these IDs probably belong to whatever
            // save was open when v1.1.0 first wrote them, and we can't know
            // which one. Cleaner to start fresh than carry the wrong queue.
            if (raw.IndexOf('|') < 0 && raw.IndexOf('=') < 0)
            {
                FFUIOverhaulMod.TechResearchQueue!.Value = "";
                MelonPreferences.Save();
                return;
            }

            foreach (var entry in raw.Split('|'))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                var key = entry.Substring(0, eq);
                if (key != _loadedForSave) continue;
                var ids = entry.Substring(eq + 1);
                foreach (var part in ids.Split(','))
                    if (int.TryParse(part.Trim(), out int id)) _queue.Add(id);
                break;
            }

            // Markers may have been attached (tech tree open) before the queue
            // loaded; refresh now so they appear the instant the queue resolves.
            TechNodePinWidget.RefreshAll();
            TechQueueStrip.RefreshText();
        }

        private static void Save()
        {
            if (FFUIOverhaulMod.TechResearchQueue == null) return;
            var name = CurrentSaveName();
            if (string.IsNullOrEmpty(name)) return; // main menu / no save active

            // Rebuild the dict: keep all other saves' entries, swap in ours.
            var raw = FFUIOverhaulMod.TechResearchQueue.Value ?? "";
            var entries = new List<string>();
            if (!string.IsNullOrEmpty(raw) && (raw.IndexOf('|') >= 0 || raw.IndexOf('=') >= 0))
            {
                foreach (var entry in raw.Split('|'))
                {
                    int eq = entry.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = entry.Substring(0, eq);
                    if (key == name) continue; // we're about to write this one
                    entries.Add(entry);
                }
            }
            if (_queue.Count > 0)
                entries.Add(name + "=" + string.Join(",", _queue));

            FFUIOverhaulMod.TechResearchQueue.Value = string.Join("|", entries);
            MelonPreferences.Save();
            _loadedForSave = name;
        }

        public static void Toggle(int id)
        {
            if (id < 0) return;
            EnsureLoadedForCurrentSave();
            if (_queue.Contains(id))
                _queue.Remove(id);
            else
                _queue.Add(id);
            Save();
            TechNodePinWidget.RefreshAll();
            TechQueueStrip.RefreshText();
            // Spend any KP banked before the queue was set up. The
            // AddKnowledgePoints postfix only catches new awards, not retro.
            TrySpendAll();
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
            EnsureLoadedForCurrentSave();
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

        /// <summary>
        /// Soft-dep bridge to Tended Wilds' published rank caps. TW exposes a
        /// public static <c>TendedWilds.TechRankCaps.GetMaxRank(string)</c>;
        /// we reflect on it so KC has no hard reference to the TW assembly.
        /// Returns -1 if TW isn't loaded, the tech name doesn't resolve, or TW
        /// doesn't cap that tech.
        /// </summary>
        internal static class TendedWildsBridge
        {
            private static bool _probed;
            private static System.Reflection.MethodInfo? _getMaxRank;
            private static readonly Dictionary<int, string> _nameCache = new();

            public static int GetReducedMaxRank(TechTreeManager tm, int techId)
            {
                if (!_probed) { Probe(); _probed = true; }
                if (_getMaxRank == null) return -1;

                if (!_nameCache.TryGetValue(techId, out var name))
                {
                    name = ResolveName(tm, techId);
                    _nameCache[techId] = name; // cache even if "" so we don't re-iterate
                }
                if (string.IsNullOrEmpty(name)) return -1;

                try { return (int)(_getMaxRank.Invoke(null, new object[] { name }) ?? -1); }
                catch { return -1; }
            }

            private static void Probe()
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = asm.GetType("TendedWilds.TechRankCaps", throwOnError: false);
                        if (t == null) continue;
                        _getMaxRank = t.GetMethod("GetMaxRank",
                            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                        if (_getMaxRank != null)
                        {
                            FFUIOverhaulMod.Log.Msg("[TechQueue] Tended Wilds rank-cap bridge active.");
                            return;
                        }
                    }
                    catch { }
                }
            }

            private static string ResolveName(TechTreeManager tm, int techId)
            {
                if (tm?.techTreeNodeData == null) return "";
                foreach (var n in tm.techTreeNodeData)
                {
                    if (n == null || n.GetId() != techId) continue;
                    var name = n.GetTechName();
                    return string.IsNullOrEmpty(name) ? "" : name;
                }
                return "";
            }
        }

        private static bool TrySpendForTarget(TechTreeManager tm, int targetId)
        {
            if (tm.knowledgePoints <= 0) return false;
            if (!tm.GetTechTreeNodeData(targetId, out _, out _, out var state,
                out int numRanks, out int curRank, out int[] prereqIds, out _)) return false;

            // Respect sibling mods' rank reductions (e.g. Tended Wilds caps
            // Woodlore at 2 ranks). TW patches FF's numRanks live but retries
            // for up to 30 minutes — without this, KC would spend the soon-to-
            // be-trimmed rank before the patch lands.
            int twCap = TendedWildsBridge.GetReducedMaxRank(tm, targetId);
            int effectiveMax = twCap > 0 ? System.Math.Min(numRanks, twCap) : numRanks;

            if (state == TechTreeNodeData.State.Active || curRank >= effectiveMax) return false;

            if (state == TechTreeNodeData.State.PrereqsMet)
            {
                tm.ActivateTechOrRank(targetId, 1, onLoad: false);
                // Audit log so if a player reports a research-completed-but-
                // building-still-locked desync, we can correlate the spend to
                // the affected tech. Single line per KP spent.
                FFUIOverhaulMod.Log.Msg($"[TechQueue] Spent KP on tech id={targetId} rank {curRank + 1}/{numRanks}");
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
