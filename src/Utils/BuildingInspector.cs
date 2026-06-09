using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace FFUIOverhaul.Utils
{
    /// <summary>
    /// Diagnostic: Ctrl+Shift+F9 with a building selected dumps its render
    /// structure — every Renderer, its mesh + submeshes, each material's shader
    /// and color/texture properties (so we know what's tintable), and which
    /// sub-parts SHARE a material (so we know what tinting one would affect).
    ///
    /// This is the recon step for the "building colour variety" idea: it tells
    /// us (a) whether FF's building shaders expose a tint colour at all, and
    /// (b) whether parts like the door / roof are separable renderers/materials
    /// or baked into one mesh.
    ///
    /// Output: MelonLoader log + a text file under the MelonLoader folder.
    /// </summary>
    internal static class BuildingInspector
    {
        private static FieldInfo? _selectedObjField;

        public static void Run()
        {
            try
            {
                var gm = UnitySingleton<GameManager>.Instance;
                if (gm == null) { FFUIOverhaulMod.Log.Msg("[BuildingInspector] No GameManager (load a save first)."); return; }

                var selected = GetSelectedGameObject(gm);
                var building = selected != null
                    ? (selected.GetComponent<Building>() ?? selected.GetComponentInParent<Building>())
                    : null;
                if (building == null)
                {
                    FFUIOverhaulMod.Log.Msg("[BuildingInspector] No building selected. Click a building, then press Ctrl+Shift+F9.");
                    return;
                }

                var root = building.gameObject;
                var sb = new StringBuilder();
                string title = root.name + " (" + building.GetType().Name + ")";
                sb.AppendLine("=== Building Material Dump: " + title + " ===");

                var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
                sb.AppendLine("Renderers: " + renderers.Length);

                // Track material sharing: shared-material instanceID -> list of part paths.
                var matToParts = new Dictionary<int, List<string>>();
                var matColorInfo = new Dictionary<int, string>();

                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    string path = RelativePath(root.transform, r.transform);
                    string meshInfo = MeshInfo(r);
                    sb.AppendLine($"[{i}] {path}  ({r.GetType().Name})  {meshInfo}  active={r.gameObject.activeInHierarchy}");

                    var mats = r.sharedMaterials;
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null) { sb.AppendLine($"      material[{m}]: <null>"); continue; }

                        int id = mat.GetInstanceID();
                        if (!matToParts.TryGetValue(id, out var parts)) { parts = new List<string>(); matToParts[id] = parts; }
                        parts.Add(path + (mats.Length > 1 ? $"#sub{m}" : ""));

                        var shader = mat.shader;
                        string shaderName = shader != null ? shader.name : "<null shader>";
                        sb.AppendLine($"      material[{m}]: \"{mat.name}\"  shader=\"{shaderName}\"");

                        // Enumerate shader properties; highlight colours (tint points).
                        var colorProps = new List<string>();
                        var texProps = new List<string>();
                        if (shader != null)
                        {
                            int count = shader.GetPropertyCount();
                            for (int p = 0; p < count; p++)
                            {
                                string pname = shader.GetPropertyName(p);
                                var ptype = shader.GetPropertyType(p);
                                if (ptype == ShaderPropertyType.Color)
                                {
                                    Color c = mat.HasProperty(pname) ? mat.GetColor(pname) : default;
                                    colorProps.Add($"{pname}=({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00})");
                                }
                                else if (ptype == ShaderPropertyType.Texture)
                                {
                                    var tex = mat.HasProperty(pname) ? mat.GetTexture(pname) : null;
                                    texProps.Add($"{pname}={(tex != null ? tex.name : "null")}");
                                }
                            }
                        }

                        sb.AppendLine("        colors: " + (colorProps.Count > 0 ? string.Join("  ", colorProps.ToArray()) : "(none — NOT tintable via _Color)"));
                        sb.AppendLine("        textures: " + (texProps.Count > 0 ? string.Join("  ", texProps.ToArray()) : "(none)"));

                        if (!matColorInfo.ContainsKey(id))
                        {
                            bool tintable = colorProps.Count > 0;
                            matColorInfo[id] = $"\"{mat.name}\" shader=\"{shaderName}\" tintable={tintable}";
                        }
                    }
                }

                // Material-sharing summary — the key question for per-part tinting.
                sb.AppendLine("--- Material sharing (tinting one material affects ALL its parts) ---");
                foreach (var kv in matToParts)
                {
                    string info = matColorInfo.TryGetValue(kv.Key, out var s) ? s : kv.Key.ToString();
                    sb.AppendLine($"  {info}");
                    sb.AppendLine($"      used by {kv.Value.Count} part(s): {string.Join(", ", kv.Value.ToArray())}");
                }

                // Name hints — does anything read as door / roof / wall?
                sb.AppendLine("--- Part-name hints (for targeting door/roof/etc.) ---");
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    string n = r.gameObject.name.ToLowerInvariant();
                    if (n.Contains("door") || n.Contains("roof") || n.Contains("wall") || n.Contains("window")
                        || n.Contains("trim") || n.Contains("flag") || n.Contains("banner") || n.Contains("cloth"))
                        sb.AppendLine($"  {RelativePath(root.transform, r.transform)}");
                }

                string text = sb.ToString();
                FFUIOverhaulMod.Log.Msg(text);

                // Also write a file for easy reading.
                try
                {
                    string dir = MelonLoader.Utils.MelonEnvironment.MelonLoaderDirectory;
                    string safe = MakeSafe(root.name);
                    string file = System.IO.Path.Combine(dir, $"KC_BuildingDump_{safe}.txt");
                    System.IO.File.WriteAllText(file, text);
                    FFUIOverhaulMod.Log.Msg("[BuildingInspector] Written: " + file);
                }
                catch (Exception fe) { FFUIOverhaulMod.Log.Warning("[BuildingInspector] file write failed: " + fe.Message); }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Error("[BuildingInspector] failed: " + e);
            }
        }

        private static string MeshInfo(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null)
                return $"mesh={smr.sharedMesh.name} submeshes={smr.sharedMesh.subMeshCount}";
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                return $"mesh={mf.sharedMesh.name} submeshes={mf.sharedMesh.subMeshCount}";
            return "mesh=?";
        }

        private static string RelativePath(Transform root, Transform t)
        {
            if (t == root) return "(root)";
            var stack = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { stack.Add(cur.name); cur = cur.parent; }
            stack.Reverse();
            return string.Join("/", stack.ToArray());
        }

        private static string MakeSafe(string s)
        {
            foreach (var ch in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            return s.Replace(' ', '_');
        }

        private static GameObject? GetSelectedGameObject(GameManager gm)
        {
            if (_selectedObjField == null)
                _selectedObjField = typeof(InputManager).GetField("selectedObject",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return _selectedObjField?.GetValue(gm.inputManager) as GameObject;
        }
    }
}
