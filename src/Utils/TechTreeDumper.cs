using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace FFUIOverhaul.Utils
{
    /// <summary>
    /// Diagnostic: Ctrl+Shift+F8 dumps the ENTIRE tech tree to log + file.
    /// For every node: name, id, tier, ranks, prerequisites, and each attached
    /// GameEffect (its class type, asset name, per-rank value, value at max rank,
    /// and the game's own displayed string). This is the ground-truth mapping of
    /// tech -> effect -> value that the baked ScriptableObjects hold, which the
    /// DLL alone can't show. Doubles as a complete tech-tree knowledge doc.
    /// </summary>
    internal static class TechTreeDumper
    {
        // Cached reflection into TechTreeNodeData's private [SerializeField]s.
        private static FieldInfo? _fName, _fId, _fTier, _fRanks, _fPrereq;
        private static bool _reflectionReady;

        public static void Run()
        {
            try
            {
                var tm = UnityEngine.Object.FindObjectOfType<TechTreeManager>();
                if (tm == null) { FFUIOverhaulMod.Log.Msg("[TechDump] No TechTreeManager (load a save first)."); return; }

                var nodes = tm.techTreeNodeData;
                if (nodes == null || nodes.Count == 0) { FFUIOverhaulMod.Log.Msg("[TechDump] Tech node list empty."); return; }

                if (!EnsureReflection(nodes[0].GetType())) { FFUIOverhaulMod.Log.Error("[TechDump] Could not reflect TechTreeNodeData fields."); return; }

                // id -> name map for resolving prereqs.
                var idToName = new Dictionary<int, string>();
                foreach (var n in nodes)
                {
                    if (n == null) continue;
                    int id = (int)(_fId!.GetValue(n) ?? -1);
                    string nm = (string)(_fName!.GetValue(n) ?? "?");
                    if (!idToName.ContainsKey(id)) idToName[id] = nm;
                }

                // Sort by tier then id for a readable doc.
                var sorted = new List<object>(nodes);
                sorted.Sort((a, b) =>
                {
                    int ta = (int)(_fTier!.GetValue(a) ?? 0), tb = (int)(_fTier!.GetValue(b) ?? 0);
                    if (ta != tb) return ta.CompareTo(tb);
                    int ia = (int)(_fId!.GetValue(a) ?? 0), ib = (int)(_fId!.GetValue(b) ?? 0);
                    return ia.CompareTo(ib);
                });

                var sb = new StringBuilder();
                sb.AppendLine("=== FARTHEST FRONTIER TECH TREE DUMP ===");
                sb.AppendLine($"Total nodes: {nodes.Count}");
                sb.AppendLine();

                int lastTier = -1;
                foreach (var n in sorted)
                {
                    if (n == null) continue;
                    int id = (int)(_fId!.GetValue(n) ?? -1);
                    string name = (string)(_fName!.GetValue(n) ?? "?");
                    int tier = (int)(_fTier!.GetValue(n) ?? 0);
                    int ranks = (int)(_fRanks!.GetValue(n) ?? 1);
                    var prereqIds = _fPrereq!.GetValue(n) as int[];

                    if (tier != lastTier)
                    {
                        sb.AppendLine($"################ TIER {tier} ################");
                        lastTier = tier;
                    }

                    string prereqs = "none";
                    if (prereqIds != null && prereqIds.Length > 0)
                    {
                        var parts = new List<string>(prereqIds.Length);
                        foreach (var pid in prereqIds)
                            parts.Add(idToName.TryGetValue(pid, out var pn) ? $"{pn}({pid})" : pid.ToString());
                        prereqs = string.Join(", ", parts.ToArray());
                    }

                    sb.AppendLine($"[{name}]  id={id}  tier={tier}  ranks={ranks}");
                    sb.AppendLine($"    prereqs: {prereqs}");

                    var entries = GetEffectEntries(n);
                    if (entries == null || entries.Count == 0)
                        sb.AppendLine("    effects: (none)");
                    else
                    {
                        foreach (var e in entries)
                        {
                            if (e == null) continue;
                            var ge = e.gameEffect;
                            float v = e.value;
                            string cls = ge != null ? ge.GetType().Name : "<null>";
                            string soName = ge != null ? ge.name : "?";
                            string perRank, atMax;
                            try { perRank = ge != null ? ge.GetString(v) : v.ToString(); } catch { perRank = v.ToString(); }
                            try { atMax = ge != null ? ge.GetString(v * ranks) : (v * ranks).ToString(); } catch { atMax = (v * ranks).ToString(); }
                            string skip = e.skipDescriptionDisplay ? "  [hidden in UI]" : "";
                            sb.AppendLine($"    effect: {cls}  \"{soName}\"  value/rank={v}  display/rank={perRank}  atMaxRank({ranks})={atMax}{skip}");
                        }
                    }
                    sb.AppendLine();
                }

                string text = sb.ToString();
                FFUIOverhaulMod.Log.Msg(text);
                try
                {
                    string dir = MelonLoader.Utils.MelonEnvironment.MelonLoaderDirectory;
                    string file = System.IO.Path.Combine(dir, "KC_TechTreeDump.txt");
                    System.IO.File.WriteAllText(file, text);
                    FFUIOverhaulMod.Log.Msg("[TechDump] Written: " + file);
                }
                catch (Exception fe) { FFUIOverhaulMod.Log.Warning("[TechDump] file write failed: " + fe.Message); }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Error("[TechDump] failed: " + e);
            }
        }

        private static bool EnsureReflection(Type t)
        {
            if (_reflectionReady) return _fName != null;
            _reflectionReady = true;
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            _fName = t.GetField("techName", BF);
            _fId = t.GetField("id", BF);
            _fTier = t.GetField("tier", BF);
            _fRanks = t.GetField("numRanks", BF);
            _fPrereq = t.GetField("prereqNodeIds", BF);
            return _fName != null && _fId != null && _fTier != null && _fRanks != null && _fPrereq != null;
        }

        // gameEffectsEntries is a public field (List<GameEffectEntry>); access directly.
        private static List<GameEffectEntry>? GetEffectEntries(object node)
        {
            var fld = node.GetType().GetField("gameEffectsEntries",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return fld?.GetValue(node) as List<GameEffectEntry>;
        }
    }
}
