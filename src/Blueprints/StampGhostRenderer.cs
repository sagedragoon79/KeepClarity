using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Translucent building meshes for the stamp preview — you see the actual
    /// buildings you're about to place, tinted green where they'll go down and
    /// red where they won't.
    ///
    /// INSTANTIATING A BUILDING PREFAB SAFELY is the whole problem. The built
    /// prefab is a live building: instantiate it normally and its Awake/Start run,
    /// registering it with FF's managers and generally pretending to be a real
    /// building in your town. The fix is Unity's own rule — components in an
    /// INACTIVE hierarchy never get Awake — so ghosts are instantiated under a
    /// deactivated holder, stripped of every MonoBehaviour and Collider, and only
    /// then made visible. Nothing but MeshFilter/MeshRenderer survives.
    ///
    /// Ghosts are built once when the stamp arms and then just moved each frame;
    /// rebuilding per frame would be far too expensive for a 40-building layout.
    ///
    /// If any of this fails — a missing prefab, no usable transparent shader —
    /// it reports once and the caller keeps drawing footprint outlines, which
    /// convey the same information less prettily.
    /// </summary>
    internal class StampGhostRenderer
    {
        private static readonly Color OkTint = new Color(0.35f, 1f, 0.45f, 0.45f);
        private static readonly Color BlockedTint = new Color(1f, 0.3f, 0.25f, 0.45f);

        private GameObject? _root;
        private readonly List<GhostInstance> _ghosts = new List<GhostInstance>();
        private Material? _ghostMat;
        private bool _failed;
        private Blueprint? _builtFor;

        private class GhostInstance
        {
            public GameObject Go = null!;
            public readonly List<Material> Materials = new List<Material>();
            public bool LastOk = true;
        }

        public bool Failed => _failed;
        public int Count => _ghosts.Count;

        /// <summary>True when ghosts exist for this exact blueprint.</summary>
        public bool IsBuiltFor(Blueprint bp) => !_failed && ReferenceEquals(_builtFor, bp) && _ghosts.Count > 0;

        // ── build ───────────────────────────────────────────────────────────

        /// <summary>Create one ghost per entry. Safe to call repeatedly; rebuilds
        /// only when the blueprint changes.</summary>
        public void Build(Blueprint bp)
        {
            if (_failed || bp == null) return;
            if (IsBuiltFor(bp)) return;

            Clear();
            try
            {
                _ghostMat = EnsureGhostMaterial();
                if (_ghostMat == null)
                {
                    Fail("no usable transparent shader");
                    return;
                }

                _root = new GameObject("FFUI_BlueprintGhosts");
                UnityEngine.Object.DontDestroyOnLoad(_root);

                // The holder stays INACTIVE while prefabs are instantiated into it,
                // so none of the buildings' own scripts ever wake up.
                var staging = new GameObject("Staging");
                staging.transform.SetParent(_root.transform, false);
                staging.SetActive(false);

                foreach (var e in bp.entries)
                {
                    var ghost = BuildOne(e, staging.transform);
                    if (ghost != null) _ghosts.Add(ghost);
                }

                if (_ghosts.Count == 0) { Fail("no building meshes resolved"); return; }

                // Re-parent the stripped, now-inert ghosts into the live root.
                foreach (var g in _ghosts)
                {
                    g.Go.transform.SetParent(_root.transform, false);
                    g.Go.SetActive(true);
                }
                UnityEngine.Object.Destroy(staging);

                _builtFor = bp;
            }
            catch (Exception e)
            {
                Fail(e.Message);
            }
        }

        private GhostInstance? BuildOne(BlueprintEntry entry, Transform staging)
        {
            var bd = GlobalAssets.buildingSetupData?.GetBuildingData(entry.id);
            var prefab = bd?.GetRandomPrefabEntry()?.PREFAB();
            if (prefab == null) return null;

            // Instantiated into an inactive parent: Awake/Start never run.
            var go = UnityEngine.Object.Instantiate(prefab, staging);
            go.name = "Ghost_" + entry.id;

            StripToVisuals(go);

            var inst = new GhostInstance { Go = go };
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                // One material instance per renderer so tints are independent.
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = new Material(_ghostMat!);
                    mats[i] = m;
                    inst.Materials.Add(m);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return inst.Materials.Count > 0 ? inst : null;
        }

        /// <summary>
        /// Reduce a building prefab to pure visuals: keep the transform and the
        /// mesh components, destroy everything else.
        ///
        /// Allow-list rather than naming types to remove — partly because it is
        /// exhaustive (a building prefab carries colliders, particles, audio,
        /// animators and FF's own scripts, and missing one means a ghost that
        /// behaves), and partly because naming those types would drag four more
        /// UnityEngine module references into Keep Clarity for no other purpose.
        ///
        /// Each destroy is guarded: RequireComponent dependencies can throw when
        /// removed in the wrong order, and a ghost with one stubborn component is
        /// still a fine ghost.
        /// </summary>
        private static void StripToVisuals(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(includeInactive: true))
            {
                if (c == null) continue;
                if (c is Transform) continue;
                if (c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }
        }

        /// <summary>A transparent unlit material. Tries the shaders most likely to
        /// exist in this build before giving up.</summary>
        private static Material? EnsureGhostMaterial()
        {
            foreach (var name in new[]
            {
                "Legacy Shaders/Transparent/Diffuse",
                "Unlit/Transparent",
                "Sprites/Default",
                "UI/Default",
            })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                var m = new Material(sh);
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                return m;
            }
            return null;
        }

        // ── per-frame ───────────────────────────────────────────────────────

        /// <summary>Place ghost <paramref name="index"/> and colour it.</summary>
        public void Place(int index, Vector3 worldPos, float yaw, bool ok)
        {
            if (_failed || index < 0 || index >= _ghosts.Count) return;
            var g = _ghosts[index];
            if (g.Go == null) return;

            g.Go.transform.position = worldPos;
            g.Go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (g.LastOk != ok || !g.Go.activeSelf)
            {
                g.LastOk = ok;
                var tint = ok ? OkTint : BlockedTint;
                foreach (var m in g.Materials)
                {
                    if (m == null) continue;
                    try { m.color = tint; } catch { }
                }
            }
            if (!g.Go.activeSelf) g.Go.SetActive(true);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
        }

        public void Clear()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _ghosts.Clear();
            _builtFor = null;
        }

        private void Fail(string why)
        {
            _failed = true;
            Clear();
            FFUIOverhaulMod.Log.Warning(
                $"[Blueprints] mesh ghosts unavailable ({why}) — using footprint outlines instead.");
        }
    }
}
