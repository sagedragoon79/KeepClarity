using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Named blueprints on disk: <c>Mods/Blueprints/&lt;name&gt;.json</c>.
    ///
    /// GLOBAL, NOT PER-SAVE — deliberately. Entries are offsets from a stamp
    /// origin, so a blueprint is meaningful in any town, and a plain folder of
    /// files is shareable: players can post a layout and others drop it in. Same
    /// drop-in-folder shape Keep Clarity uses for translation packs.
    ///
    /// Serialization is Newtonsoft (shipped with the game, in its Managed folder).
    /// Unity's JsonUtility was tried first and SILENTLY dropped the entries list:
    /// it wrote a well-formed file containing only {schema, name, created}, with no
    /// error and no exception, so saves looked successful while the layout was
    /// simply gone. Do not go back to it.
    /// </summary>
    internal static class BlueprintStore
    {
        public const string DirName = "Blueprints";

        /// <summary>Cached listing so the panel isn't hitting the disk every
        /// OnGUI pass (IMGUI redraws several times a frame).</summary>
        private static List<Blueprint>? _cache;
        private static readonly HashSet<string> _warned = new HashSet<string>();

        public static string Directory()
        {
            // Application.dataPath is <game>/Farthest Frontier_Data, so "../Mods"
            // lands beside the game's other mod folders.
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Mods", DirName));
        }

        public static void Invalidate() { _cache = null; _warned.Clear(); }

        /// <summary>All saved blueprints, newest-named-first (alphabetical).</summary>
        public static List<Blueprint> All()
        {
            if (_cache != null) return _cache;

            var list = new List<Blueprint>();
            try
            {
                var dir = Directory();
                if (System.IO.Directory.Exists(dir))
                {
                    var files = System.IO.Directory.GetFiles(dir, "*.json");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    foreach (var f in files)
                    {
                        var bp = LoadFile(f);
                        if (bp != null) list.Add(bp);
                    }
                }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[Store] listing failed: " + e.Message);
            }

            _cache = list;
            return list;
        }

        public static Blueprint? LoadFile(string path)
        {
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                var bp = JsonConvert.DeserializeObject<Blueprint>(json);
                if (bp == null || bp.entries == null || bp.entries.Count == 0)
                {
                    // Warn once per file: the listing is re-read on every panel
                    // interaction, and a bad file would otherwise flood the log.
                    if (_warned.Add(path))
                        FFUIOverhaulMod.Log.Warning($"[Store] '{Path.GetFileName(path)}' has no entries — skipped.");
                    return null;
                }
                // Trust the filename over the stored name: renaming the file is the
                // obvious way to rename a blueprint, and the two must not disagree.
                bp.name = Path.GetFileNameWithoutExtension(path);
                return bp;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[Store] '{Path.GetFileName(path)}' failed to load: {e.Message}");
                return null;
            }
        }

        /// <summary>Save under a player-supplied name. Returns true on success.</summary>
        public static bool Save(Blueprint bp, string name)
        {
            if (bp == null || bp.entries.Count == 0)
            {
                FFUIOverhaulMod.Log.Warning("[Store] nothing to save.");
                return false;
            }

            string safe = Sanitize(name);
            if (safe.Length == 0)
            {
                FFUIOverhaulMod.Log.Warning("[Store] blueprint needs a name.");
                return false;
            }

            try
            {
                var dir = Directory();
                System.IO.Directory.CreateDirectory(dir);
                bp.name = safe;
                bp.schema = Blueprint.CurrentSchema;
                if (string.IsNullOrEmpty(bp.created))
                    bp.created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                var path = Path.Combine(dir, safe + ".json");
                File.WriteAllText(path, JsonConvert.SerializeObject(bp, Formatting.Indented), new UTF8Encoding(false));
                Invalidate();
                FFUIOverhaulMod.Log.Msg($"[Store] saved '{safe}' ({bp.entries.Count} building(s)) → {path}");
                return true;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Error("[Store] save failed: " + e.Message);
                return false;
            }
        }

        public static bool Delete(string name)
        {
            try
            {
                var path = Path.Combine(Directory(), Sanitize(name) + ".json");
                if (!File.Exists(path)) return false;
                File.Delete(path);
                Invalidate();
                FFUIOverhaulMod.Log.Msg($"[Store] deleted '{name}'.");
                return true;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Error("[Store] delete failed: " + e.Message);
                return false;
            }
        }

        public static bool Exists(string name)
        {
            try { return File.Exists(Path.Combine(Directory(), Sanitize(name) + ".json")); }
            catch { return false; }
        }

        /// <summary>Strip anything that can't be a filename — the name is the
        /// identity, so it has to survive a round trip through the filesystem.</summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder(name.Length);
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in name.Trim())
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return sb.ToString().Trim();
        }
    }
}
