using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Per-instance colour variety for buildings. Each structure gets a subtle,
    /// deterministic tint (from its world position, so it's stable across reloads
    /// with nothing saved) applied via a MaterialPropertyBlock on its solid mesh
    /// renderers. No assets added, no shared material touched.
    ///
    /// Perf: we use FF's existing building list (resourceManager.allBuildingsRO)
    /// — NOT FindObjectsOfType — and cache each building's renderers + seed once.
    /// Changing the Intensity slider only re-colours cached renderers (cheap);
    /// it does NOT re-scan. New buildings are picked up on a throttled pass with
    /// a per-tick budget so there's no hitch.
    /// </summary>
    internal static class BuildingVariety
    {
        // Warm-only weathered tint. Hue clamped to terracotta→straw (no pure red,
        // no yellow-green); gentle saturation so it reads as aged wood, not paint.
        // Calibrated from the full-range pass.
        // Hue stays in the safe warm band (no green/red outliers). Saturation and
        // brightness scale with the Intensity slider all the way up to a deliberate
        // "too much" ceiling — so players can crank it to see the extreme, then dial
        // back to taste. The default intensity sits comfortably in the middle.
        private const float HueWarmMin = 0.03f;     // red-orange / terracotta
        private const float HueWarmMax = 0.14f;     // warm straw (before yellow-green)
        private const float SatMin = 0.10f;
        private const float SatMax = 0.45f;         // intensity 1.0 = intentionally too much
        private const float MaxValueDrop = 0.30f;   // brightness down up to 30%

        private const int NewPerScan = 40;          // cap new-building work per scan tick

        private sealed class Entry
        {
            public Building building = null!;
            public Renderer[] renderers = null!;
            public int seed;
        }

        private static readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static float _lastIntensity = -1f;
        private static bool _wasEnabled;
        private static float _scanTimer;

        public static void OnSceneChanged()
        {
            _entries.Clear();
            _lastIntensity = -1f;
            _wasEnabled = false;
            _scanTimer = 0f;
        }

        public static void Tick()
        {
            bool on = FFUIOverhaulMod.EnableBuildingVariety.Value;

            if (!on)
            {
                if (_wasEnabled) { ClearAll(); _wasEnabled = false; }
                return;
            }
            if (!GameManager.gameReadyToPlay) return;
            var gm = UnitySingleton<GameManager>.Instance;
            var rm = gm != null ? gm.resourceManager : null;
            if (rm == null || rm.allBuildingsRO == null) return;

            float intensity = Mathf.Clamp01(FFUIOverhaulMod.BuildingVarietyIntensity.Value);

            // Slider moved → recolour cached renderers only (no scan, no alloc).
            if (!Mathf.Approximately(intensity, _lastIntensity))
            {
                RecolorAll(intensity);
                _lastIntensity = intensity;
            }
            _wasEnabled = true;

            // Throttled pass to catch newly-built structures (budgeted).
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer > 0f) return;
            _scanTimer = 1f;

            ScanForNew(rm, intensity);
        }

        private static void ScanForNew(ResourceManager rm, float intensity)
        {
            var list = rm.allBuildingsRO;
            int budget = NewPerScan;
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                if (b == null) continue;
                int id = b.GetInstanceID();
                if (_entries.ContainsKey(id)) continue;

                // Excluded landmarks / infrastructure: cache an empty entry so we
                // skip them cheaply next scan (no GetComponentsInChildren cost).
                if (IsExcluded(b))
                {
                    _entries[id] = new Entry { building = b, renderers = new Renderer[0], seed = 0 };
                    continue;
                }

                var rends = CollectStructuralRenderers(b);
                var e = new Entry { building = b, renderers = rends, seed = Seed(b.transform.position) };
                _entries[id] = e;
                ApplyEntry(e, intensity);

                if (--budget <= 0) break;
            }
        }

        /// <summary>Landmarks and infrastructure that should keep their canonical
        /// look: Town Center, Temple, Academy, Trading Post, Guild Hall, the three
        /// Monuments, walls, gates, and decorations/fences (DecorativeBuilding).
        /// Roads aren't Buildings, so they're never in the list to begin with.</summary>
        private static bool IsExcluded(Building b)
        {
            return b is TownCenter
                || b is Temple
                || b is Academy
                || b is TradingPost
                || b is GuildHall
                || b is CivicMonument
                || b is EconomicMonument
                || b is MilitaryMonument
                || b is Wall
                || b is Gate
                || b is DecorativeBuilding;
        }

        private static Renderer[] CollectStructuralRenderers(Building b)
        {
            var all = b.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            var keep = new List<Renderer>(2);
            for (int i = 0; i < all.Length; i++)
            {
                var r = all[i];
                if (r == null) continue;
                var sh = r.sharedMaterial != null ? r.sharedMaterial.shader : null;
                if (sh != null && sh.name.StartsWith("Standard")) keep.Add(r); // solid building meshes only
            }
            return keep.ToArray();
        }

        private static void RecolorAll(float intensity)
        {
            foreach (var e in _entries.Values) ApplyEntry(e, intensity);
        }

        private static void ApplyEntry(Entry e, float intensity)
        {
            Color tint = ComputeTint(e.seed, intensity);
            var rends = e.renderers;
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(ColorId, tint);
                r.SetPropertyBlock(_mpb);
            }
        }

        private static void ClearAll()
        {
            foreach (var e in _entries.Values)
            {
                var rends = e.renderers;
                for (int i = 0; i < rends.Length; i++)
                {
                    var r = rends[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(ColorId, Color.white); // white = identity for a multiply
                    r.SetPropertyBlock(_mpb);
                }
            }
            _entries.Clear();
        }

        private static Color ComputeTint(int seed, float intensity)
        {
            if (intensity <= 0f) return Color.white;
            float r1 = Rand01(seed, 1);
            float r2 = Rand01(seed, 2);
            float r3 = Rand01(seed, 3);

            float h = Mathf.Lerp(HueWarmMin, HueWarmMax, r3);          // warm band
            float s = intensity * Mathf.Lerp(SatMin, SatMax, r2);      // gentle warm cast
            float v = 1f - intensity * MaxValueDrop * r1;              // brightness spread
            var c = Color.HSVToRGB(h, s, v);
            c.a = 1f;
            return c;
        }

        private static int Seed(Vector3 p)
        {
            int x = Mathf.RoundToInt(p.x * 4f);
            int z = Mathf.RoundToInt(p.z * 4f);
            unchecked { int h = 17; h = h * 31 + x; h = h * 31 + z; return h; }
        }

        private static float Rand01(int seed, int salt)
        {
            unchecked
            {
                uint n = (uint)(seed * 73856093 ^ salt * 19349663);
                n = (n ^ (n >> 13)) * 1274126177u;
                n ^= n >> 16;
                return (n & 0xFFFFFFu) / (float)0x1000000;
            }
        }
    }
}
