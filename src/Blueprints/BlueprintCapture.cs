using System;
using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// M1 — capture. Press the capture hotkey to arm, click once to anchor a
    /// corner, move, then click again to commit. Right-click or Esc cancels.
    ///
    /// INTERACTION mirrors FF's own placement tools deliberately — they are
    /// click/click ("Click to finish road"), not press-and-hold, so the button
    /// does not have to be held through the drag. Two earlier attempts were worse:
    /// capturing on hotkey-release (no way to abort), then hold-drag (correct
    /// shape, wrong muscle memory).
    ///
    /// THE BOX is drawn by CaptureBoxRenderer as terrain-following magenta grid
    /// lines, with corners snapped to whole cells via BuildManager.cellSize so the
    /// selection lines up with the build grid. FF's own unit-selection marquee is
    /// suppressed while armed (SelectionBoxSuppressPatch) — it is screen-space and
    /// ours is world-space, so the two can never agree on screen.
    ///
    /// The box is a WORLD-space rectangle (both corners come from the terrain
    /// raycast), not a screen rectangle: FF's camera pitches, and a screen-space
    /// marquee would select a trapezoid of world space and drift out of alignment
    /// with the grid the moment you tilt.
    ///
    /// MEMBERSHIP is centre-inside rather than "every grid cell inside" — a
    /// strict rule would force a margin around everything, and dragging tight
    /// along a row of buildings is the common case.
    /// </summary>
    internal static class BlueprintCapture
    {
        private enum State { Idle, Armed, Dragging }
        private static State _state = State.Idle;

        private static Vector2Int _anchorCell;
        private static Vector2Int _currentCell;


        /// <summary>The most recent capture — the in-memory "clipboard" a stamp
        /// will use before named blueprint files exist (M3). Survives scene
        /// changes: blueprints are position-independent, so a layout captured in
        /// one town is still meaningful in the next.</summary>
        internal static Blueprint? Clipboard { get; private set; }

        internal static bool IsArmed => _state != State.Idle;

        public static void OnMapLoaded()
        {
            _state = State.Idle;
            TopDownCamera.Reset();
            DestroyVisuals();
        }

        /// <summary>Toggle capture mode (called when the hotkey is pressed).</summary>
        public static void ToggleArmed()
        {
            if (_state == State.Idle)
            {
                // Both tools own the left click; arming one must disarm the other.
                if (BlueprintStamp.IsArmed) BlueprintStamp.Toggle();
                _state = State.Armed;
                TopDownCamera.SetActive(true);   // Ctrl = overhead, as in placement
                FFUIOverhaulMod.Log.Msg("[Capture] armed — click once to anchor a corner, " +
                    "hold Ctrl for the overhead view, " +
                    "click again to capture (right-click or Esc to cancel).");
            }
            else
            {
                Cancel("disarmed");
            }
        }

        public static void OnUpdate()
        {
            if (_state == State.Idle) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Cancel("cancelled");
                return;
            }

            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null || gm.terrainManager == null) return;

            // Don't start a drag through the HUD.
            bool overUI = false;
            try { overUI = gm.pointerIsOverUI; } catch { }

            // Never treat a click on our own panel as a world click.
            if (BlueprintPanel.PointerOverPanel) return;

            if (_state == State.Armed)
            {
                // Show the alignment grid at the cursor before the drag starts —
                // the same affordance FF gives while siting a crop field, so you
                // can line the capture up before committing to it.
                if (gm.terrainManager.GetTerrainWorldPointUnderCursor(out Vector3 hover))
                    DrawGuide(hover);

                if (Input.GetMouseButtonDown(0) && !overUI &&
                    gm.terrainManager.GetTerrainWorldPointUnderCursor(out Vector3 start))
                {
                    _anchorCell = CellOf(start);
                    _currentCell = _anchorCell;
                    _state = State.Dragging;
                }
                return;
            }

            // Dragging
            if (gm.terrainManager.GetTerrainWorldPointUnderCursor(out Vector3 cur))
                _currentCell = CellOf(cur);

            CellRect(_anchorCell, _currentCell, out float bx0, out float bz0, out float bx1, out float bz1);
            DrawBox(new Vector3(bx0, 0f, bz0), new Vector3(bx1, 0f, bz1));

            // Click to ANCHOR, click again to COMMIT — FF's own tools work this way
            // ("Click to finish road"), so holding the button through the drag felt
            // wrong. GetMouseButtonDown, not Up: releasing the anchoring click must
            // not immediately commit a zero-size box.
            if (Input.GetMouseButtonDown(0))
            {
                DestroyVisuals();
                _state = State.Idle;
                TopDownCamera.SetActive(false);
                CellRect(_anchorCell, _currentCell, out float cx0, out float cz0, out float cx1, out float cz1);
                Commit(new Vector3(cx0, 0f, cz0), new Vector3(cx1, 0f, cz1));
            }
        }

        private static void Cancel(string why)
        {
            _state = State.Idle;
            TopDownCamera.SetActive(false);
            DestroyVisuals();
            FFUIOverhaulMod.Log.Msg($"[Capture] {why}.");
        }

        /// <summary>
        /// The CELL a world point falls in, as integer cell coordinates.
        ///
        /// Anchoring used to Round to the nearest grid LINE, which meant clicking
        /// past the midpoint of a square jumped the corner to the neighbouring
        /// one — you had to hit the middle of a cell to get the cell you aimed at.
        /// Selecting whole cells (floor) instead means clicking anywhere inside a
        /// square selects that square, which is also what the cursor highlight
        /// already showed.
        /// </summary>
        private static Vector2Int CellOf(Vector3 p)
        {
            float cell = CellSize();
            return new Vector2Int(
                Mathf.FloorToInt(p.x / cell),
                Mathf.FloorToInt(p.z / cell));
        }

        /// <summary>World-space rect covering every cell between the two cells,
        /// inclusive — so both the anchor and the current cell are inside.</summary>
        private static void CellRect(Vector2Int a, Vector2Int b,
            out float minX, out float minZ, out float maxX, out float maxZ)
        {
            float cell = CellSize();
            minX = Mathf.Min(a.x, b.x) * cell;
            minZ = Mathf.Min(a.y, b.y) * cell;
            maxX = (Mathf.Max(a.x, b.x) + 1) * cell;
            maxZ = (Mathf.Max(a.y, b.y) + 1) * cell;
        }

        // ── capture ─────────────────────────────────────────────────────────

        private static void Commit(Vector3 a, Vector3 b)
        {
            var bp = Capture(a, b);
            if (bp == null) return;

            Clipboard = bp;
            var size = bp.SizeXZ();
            FFUIOverhaulMod.Log.Msg($"[Capture] CAPTURED {bp.entries.Count} building(s), " +
                $"footprint {size.x:0.#} x {size.y:0.#}:");
            foreach (var e in bp.entries)
            {
                string extra = e.settings.HasAny
                    ? $"  [workers={e.settings.workers} work={e.settings.workEnabled}" +
                      (e.settings.recipes.Count > 0 ? $" recipes={e.settings.recipes.Count}" : "") +
                      (e.settings.priority.Length > 0 ? $" prio={e.settings.priority}" : "") + "]"
                    : "";
                FFUIOverhaulMod.Log.Msg("[Capture]   " + e + extra);
            }
        }

        private static Blueprint? Capture(Vector3 a, Vector3 b)
        {
            var gm = UnitySingleton<GameManager>.Instance;
            var rm = gm != null ? gm.resourceManager : null;
            if (rm == null || rm.allBuildingsRO == null)
            {
                FFUIOverhaulMod.Log.Warning("[Capture] no resource manager.");
                return null;
            }

            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minZ = Mathf.Min(a.z, b.z), maxZ = Mathf.Max(a.z, b.z);

            var hits = new List<Building>();
            int skippedSegment = 0;
            foreach (var building in rm.allBuildingsRO)
            {
                if (building == null) continue;
                var p = building.transform.position;
                if (p.x < minX || p.x > maxX || p.z < minZ || p.z > maxZ) continue;

                // v1 excludes walls, gates and roads: they are placed by
                // SegmentBuilder / SplineRoadBuilder, not Input_PlaceBuilding, so
                // Construct is not their pipeline and a stamp cannot recreate them.
                if (IsSegmentLike(building)) { skippedSegment++; continue; }

                hits.Add(building);
            }

            if (hits.Count == 0)
            {
                FFUIOverhaulMod.Log.Msg("[Capture] nothing captured" +
                    (skippedSegment > 0 ? $" ({skippedSegment} wall/gate/road skipped — not supported yet)." : "."));
                return null;
            }

            // Origin = min corner of what we actually captured, not of the drag
            // box, so a sloppy drag doesn't bake dead space into the offsets.
            float originX = float.MaxValue, originZ = float.MaxValue;
            foreach (var bldg in hits)
            {
                var p = bldg.transform.position;
                if (p.x < originX) originX = p.x;
                if (p.z < originZ) originZ = p.z;
            }

            var bp = new Blueprint
            {
                name = "clipboard",
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            };

            foreach (var bldg in hits)
            {
                string id = null!;
                try { id = bldg.buildingDataRecordName; } catch { }
                if (string.IsNullOrEmpty(id))
                {
                    FFUIOverhaulMod.Log.Warning($"[Capture] '{bldg.name}' has no buildingDataRecordName — skipped.");
                    continue;
                }

                var p = bldg.transform.position;
                float yaw = MiscUtilities.SnapAngleToClosest(bldg.transform.eulerAngles.y, 90f);
                int rot90 = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 90f) % 4;

                bp.entries.Add(new BlueprintEntry
                {
                    id = id,
                    dx = p.x - originX,
                    dz = p.z - originZ,
                    rot90 = rot90,
                    settings = CaptureSettings(bldg),
                });
            }

            if (skippedSegment > 0)
                FFUIOverhaulMod.Log.Msg($"[Capture] skipped {skippedSegment} wall/gate/road (not supported in v1).");

            return bp.entries.Count > 0 ? bp : null;
        }

        /// <summary>
        /// Read the settings worth preserving. Every one of these has a matching
        /// ConstructionData field, so the stamp can pass them in at construct time
        /// rather than re-applying afterwards. Read defensively per field: a
        /// building type that doesn't implement one should cost that field, not
        /// the whole capture.
        /// </summary>
        private static BlueprintSettings CaptureSettings(Building b)
        {
            var s = new BlueprintSettings();

            try { s.workEnabled = b.isWorkEnabled; } catch { }
            try { s.workers = b.userDefinedMaxWorkers; } catch { }
            try { if (b.priority != Resource.Priority.None) s.priority = b.priority.ToString(); } catch { }

            // Merge both game dictionaries into one keyed list — JsonUtility can't
            // serialize Dictionary, and the stamp rebuilds dictionaries anyway.
            var byGuid = new Dictionary<string, RecipePref>();
            try
            {
                if (b.manufactureDefEnabledDict != null)
                    foreach (var kv in b.manufactureDefEnabledDict)
                    {
                        if (kv.Key == null) continue;
                        string g = kv.Key.guid.ToString();
                        if (!byGuid.TryGetValue(g, out var r)) byGuid[g] = r = new RecipePref { guid = g };
                        r.enabled = kv.Value;
                    }
            }
            catch { }

            try
            {
                if (b.manufactureDefBatchSizeDict != null)
                    foreach (var kv in b.manufactureDefBatchSizeDict)
                    {
                        if (kv.Key == null) continue;
                        string g = kv.Key.guid.ToString();
                        if (!byGuid.TryGetValue(g, out var r)) byGuid[g] = r = new RecipePref { guid = g };
                        r.batch = kv.Value;
                    }
            }
            catch { }

            s.recipes.AddRange(byGuid.Values);
            return s;
        }

        private static bool IsSegmentLike(Building b) =>
            b is Wall || b is Gate || b is SplineRoadBuilding || b is SplineRoadBuildPoint;

        // ── box visual ──────────────────────────────────────────────────────

        /// <summary>Cells of alignment grid shown either side of the cursor.
        /// ~20x20 total, matching the local grid FF shows during placement.</summary>
        private const int GuideHalfCells = 10;

        private static void DrawGuide(Vector3 cursor)
        {
            float cell = CellSize();
            CaptureBoxRenderer.Begin();
            CaptureBoxRenderer.AddGuideGrid(cursor, cell, GuideHalfCells);
            CaptureBoxRenderer.AddCursorCell(cursor, cell);
            CaptureBoxRenderer.End();
        }

        private static void DrawBox(Vector3 a, Vector3 b)
        {
            float cell = CellSize();
            CaptureBoxRenderer.Begin();
            // Guide grid stays up during the drag, centred on the live corner.
            CaptureBoxRenderer.AddGuideGrid(b, cell, GuideHalfCells);
            CaptureBoxRenderer.AddBox(a, b, cell);
            CaptureBoxRenderer.End();
        }

        private static void DestroyVisuals() => CaptureBoxRenderer.Clear();

        private static float CellSize()
        {
            try
            {
                var bm = UnitySingleton<GameManager>.Instance?.buildManager;
                if (bm != null && bm.cellSize > 0.01f) return bm.cellSize;
            }
            catch { }
            return 2f;
        }

    }
}
