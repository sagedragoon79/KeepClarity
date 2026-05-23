using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Severity-tints the predator-alert bar (<c>UIBlurb</c>) by threat level
    /// and keeps it LIVE — a boar/fox chased away (now retreating) flips red →
    /// amber, since the threat is over and the player needn't act fast.
    ///
    /// Self-contained (no dependency on WotW's clone): we detect a predator
    /// blurb by its container TYPE (<c>PredatorCombatBlurbContainer</c>) — which
    /// survives FF's consolidation logic that resets the blurbDefinition back to
    /// the vanilla sprite (that reset was what made the bar intermittently go
    /// black). Severity is read each tick from the live <c>enemyUnitsInCombat</c>
    /// list (DamageableComponents with <c>isRetreating</c>) plus the container's
    /// <c>blurbType</c> (sighted vs engaging):
    ///   Wolf / Bear            → always red.
    ///   Fox / Boar, fleeing    → amber  (chased away / retreating).
    ///   Fox / Boar, sighted    → amber  (spotted, not yet a real threat).
    ///   Fox / Boar, engaging   → red.
    ///
    /// Recolors the bar's "Standard" (fill) + "OnHover" (border) children — the
    /// light-tintable IMG_BorderSimpleThick* sprites identified via a live
    /// UIDump. WotW still owns the round stacked-icon + per-animal icon sprite.
    /// </summary>
    [HarmonyPatch(typeof(UIBlurb), nameof(UIBlurb.Init))]
    internal static class PredatorBlurbBackground
    {
        private const string PredatorContainer = "PredatorCombatBlurbContainer";

        private static readonly Color Amber = new(0.72f, 0.45f, 0.07f, 1f);
        private static readonly Color Red = new(0.62f, 0.13f, 0.11f, 1f);

        private static readonly string[] AnimalKinds = { "Wolf", "Bear", "Fox", "Boar" };

        private static readonly Dictionary<int, Color> _original = new();
        private static readonly List<UIBlurb> _tracked = new();

        private static readonly BindingFlags All =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // ── Init ────────────────────────────────────────────────────────────

        [HarmonyPostfix]
        private static void InitPostfix(UIBlurb __instance)
        {
            try
            {
                Apply(__instance);
                if (IsPredator(__instance) && !_tracked.Contains(__instance))
                    _tracked.Add(__instance);
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[PredatorBlurbBg] init: {e.Message}"); }
        }

        // ── Live tick (from Plugin.OnUpdate) ────────────────────────────────

        private static float _timer;

        public static void Tick(float dt)
        {
            if (_tracked.Count == 0) return;
            _timer += dt;
            if (_timer < 0.25f) return;
            _timer = 0f;

            try
            {
                for (int i = _tracked.Count - 1; i >= 0; i--)
                {
                    var b = _tracked[i];
                    if (b == null) { _tracked.RemoveAt(i); continue; }
                    Apply(b);
                }
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[PredatorBlurbBg] tick: {e.Message}"); }
        }

        // ── Core ────────────────────────────────────────────────────────────

        private static bool IsPredator(UIBlurb blurb)
        {
            var c = blurb.blurbContainer;
            return c != null && c.GetType().Name == PredatorContainer;
        }

        private static void Apply(UIBlurb blurb)
        {
            var fill = FindChild(blurb.transform, "Standard");
            var border = FindChild(blurb.transform, "OnHover");
            if (fill == null && border == null) return;

            if (IsPredator(blurb))
            {
                bool red = ClassifyRed(blurb.blurbContainer!);
                Recolor(fill, red ? Red : Amber);
                Recolor(border, red ? Red : Amber);
            }
            else
            {
                RestoreOriginal(fill);
                RestoreOriginal(border);
            }
        }

        /// <summary>Live severity from the container's enemies + blurbType.</summary>
        private static bool ClassifyRed(object container)
        {
            string blurbType = ReadMember(container, "blurbType")?.ToString() ?? "";
            bool sighted = blurbType.IndexOf("Sighted", StringComparison.OrdinalIgnoreCase) >= 0;

            bool anyWolfBear = false, anyFoxBoar = false, anyEngaging = false;

            if (ReadMember(container, "enemyUnitsInCombat") is IEnumerable enemies)
            {
                foreach (var dcObj in enemies)
                {
                    var dc = dcObj as Component;
                    if (dc == null) continue;
                    string kind = GetKind(dc.gameObject);
                    if (kind == "Wolf" || kind == "Bear") { anyWolfBear = true; }
                    else if (kind == "Fox" || kind == "Boar")
                    {
                        anyFoxBoar = true;
                        var retreating = ReadMember(dc, "isRetreating");
                        if (!(retreating is bool rb && rb)) anyEngaging = true;
                    }
                }
            }

            if (anyWolfBear) return true;            // always a real threat
            if (!anyFoxBoar) return !sighted;        // unknown enemy: trust blurbType
            if (sighted) return false;               // spotted fox/boar → amber
            return anyEngaging;                      // engaging & not all fleeing → red
        }

        private static string GetKind(GameObject go)
        {
            foreach (var kind in AnimalKinds)
            {
                var t = AccessTools.TypeByName(kind);
                if (t == null) continue;
                if (go.GetComponent(t) != null || go.GetComponentInParent(t) != null) return kind;
            }
            return "";
        }

        // ── Recolor helpers ─────────────────────────────────────────────────

        private static void Recolor(Image? img, Color sev)
        {
            if (img == null) return;
            int id = img.GetInstanceID();
            if (!_original.ContainsKey(id)) _original[id] = img.color;
            float a = _original[id].a;
            sev.a = a < 0.5f ? 1f : a; // floor: avoid invisible tint captured mid fade-in
            if (img.color != sev) img.color = sev;
        }

        private static void RestoreOriginal(Image? img)
        {
            if (img == null) return;
            int id = img.GetInstanceID();
            if (_original.TryGetValue(id, out var orig) && img.color != orig) img.color = orig;
        }

        // ── Reflection helpers ──────────────────────────────────────────────

        private static Image? FindChild(Transform root, string childName)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(includeInactive: true))
                if (img != null && img.gameObject.name == childName)
                    return img;
            return null;
        }

        private static object? ReadMember(object? obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            while (t != null)
            {
                var p = t.GetProperty(name, All);
                if (p != null && p.CanRead) return p.GetValue(obj);
                var f = t.GetField(name, All);
                if (f != null) return f.GetValue(obj);
                t = t.BaseType;
            }
            return null;
        }
    }
}
