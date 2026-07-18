using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Snapping
{
    /// <summary>
    /// Tracks every live Keep Clarity <see cref="UI.DraggablePanel"/> so a panel being
    /// dragged can snap to the others (plus the game minimap and the screen edges).
    /// Panels self-register in OnEnable/OnDisable, so all overlay types — pinned, tech
    /// queue, build queue, company, and their collapsed tabs — participate with zero
    /// per-overlay code.
    /// </summary>
    internal static class OverlayRegistry
    {
        private static readonly List<UI.DraggablePanel> _panels = new List<UI.DraggablePanel>();
        private static readonly List<Rect> _scratch = new List<Rect>(8);

        public static void Register(UI.DraggablePanel p)
        {
            if (p != null && !_panels.Contains(p)) _panels.Add(p);
        }

        public static void Unregister(UI.DraggablePanel p) => _panels.Remove(p);

        /// <summary>
        /// Snap offset (canvas-local px) for <paramref name="dragging"/> at its proposed
        /// rect, considering every other active panel, the minimap, and the screen bounds.
        /// </summary>
        public static Vector2 ComputeSnap(UI.DraggablePanel dragging, RectTransform canvasRt,
            Camera? canvasCam, Rect draggedProposed, float threshold)
        {
            _scratch.Clear();
            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                if (p == null || p == dragging || p.Target == null) continue;
                if (!p.isActiveAndEnabled || !p.Target.gameObject.activeInHierarchy) continue;
                if (SnapEngine.TryLocalRect(canvasRt, canvasCam, p.Target, out var r)) _scratch.Add(r);
            }

            var mm = ResolveMinimap();
            if (mm != null && mm.gameObject.activeInHierarchy &&
                SnapEngine.TryLocalRect(canvasRt, canvasCam, mm, out var mr)) _scratch.Add(mr);

            return SnapEngine.Apply(draggedProposed, _scratch, canvasRt.rect, threshold);
        }

        // ---- Minimap (reflective soft access; degrades to null if FF renames it) ----
        // FF's Minimap.instance is FindObjectOfType-backed; the visible widget hangs at
        // "MiniMap Root" under it. We cache the RectTransform but Unity's == treats a
        // destroyed object (scene reload) as null, so it transparently re-resolves.
        private static RectTransform? _minimap;
        private static MethodInfo? _instanceGetter;
        private static FieldInfo? _instanceField;
        private static bool _typeMissing;

        private static RectTransform? ResolveMinimap()
        {
            if (_minimap != null) return _minimap;     // null also when the cached object was destroyed
            if (_typeMissing) return null;
            try
            {
                var t = AccessTools.TypeByName("Minimap");
                if (t == null) { _typeMissing = true; return null; }

                if (GetStaticInstance(t) is not Component comp || comp == null) return null; // not built yet — retry next drag
                var rootTf = comp.transform.Find("MiniMap Root");
                var pick = rootTf != null ? rootTf : comp.transform;
                _minimap = pick as RectTransform ?? pick.GetComponent<RectTransform>();
                return _minimap;
            }
            catch { return null; }
        }

        private static object? GetStaticInstance(Type t)
        {
            const BindingFlags F = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
            if (_instanceGetter == null && _instanceField == null)
            {
                _instanceGetter = t.GetProperty("instance", F)?.GetGetMethod()
                               ?? t.GetProperty("Instance", F)?.GetGetMethod();
                if (_instanceGetter == null)
                    _instanceField = t.GetField("instance", F) ?? t.GetField("Instance", F);
            }
            return _instanceGetter != null ? _instanceGetter.Invoke(null, null) : _instanceField?.GetValue(null);
        }
    }
}
