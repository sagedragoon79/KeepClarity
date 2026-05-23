using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// One-shot sprite exporter — dumps every loaded UI sprite to PNG plus a
    /// self-contained <c>index.html</c> gallery, so FF's UI atlas can be browsed
    /// offline instead of guess-build-deploy-squint. Triggered via Ctrl+Shift+F10.
    ///
    /// Why a RenderTexture blit: most FF textures are imported non-readable, so
    /// Texture2D.GetPixels/EncodeToPNG throws. Blitting to a temporary
    /// RenderTexture then ReadPixels into a fresh readable Texture2D sidesteps
    /// the import flag entirely. The full readable texture is cached per source
    /// so atlas-packed sprites don't re-blit the same sheet repeatedly.
    ///
    /// Output: <c>MelonLoader/KC_SpriteExport_&lt;ts&gt;/</c>
    ///   ├── index.html        (gallery; manifest embedded inline)
    ///   └── sprites/*.png      (one per sprite, cropped to its atlas rect)
    /// </summary>
    public static class SpriteExport
    {
        public static void Run()
        {
            try
            {
                string root = Path.Combine("MelonLoader", $"KC_SpriteExport_{DateTime.Now:yyyyMMdd_HHmmss}");
                string spritesDir = Path.Combine(root, "sprites");
                Directory.CreateDirectory(spritesDir);

                // FindObjectsOfTypeAll grabs every loaded Sprite (incl. unused /
                // not-currently-on-screen), giving far better coverage than only
                // walking active Image components.
                var all = Resources.FindObjectsOfTypeAll<Sprite>();
                var readableCache = new Dictionary<Texture2D, Texture2D?>();
                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seen = new HashSet<int>();
                var entries = new List<Entry>();

                int exported = 0, failed = 0;
                foreach (var sp in all)
                {
                    if (sp == null || sp.texture == null) continue;
                    if (!seen.Add(sp.GetInstanceID())) continue;

                    // Skip obvious non-UI noise: keep everything, but cap total
                    // so a runaway scene can't write tens of thousands of files.
                    if (exported >= 4000) break;

                    try
                    {
                        var cropped = CropSprite(sp, readableCache);
                        if (cropped == null) { failed++; continue; }

                        var png = ImageConversion.EncodeToPNG(cropped);
                        UnityEngine.Object.Destroy(cropped);
                        if (png == null || png.Length == 0) { failed++; continue; }

                        string file = SafeFileName(sp.name, usedNames) + ".png";
                        File.WriteAllBytes(Path.Combine(spritesDir, file), png);

                        var b = sp.border; // x=left, y=bottom, z=right, w=top
                        entries.Add(new Entry
                        {
                            Name = sp.name,
                            File = file,
                            W = Mathf.RoundToInt(sp.rect.width),
                            H = Mathf.RoundToInt(sp.rect.height),
                            BL = Mathf.RoundToInt(b.x),
                            BB = Mathf.RoundToInt(b.y),
                            BR = Mathf.RoundToInt(b.z),
                            BT = Mathf.RoundToInt(b.w),
                        });
                        exported++;
                    }
                    catch { failed++; }
                }

                // Release cached readable copies.
                foreach (var t in readableCache.Values)
                    if (t != null) UnityEngine.Object.Destroy(t);

                File.WriteAllText(Path.Combine(root, "index.html"), BuildHtml(entries), Encoding.UTF8);

                FFUIOverhaulMod.Log.Msg($"[SpriteExport] wrote {exported} sprites ({failed} failed) → {Path.GetFullPath(root)}");
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Error($"[SpriteExport] failed: {ex}");
            }
        }

        private sealed class Entry
        {
            public string Name = "";
            public string File = "";
            public int W, H, BL, BB, BR, BT;
        }

        /// <summary>Returns a readable Texture2D cropped to the sprite's atlas
        /// rect, or null if the source can't be read.</summary>
        private static Texture2D? CropSprite(Sprite sp, Dictionary<Texture2D, Texture2D?> cache)
        {
            var src = sp.texture;
            if (!cache.TryGetValue(src, out var readable))
            {
                readable = ToReadable(src);
                cache[src] = readable;
            }
            if (readable == null) return null;

            // textureRect is the sprite's pixel rect within the (possibly atlas)
            // texture. Clamp to bounds — packed/rotated edge cases can over-run.
            var r = sp.textureRect;
            int x = Mathf.Clamp(Mathf.RoundToInt(r.x), 0, readable.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(r.y), 0, readable.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(r.width), 1, readable.width - x);
            int h = Mathf.Clamp(Mathf.RoundToInt(r.height), 1, readable.height - y);

            var px = readable.GetPixels(x, y, w, h);
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels(px);
            outTex.Apply();
            return outTex;
        }

        private static Texture2D? ToReadable(Texture2D src)
        {
            try
            {
                var rt = RenderTexture.GetTemporary(src.width, src.height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                var prevActive = RenderTexture.active;
                Graphics.Blit(src, rt);
                RenderTexture.active = rt;
                var readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
                readable.Apply();
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
                return readable;
            }
            catch { return null; }
        }

        private static string SafeFileName(string name, HashSet<string> used)
        {
            var sb = new StringBuilder(name.Length);
            foreach (var ch in name)
                sb.Append(char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_' ? ch : '_');
            string baseName = sb.Length > 0 ? sb.ToString() : "sprite";
            string candidate = baseName;
            int i = 1;
            while (!used.Add(candidate)) candidate = $"{baseName}_{i++}";
            return candidate;
        }

        // ── HTML gallery ────────────────────────────────────────────────────

        private static string BuildHtml(List<Entry> entries)
        {
            // Inline JS manifest so the page works under file:// (no fetch/CORS).
            var sb = new StringBuilder();
            sb.Append("const SPRITES=[");
            foreach (var e in entries.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                sb.Append('{')
                  .Append("n:").Append(JsStr(e.Name)).Append(',')
                  .Append("f:").Append(JsStr(e.File)).Append(',')
                  .Append("w:").Append(e.W).Append(',')
                  .Append("h:").Append(e.H).Append(',')
                  .Append("b:[").Append(e.BL).Append(',').Append(e.BB).Append(',')
                  .Append(e.BR).Append(',').Append(e.BT).Append(']')
                  .Append("},");
            }
            sb.Append("];");
            string manifest = sb.ToString();

            return @"<!DOCTYPE html><html lang=""en""><head><meta charset=""utf-8"">
<title>Keep Clarity — FF Sprite Gallery</title>
<style>
  :root{--bg:#1b1712;--panel:#262019;--ink:#e8e0d0;--dim:#a89878;--accent:#d4a032;}
  *{box-sizing:border-box}
  body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.4 system-ui,Segoe UI,sans-serif}
  header{position:sticky;top:0;background:var(--panel);padding:12px 16px;border-bottom:1px solid #000;
         display:flex;gap:12px;align-items:center;flex-wrap:wrap;z-index:10}
  h1{font-size:16px;margin:0;color:var(--accent);font-weight:700}
  input{background:#15110c;border:1px solid #4a3d28;color:var(--ink);padding:7px 10px;border-radius:4px;min-width:240px}
  label{color:var(--dim);font-size:12px;display:flex;gap:6px;align-items:center}
  #count{color:var(--dim);font-size:12px}
  #grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:10px;padding:16px}
  .card{background:var(--panel);border:1px solid #3a3024;border-radius:6px;padding:8px;display:flex;
        flex-direction:column;gap:6px;cursor:pointer}
  .card:hover{border-color:var(--accent)}
  .thumb{height:96px;display:flex;align-items:center;justify-content:center;border-radius:4px;
    background-image:linear-gradient(45deg,#3a3a3a 25%,transparent 25%),linear-gradient(-45deg,#3a3a3a 25%,transparent 25%),
    linear-gradient(45deg,transparent 75%,#3a3a3a 75%),linear-gradient(-45deg,transparent 75%,#3a3a3a 75%);
    background-size:16px 16px;background-position:0 0,0 8px,8px -8px,-8px 0;overflow:hidden}
  .thumb img{max-width:100%;max-height:96px;image-rendering:pixelated}
  .nm{font-size:11px;word-break:break-all;color:var(--ink)}
  .meta{font-size:10px;color:var(--dim)}
  .b9{color:var(--accent)}
</style></head><body>
<header>
  <h1>FF Sprite Gallery</h1>
  <input id=""q"" placeholder=""filter by name… (e.g. Border, BTN, ICN, Header)"" autofocus>
  <label><input type=""checkbox"" id=""only9""> 9-slice only</label>
  <span id=""count""></span>
</header>
<div id=""grid""></div>
<script>
" + manifest + @"
const grid=document.getElementById('grid'),q=document.getElementById('q'),
      only9=document.getElementById('only9'),count=document.getElementById('count');
function render(){
  const term=q.value.trim().toLowerCase(), o9=only9.checked;
  grid.innerHTML='';
  let n=0;
  for(const s of SPRITES){
    if(term && !s.n.toLowerCase().includes(term)) continue;
    const has9=s.b[0]||s.b[1]||s.b[2]||s.b[3];
    if(o9 && !has9) continue;
    n++;
    const c=document.createElement('div'); c.className='card';
    c.title='click to copy name';
    c.onclick=()=>navigator.clipboard&&navigator.clipboard.writeText(s.n);
    const t=document.createElement('div'); t.className='thumb';
    const img=document.createElement('img'); img.loading='lazy'; img.src='sprites/'+s.f; t.appendChild(img);
    const nm=document.createElement('div'); nm.className='nm'; nm.textContent=s.n;
    const meta=document.createElement('div'); meta.className='meta';
    meta.innerHTML=s.w+'×'+s.h+(has9?(' · <span class=b9>9-slice '+s.b.join(',')+'</span>'):'');
    c.appendChild(t); c.appendChild(nm); c.appendChild(meta); grid.appendChild(c);
  }
  count.textContent=n+' / '+SPRITES.length+' sprites';
}
q.addEventListener('input',render); only9.addEventListener('change',render); render();
</script></body></html>";
        }

        private static string JsStr(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (var ch in s)
            {
                if (ch == '"' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch == '\n') sb.Append("\\n");
                else if (ch == '\r') { }
                else sb.Append(ch);
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
