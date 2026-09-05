using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Draws the capture selection as a grid of magenta lines that follows the
    /// terrain.
    ///
    /// WHY NOT FF'S OWN TerrainGrid: Placeable.terrainGridPrefab turned out to be
    /// unavailable on the placeables we can reach (the lookup returned null, so
    /// the first attempt silently fell back to a plain outline). Rather than keep
    /// chasing an engine object that carries placement semantics we don't want,
    /// this draws the grid directly — full control over colour, no cost/validity
    /// baggage, and it can't fail at runtime.
    ///
    /// Lines are terrain-sampled at every cell crossing so they hug slopes
    /// instead of floating over them, and LineRenderers are pooled because a
    /// drag re-draws every frame.
    /// </summary>
    internal static class CaptureBoxRenderer
    {
        // Bright magenta for the border, a dimmer wash for interior cell lines so
        // the outline stays readable against the grid.
        private static readonly Color BorderColor = new Color(1f, 0.15f, 0.9f, 1f);
        private static readonly Color GridColor = new Color(1f, 0.35f, 0.9f, 0.5f);

        // The alignment grid is deliberately quiet — it is a reference, not the
        // subject; the magenta selection has to stay the thing your eye lands on.
        private static readonly Color GuideColor = new Color(1f, 1f, 1f, 0.28f);
        private const float GuideWidth = 0.10f;

        private const float BorderWidth = 0.5f;
        private const float GridWidth = 0.18f;
        private const float Lift = 0.35f;

        // A pathological drag shouldn't spawn thousands of renderers; past this
        // many cells per axis the interior grid is dropped and only the border
        // draws. The selection itself is unaffected.
        private const int MaxCellsPerAxis = 80;

        private static GameObject? _root;
        private static readonly List<LineRenderer> _pool = new List<LineRenderer>();
        private static int _used;
        private static Material? _mat;

        /// <summary>Start a frame's drawing. Both the guide grid and the selection
        /// box draw from one pooled set of renderers, so they must be bracketed by
        /// Begin/End.</summary>
        public static void Begin()
        {
            EnsureRoot();
            _used = 0;
        }

        /// <summary>Finish a frame: retire any renderers left over from a busier
        /// previous frame.</summary>
        public static void End()
        {
            for (int i = _used; i < _pool.Count; i++)
                if (_pool[i] != null) _pool[i].gameObject.SetActive(false);
            if (_root != null && !_root.activeSelf) _root.SetActive(true);
        }

        /// <summary>
        /// A faint alignment grid centred on the cursor, mirroring the local grid
        /// FF shows while you size a crop field. It exists so you can see the
        /// build grid you're snapping to before committing to a drag.
        /// </summary>
        public static void AddGuideGrid(Vector3 centre, float cellSize, int halfCells)
        {
            float half = halfCells * cellSize;
            float cx = Mathf.Round(centre.x / cellSize) * cellSize;
            float cz = Mathf.Round(centre.z / cellSize) * cellSize;
            float minX = cx - half, maxX = cx + half;
            float minZ = cz - half, maxZ = cz + half;
            int spans = halfCells * 2;

            for (int i = 0; i <= spans; i++)
            {
                float x = minX + i * cellSize;
                DrawSampledLine(new Vector3(x, 0f, minZ), new Vector3(x, 0f, maxZ),
                    spans, GuideColor, GuideWidth);
            }
            for (int j = 0; j <= spans; j++)
            {
                float z = minZ + j * cellSize;
                DrawSampledLine(new Vector3(minX, 0f, z), new Vector3(maxX, 0f, z),
                    spans, GuideColor, GuideWidth);
            }
        }

        /// <summary>A single magenta cell at the cursor — the "you are armed and
        /// this is where you'd start" marker, mirroring the highlighted square FF
        /// shows while siting a placement.</summary>
        public static void AddCursorCell(Vector3 cursor, float cellSize)
        {
            // Floor, not Round: the cell must span from the grid line at or below
            // the cursor to the next one, so its edges land ON the guide-grid
            // lines. Rounding centres the square on an intersection instead,
            // which draws it half a cell out of phase with the grid.
            float x0 = Mathf.Floor(cursor.x / cellSize) * cellSize;
            float z0 = Mathf.Floor(cursor.z / cellSize) * cellSize;
            float x1 = x0 + cellSize;
            float z1 = z0 + cellSize;
            var c0 = new Vector3(x0, 0f, z0);
            var c1 = new Vector3(x1, 0f, z0);
            var c2 = new Vector3(x1, 0f, z1);
            var c3 = new Vector3(x0, 0f, z1);
            DrawSampledLine(c0, c1, 1, BorderColor, BorderWidth);
            DrawSampledLine(c1, c2, 1, BorderColor, BorderWidth);
            DrawSampledLine(c2, c3, 1, BorderColor, BorderWidth);
            DrawSampledLine(c3, c0, 1, BorderColor, BorderWidth);
        }

        public static void AddBox(Vector3 a, Vector3 b, float cellSize)
        {

            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minZ = Mathf.Min(a.z, b.z), maxZ = Mathf.Max(a.z, b.z);

            int cellsX = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / cellSize));
            int cellsZ = Mathf.Max(1, Mathf.RoundToInt((maxZ - minZ) / cellSize));

            if (cellsX <= MaxCellsPerAxis && cellsZ <= MaxCellsPerAxis)
            {
                // Interior lines, skipping the outer edges (the border draws those).
                for (int i = 1; i < cellsX; i++)
                {
                    float x = minX + i * cellSize;
                    DrawSampledLine(new Vector3(x, 0f, minZ), new Vector3(x, 0f, maxZ),
                        cellsZ, GridColor, GridWidth);
                }
                for (int j = 1; j < cellsZ; j++)
                {
                    float z = minZ + j * cellSize;
                    DrawSampledLine(new Vector3(minX, 0f, z), new Vector3(maxX, 0f, z),
                        cellsX, GridColor, GridWidth);
                }
            }

            // Border last so it sorts over the interior.
            DrawSampledLine(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ), cellsX, BorderColor, BorderWidth);
            DrawSampledLine(new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ), cellsZ, BorderColor, BorderWidth);
            DrawSampledLine(new Vector3(maxX, 0f, maxZ), new Vector3(minX, 0f, maxZ), cellsX, BorderColor, BorderWidth);
            DrawSampledLine(new Vector3(minX, 0f, maxZ), new Vector3(minX, 0f, minZ), cellsZ, BorderColor, BorderWidth);

        }

        /// <summary>A building footprint outline, centred on its placement, in the
        /// colour that says whether it will actually be placed.</summary>
        public static void AddFootprint(Vector3 centre, float width, float depth, Color color)
        {
            float hw = width * 0.5f, hd = depth * 0.5f;
            var c0 = new Vector3(centre.x - hw, 0f, centre.z - hd);
            var c1 = new Vector3(centre.x + hw, 0f, centre.z - hd);
            var c2 = new Vector3(centre.x + hw, 0f, centre.z + hd);
            var c3 = new Vector3(centre.x - hw, 0f, centre.z + hd);
            DrawSampledLine(c0, c1, 2, color, BorderWidth);
            DrawSampledLine(c1, c2, 2, color, BorderWidth);
            DrawSampledLine(c2, c3, 2, color, BorderWidth);
            DrawSampledLine(c3, c0, 2, color, BorderWidth);
        }

        /// <summary>One line, subdivided and terrain-sampled so it follows slopes.</summary>
        private static void DrawSampledLine(Vector3 from, Vector3 to, int segments, Color color, float width)
        {
            segments = Mathf.Clamp(segments, 1, 200);
            var lr = Take();
            lr.startColor = lr.endColor = color;
            lr.startWidth = lr.endWidth = width;
            lr.positionCount = segments + 1;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                var p = Vector3.Lerp(from, to, t);
                lr.SetPosition(i, SampleTerrain(p));
            }
        }

        private static Vector3 SampleTerrain(Vector3 p)
        {
            try { p = MeshUtilities.SetWorldPositionToTerrain(p); }
            catch { /* off-map or terrain not ready — keep the raw point */ }
            p.y += Lift;
            return p;
        }

        private static LineRenderer Take()
        {
            if (_used < _pool.Count)
            {
                var existing = _pool[_used++];
                existing.gameObject.SetActive(true);
                return existing;
            }

            var go = new GameObject("Line");
            go.transform.SetParent(_root!.transform, worldPositionStays: false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.material = _mat!;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            _pool.Add(lr);
            _used++;
            return lr;
        }

        private static void EnsureRoot()
        {
            if (_root != null) return;
            _root = new GameObject("FFUI_BlueprintBox");
            Object.DontDestroyOnLoad(_root);
            // Sprites/Default renders untextured vertex colour without needing a
            // project material — the standard runtime-gizmo trick.
            _mat = new Material(Shader.Find("Sprites/Default"));
        }

        public static void Clear()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _pool.Clear();
            _used = 0;
        }
    }
}
