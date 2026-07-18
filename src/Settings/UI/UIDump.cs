using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// One-shot dump utility — walks every active Canvas in the scene and
    /// records the full UI tree (Images, Texts, Buttons, Toggles, Scrollbars,
    /// Sliders, Dropdowns, ScrollRects). Writes to a timestamped file in
    /// <c>MelonLoader/Logs/KeepClarity_UIDump_*.txt</c> so we can study FF's
    /// real UI surface and write targeted asset lookups instead of heuristic probes.
    ///
    /// Triggered from Plugin.cs via Shift+F10. Run with FF's options menu (or
    /// any panel whose styling you want to match) open so the dump captures it.
    /// </summary>
    public static class UIDump
    {
        public static void Run()
        {
            try
            {
                var dumpDir = Path.Combine("MelonLoader", "Logs");
                Directory.CreateDirectory(dumpDir);
                var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var path = Path.Combine(dumpDir, $"KeepClarity_UIDump_{ts}.txt");

                using (var w = new StreamWriter(path))
                {
                    w.WriteLine($"# Keep Clarity — FF UI dump @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    w.WriteLine();

                    DumpFonts(w);
                    DumpCanvases(w);
                    DumpRaycastTargets(w); // click-interception debugging (invisible blockers)
                    DumpSprites(w);
                    DumpControls(w);
                }

                FFUIOverhaulMod.Log.Msg($"[UIDump] wrote {path}");
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Error($"[UIDump] failed: {ex}");
            }
        }

        private static void DumpFonts(StreamWriter w)
        {
            w.WriteLine("## Fonts (TMP_FontAsset)");
            w.WriteLine();
            var seen = new System.Collections.Generic.HashSet<string>();
            var texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(includeInactive: true);
            foreach (var t in texts)
            {
                if (t == null || t.font == null) continue;
                var fname = t.font.name ?? "<unnamed>";
                if (seen.Add(fname))
                {
                    w.WriteLine($"  - {fname}  (first seen on '{t.gameObject.name}', size={t.fontSize}, style={t.fontStyle})");
                }
            }
            w.WriteLine();
        }

        private static void DumpCanvases(StreamWriter w)
        {
            w.WriteLine("## Canvases");
            w.WriteLine();
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(includeInactive: false);
            foreach (var c in canvases)
            {
                if (c == null) continue;
                w.WriteLine($"  - '{c.gameObject.name}'  renderMode={c.renderMode} sortingOrder={c.sortingOrder} active={c.gameObject.activeInHierarchy}");
            }
            w.WriteLine();
        }

        /// <summary>Every raycast-blocking Graphic in the scene — INCLUDING spriteless
        /// ones (the usual invisible click-eaters). For each, records raycastTarget,
        /// whether it has a sprite, its rect/world-corners, sibling index (higher =
        /// drawn/hit later = wins the click), any pointer handlers, and enclosing
        /// CanvasGroup.blocksRaycasts. Use to find what's intercepting clicks over a
        /// given screen region (e.g. a save-grid cell).</summary>
        private static void DumpRaycastTargets(StreamWriter w)
        {
            w.WriteLine("## Raycast targets (Graphics with raycastTarget=true, incl. spriteless)");
            w.WriteLine("  format: <path> | sprite=<name|NONE> rect=<wxh> screenX=[min..max] screenY=[min..max] sibIdx=<n> [handlers] [CanvasGroup.blocksRaycasts]");
            w.WriteLine();
            var graphics = UnityEngine.Object.FindObjectsOfType<Graphic>(includeInactive: false);
            var sorted = graphics.Where(g => g != null && g.raycastTarget)
                                 .OrderBy(g => HierarchyPath(g.gameObject))
                                 .ToArray();
            var corners = new Vector3[4];
            int n = 0;
            foreach (var g in sorted)
            {
                var img = g as Image;
                string sprite = img != null ? (img.sprite != null ? $"'{img.sprite.name}'" : "NONE") : $"({g.GetType().Name})";
                var rt = g.rectTransform;
                string rect = rt != null ? $"{rt.rect.width:F0}x{rt.rect.height:F0}" : "?";
                string sx = "?", sy = "?";
                if (rt != null)
                {
                    rt.GetWorldCorners(corners);
                    // ScreenSpaceOverlay: world corners are already screen pixels.
                    float xmin = Mathf.Min(corners[0].x, corners[2].x), xmax = Mathf.Max(corners[0].x, corners[2].x);
                    float ymin = Mathf.Min(corners[0].y, corners[2].y), ymax = Mathf.Max(corners[0].y, corners[2].y);
                    sx = $"[{xmin:F0}..{xmax:F0}]"; sy = $"[{ymin:F0}..{ymax:F0}]";
                }
                string handlers = "";
                if (g.GetComponent<Button>() != null) handlers += " Button";
                if (g.GetComponent<Toggle>() != null) handlers += " Toggle";
                if (g.GetComponent<ScrollRect>() != null) handlers += " ScrollRect";
                if (g.GetComponent<UnityEngine.EventSystems.EventTrigger>() != null) handlers += " EventTrigger";
                // Any custom pointer handler (IPointerClickHandler/DownHandler/etc.)
                foreach (var mb in g.GetComponents<MonoBehaviour>())
                {
                    if (mb == null) continue;
                    if (mb is UnityEngine.EventSystems.IPointerClickHandler || mb is UnityEngine.EventSystems.IPointerDownHandler
                        || mb is UnityEngine.EventSystems.IBeginDragHandler || mb is UnityEngine.EventSystems.IDragHandler)
                    {
                        var tn = mb.GetType().Name;
                        if (tn != "Button" && tn != "Toggle" && tn != "ScrollRect" && tn != "Scrollbar")
                            handlers += $" {tn}";
                    }
                }
                var cg = g.GetComponentInParent<CanvasGroup>();
                string blocks = (cg != null && cg.blocksRaycasts) ? $" CanvasGroup.blocksRaycasts('{cg.gameObject.name}')" : "";
                w.WriteLine($"  {HierarchyPath(g.gameObject)} | sprite={sprite} rect={rect} screenX={sx} screenY={sy} sibIdx={g.transform.GetSiblingIndex()}{handlers}{blocks}");
                if (++n > 1200) { w.WriteLine("  ... (truncated at 1200)"); break; }
            }
            w.WriteLine();
            w.WriteLine($"  total: {n} raycast-blocking graphics");
            w.WriteLine();
        }

        private static void DumpSprites(StreamWriter w)
        {
            w.WriteLine("## Image components (active in scene)");
            w.WriteLine("  format: <hierarchy-path> | sprite='<sprite-name>' type=<simple|sliced|tiled|filled> color=<rgba> rect=<wxh> [Sliced border=L,B,R,T]");
            w.WriteLine();
            var images = UnityEngine.Object.FindObjectsOfType<Image>(includeInactive: false);
            // Sort by hierarchy path for readability
            var sorted = images.Where(i => i != null && i.sprite != null)
                               .OrderBy(i => HierarchyPath(i.gameObject))
                               .ToArray();
            int n = 0;
            foreach (var img in sorted)
            {
                var sp = img.sprite;
                var rect = img.rectTransform != null
                    ? $"{img.rectTransform.rect.width:F0}x{img.rectTransform.rect.height:F0}"
                    : "?";
                var border = (img.type == Image.Type.Sliced && sp != null)
                    ? $" border={sp.border.x:F0},{sp.border.y:F0},{sp.border.z:F0},{sp.border.w:F0}"
                    : "";
                w.WriteLine($"  {HierarchyPath(img.gameObject)} | sprite='{sp.name}' type={img.type} color={ColorHex(img.color)} rect={rect}{border}");
                n++;
                if (n > 800) { w.WriteLine("  ... (truncated at 800 images)"); break; }
            }
            w.WriteLine();
            w.WriteLine($"  total: {n} Image components with sprites");
            w.WriteLine();
        }

        private static void DumpControls(StreamWriter w)
        {
            w.WriteLine("## Buttons (active)");
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (b == null) continue;
                var img = b.targetGraphic as Image;
                string spr = img?.sprite?.name ?? "<no sprite>";
                w.WriteLine($"  {HierarchyPath(b.gameObject)} | targetSprite='{spr}'");
            }
            w.WriteLine();

            w.WriteLine("## Toggles (active)");
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Toggle>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (t == null) continue;
                var bg = t.targetGraphic as Image;
                var ck = t.graphic as Image;
                w.WriteLine($"  {HierarchyPath(t.gameObject)} | bg='{bg?.sprite?.name ?? "?"}' check='{ck?.sprite?.name ?? "?"}'");
            }
            w.WriteLine();

            w.WriteLine("## Sliders (active)");
            foreach (var s in UnityEngine.Object.FindObjectsOfType<Slider>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (s == null) continue;
                var fill = s.fillRect?.GetComponent<Image>();
                var handle = s.handleRect?.GetComponent<Image>();
                w.WriteLine($"  {HierarchyPath(s.gameObject)} | fill='{fill?.sprite?.name ?? "?"}' handle='{handle?.sprite?.name ?? "?"}'");
            }
            w.WriteLine();

            w.WriteLine("## Scrollbars (active)");
            foreach (var sb in UnityEngine.Object.FindObjectsOfType<Scrollbar>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (sb == null) continue;
                var handleImg = sb.handleRect?.GetComponent<Image>();
                w.WriteLine($"  {HierarchyPath(sb.gameObject)} | handle='{handleImg?.sprite?.name ?? "?"}'");
            }
            w.WriteLine();

            w.WriteLine("## ScrollRects (active)");
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<ScrollRect>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (sr == null) continue;
                w.WriteLine($"  {HierarchyPath(sr.gameObject)} | viewport='{sr.viewport?.name ?? "?"}' content='{sr.content?.name ?? "?"}'");
            }
            w.WriteLine();

            w.WriteLine("## TMP_InputFields (active) — full hierarchy");
            foreach (var inp in UnityEngine.Object.FindObjectsOfType<TMP_InputField>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (inp == null) continue;
                // Wrap per-field: some TMP_InputField property getters
                // (notably caretColor) throw NRE when the caret material isn't
                // initialized. Don't let one bad field abort the whole dump.
                try
                {
                w.WriteLine($"  {HierarchyPath(inp.gameObject)}");
                w.WriteLine($"    text='{inp.text}' lineType={inp.lineType} contentType={inp.contentType}");
                w.WriteLine($"    textComponent='{(inp.textComponent != null ? HierarchyPath(inp.textComponent.gameObject) : "<null>")}'");
                w.WriteLine($"    textViewport='{(inp.textViewport != null ? HierarchyPath(inp.textViewport.gameObject) : "<null>")}'");
                w.WriteLine($"    placeholder='{(inp.placeholder != null ? HierarchyPath(inp.placeholder.gameObject) : "<null>")}'");
                var tg = inp.targetGraphic;
                w.WriteLine($"    targetGraphic='{(tg != null ? HierarchyPath(tg.gameObject) : "<null>")}'");
                string caretColor = "?", selColor = "?";
                try { caretColor = ColorHex(inp.caretColor); } catch { caretColor = "<throws>"; }
                try { selColor = ColorHex(inp.selectionColor); } catch { selColor = "<throws>"; }
                w.WriteLine($"    customCaretColor={inp.customCaretColor} caretColor={caretColor} caretWidth={inp.caretWidth} caretBlinkRate={inp.caretBlinkRate}");
                w.WriteLine($"    selectionColor={selColor}");
                w.WriteLine($"    fontAsset={(inp.fontAsset != null ? inp.fontAsset.name : "<null>")} pointSize={inp.pointSize}");
                w.WriteLine($"    shouldHideMobileInput={inp.shouldHideMobileInput} restoreOriginalTextOnEscape={inp.restoreOriginalTextOnEscape}");
                // Child tree (skip Caret which Unity creates lazily)
                w.WriteLine($"    children:");
                for (int i = 0; i < inp.transform.childCount; i++)
                {
                    var c = inp.transform.GetChild(i);
                    var img = c.GetComponent<Image>();
                    var txt = c.GetComponent<TMP_Text>();
                    var mask2d = c.GetComponent<UnityEngine.UI.RectMask2D>();
                    var mask = c.GetComponent<UnityEngine.UI.Mask>();
                    string detail = "";
                    if (img != null) detail += $" Image(sprite='{img.sprite?.name ?? "null"}' type={img.type})";
                    if (txt != null) detail += $" {txt.GetType().Name}(font='{txt.font?.name}' size={txt.fontSize} style={txt.fontStyle} color={ColorHex(txt.color)} text='{txt.text}')";
                    if (mask2d != null) detail += " RectMask2D";
                    if (mask != null) detail += " Mask";
                    w.WriteLine($"      [{i}] '{c.name}'{detail}");
                    // One level deeper
                    for (int j = 0; j < c.childCount; j++)
                    {
                        var cc = c.GetChild(j);
                        var cimg = cc.GetComponent<Image>();
                        var ctxt = cc.GetComponent<TMP_Text>();
                        string cdetail = "";
                        if (cimg != null) cdetail += $" Image(sprite='{cimg.sprite?.name ?? "null"}')";
                        if (ctxt != null) cdetail += $" {ctxt.GetType().Name}";
                        w.WriteLine($"        [{i}.{j}] '{cc.name}'{cdetail}");
                    }
                }
                }
                catch (Exception ex)
                {
                    w.WriteLine($"    <dump error: {ex.GetType().Name}: {ex.Message}>");
                }
            }
            w.WriteLine();

            w.WriteLine("## TMP_Dropdowns (active)");
            foreach (var d in UnityEngine.Object.FindObjectsOfType<TMP_Dropdown>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (d == null) continue;
                var bg = d.GetComponent<Image>();
                w.WriteLine($"  {HierarchyPath(d.gameObject)} | bg='{bg?.sprite?.name ?? "?"}' templateOK={d.template != null}");
            }
            w.WriteLine();

            w.WriteLine("## Texts (active, sample of first 50)");
            int n = 0;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>(includeInactive: false).OrderBy(x => HierarchyPath(x.gameObject)))
            {
                if (t == null) continue;
                w.WriteLine($"  {HierarchyPath(t.gameObject)} | font='{t.font?.name ?? "?"}' size={t.fontSize} style={t.fontStyle} color={ColorHex(t.color)}");
                if (++n >= 50) break;
            }
            w.WriteLine();
        }

        private static string HierarchyPath(GameObject go)
        {
            if (go == null) return "<null>";
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            int depth = 0;
            while (t != null && depth < 12)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
                depth++;
            }
            return sb.ToString();
        }

        private static string ColorHex(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255), 0, 255);
            return $"#{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}
