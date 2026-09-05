using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// M2 — stamping. Arm with a blueprint selected, move the cursor to place the
    /// layout, Tab rotates it in 90° steps (the game's own rotate convention),
    /// click commits, right-click/Esc cancels.
    ///
    /// PREVIEW is translucent building meshes (StampGhostRenderer) over footprint
    /// outlines. Both are tinted green where the building will be placed and red
    /// where it won't — locked by tech, missing mod/DLC, or already at its cap —
    /// so a partial stamp is visible before you click rather than a surprise
    /// afterwards. If ghosts can't be built the outlines carry the preview alone.
    ///
    /// PLACEMENT is spread across frames. Each Construct instantiates GridCell
    /// objects, so a large blueprint placed in one frame visibly hitches.
    ///
    /// The stamp never creates anything the player couldn't build themselves: it
    /// drops ordinary build sites that villagers must supply and construct.
    /// </summary>
    internal static class BlueprintStamp
    {
        private enum State { Idle, Aiming, Placing }
        private static State _state = State.Idle;

        private static Blueprint? _blueprint;
        private static int _rot90;                 // whole-stamp rotation
        private static Vector3 _cursor;
        private static int _placedThisRun;
        private static Chord? _rotate;
        private static readonly StampGhostRenderer _ghosts = new StampGhostRenderer();
        private static int _lastRotateFrame = -1;

        /// <summary>Buildings placed per frame while committing. Small enough that
        /// a 40-building blueprint doesn't stutter, large enough to feel instant.</summary>
        private const int PlacementsPerFrame = 3;

        private static readonly Color OkColor = new Color(0.25f, 1f, 0.45f, 1f);
        private static readonly Color BlockedColor = new Color(1f, 0.25f, 0.2f, 1f);

        internal static bool IsArmed => _state != State.Idle;
        internal static string? ArmedName => _blueprint?.name;

        public static void OnMapLoaded()
        {
            _state = State.Idle;
            _blueprint = null;
            _rot90 = 0;
            StampGate.Reset();
            CaptureBoxRenderer.Clear();
            _ghosts.Clear();
            _rotate = Chord.Parse(FFUIOverhaulMod.RotateHotkey.Value, KeyCode.Tab);
        }

        /// <summary>Arm with the blueprint currently selected in the panel.</summary>
        public static void Toggle()
        {
            if (_state != State.Idle) { Cancel("disarmed"); return; }

            var bp = BlueprintPanel.Selected ?? BlueprintCapture.Clipboard;
            if (bp == null || bp.entries.Count == 0)
            {
                FFUIOverhaulMod.Log.Msg("[Stamp] nothing to stamp — capture a layout or select a saved blueprint.");
                return;
            }

            if (BlueprintCapture.IsArmed) BlueprintCapture.ToggleArmed();
            _blueprint = bp;
            _rot90 = 0;
            _state = State.Aiming;
            _ghosts.Build(bp);
            TopDownCamera.SetActive(true);
            FFUIOverhaulMod.Log.Msg($"[Stamp] armed '{bp.name}' ({bp.entries.Count} building(s)) — " +
                $"move to aim, {_rotate} to rotate, click to place, right-click or Esc to cancel.");
        }

        public static void OnUpdate()
        {
            if (_state == State.Idle) return;
            if (_state == State.Placing) return;   // the coroutine owns it

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Cancel("cancelled");
                return;
            }

            if (BlueprintPanel.PointerOverPanel) return;

            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null || gm.terrainManager == null) return;

            // Ctrl is the overhead-view modifier during stamping, so rotation must
            // still work while it's held — hence DownIgnoringCtrl rather than an
            // exact chord match.
            if (_rotate != null && _rotate.DownIgnoringCtrl()) Rotate();

            if (!gm.terrainManager.GetTerrainWorldPointUnderCursor(out Vector3 ground)) return;
            // Snap the anchor to the build grid: the captured offsets are already
            // grid-aligned, so an unsnapped anchor would drop the whole layout
            // between cells.
            float snapCell = CellSize();
            ground.x = Mathf.Round(ground.x / snapCell) * snapCell;
            ground.z = Mathf.Round(ground.z / snapCell) * snapCell;
            _cursor = ground;

            DrawPreview(out int ok, out int blocked);

            bool overUI = false;
            try { overUI = gm.pointerIsOverUI; } catch { }

            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                if (ok == 0)
                {
                    FFUIOverhaulMod.Log.Msg("[Stamp] nothing placeable here — every building is locked, missing or maxed.");
                    return;
                }
                Commit();
            }
        }

        /// <summary>Rotate the stamp a quarter turn. Frame-guarded because this is
        /// reachable from two places: the normal input path, and the panel's IMGUI
        /// event handler (IMGUI swallows Tab for focus traversal once a text field
        /// has focus, which stopped rotation dead after the first press).</summary>
        public static void Rotate()
        {
            if (_state != State.Aiming) return;
            if (Time.frameCount == _lastRotateFrame) return;
            _lastRotateFrame = Time.frameCount;
            _rot90 = (_rot90 + 1) & 3;
            FFUIOverhaulMod.Log.Msg($"[Stamp] rotated to {_rot90 * 90}°.");
        }

        internal static KeyCode RotateKey => _rotate?.Key ?? KeyCode.Tab;

        private static void Cancel(string why)
        {
            _state = State.Idle;
            TopDownCamera.SetActive(false);
            CaptureBoxRenderer.Clear();
            _ghosts.Clear();
            FFUIOverhaulMod.Log.Msg($"[Stamp] {why}.");
        }

        // ── geometry ────────────────────────────────────────────────────────

        /// <summary>Rotate a blueprint offset by the stamp's rotation. Quarter
        /// turns only, so this is exact — no trig, no drift.</summary>
        private static Vector2 RotateOffset(float dx, float dz, int rot90)
        {
            switch (rot90 & 3)
            {
                case 1:  return new Vector2(-dz, dx);
                case 2:  return new Vector2(-dx, -dz);
                case 3:  return new Vector2(dz, -dx);
                default: return new Vector2(dx, dz);
            }
        }

        /// <summary>
        /// The blueprint's pivot: the centre of its bounding box, SNAPPED to whole
        /// cells.
        ///
        /// Offsets are stored from the min-corner, so rotating them directly spins
        /// the layout around its corner — technically correct, but nothing like
        /// what a player expects from a stamp. Subtracting the centre first pivots
        /// about the middle and puts the middle under the cursor.
        ///
        /// The snap matters: a half-cell centre would shift every building half a
        /// cell off the build lattice. Rounding the pivot to whole cells keeps the
        /// captured layout exactly on-grid while still pivoting near the middle.
        /// </summary>
        private static Vector2 PivotOf(Blueprint bp)
        {
            if (bp.entries.Count == 0) return Vector2.zero;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var e in bp.entries)
            {
                if (e.dx < minX) minX = e.dx;
                if (e.dx > maxX) maxX = e.dx;
                if (e.dz < minZ) minZ = e.dz;
                if (e.dz > maxZ) maxZ = e.dz;
            }
            float cell = CellSize();
            return new Vector2(
                Mathf.Round((minX + maxX) * 0.5f / cell) * cell,
                Mathf.Round((minZ + maxZ) * 0.5f / cell) * cell);
        }

        private static Vector3 WorldPosOf(BlueprintEntry e)
        {
            var pivot = PivotOf(_blueprint!);
            var off = RotateOffset(e.dx - pivot.x, e.dz - pivot.y, _rot90);
            return new Vector3(_cursor.x + off.x, _cursor.y, _cursor.z + off.y);
        }

        // ── preview ─────────────────────────────────────────────────────────

        private static void DrawPreview(out int ok, out int blocked)
        {
            ok = 0; blocked = 0;
            if (_blueprint == null) return;

            // Mesh ghosts show the real buildings; outlines stay underneath so the
            // grid footprint is still legible (and carry the whole preview if
            // ghosts couldn't be built on this machine).
            bool useGhosts = !_ghosts.Failed;
            if (useGhosts) _ghosts.Build(_blueprint);
            useGhosts = !_ghosts.Failed && _ghosts.Count > 0;

            float cell = CellSize();
            CaptureBoxRenderer.Begin();

            int i = 0;
            foreach (var e in _blueprint.entries)
            {
                var block = StampGate.Check(e.id, out _);
                bool good = block == StampBlock.None;
                if (good) ok++; else blocked++;

                var p = WorldPosOf(e);
                float yaw = ((e.rot90 + _rot90) & 3) * 90f;
                var size = FootprintOf(e.id, (e.rot90 + _rot90) & 3, cell);

                CaptureBoxRenderer.AddFootprint(p, size.x, size.y,
                    good ? OkColor : BlockedColor);

                if (useGhosts)
                {
                    var groundPos = p;
                    try { groundPos = MeshUtilities.SetWorldPositionToTerrain(p); } catch { }
                    _ghosts.Place(i, groundPos, yaw, good);
                }
                i++;
            }

            CaptureBoxRenderer.End();
            if (useGhosts) _ghosts.SetVisible(true);
        }

        /// <summary>Footprint in world units, from the building's own grid size.
        /// Swapped for quarter turns that put it on its side.</summary>
        private static Vector2 FootprintOf(string id, int rot90, float cell)
        {
            float w = cell, h = cell;
            try
            {
                var bd = GlobalAssets.buildingSetupData?.GetBuildingData(id);
                var pl = bd?.placeablePrefab?.GetComponent<Placeable>();
                if (pl != null)
                {
                    var g = pl.gridSize;
                    if (g.x > 0f) w = g.x * cell;
                    if (g.y > 0f) h = g.y * cell;
                }
            }
            catch { }
            return (rot90 == 1 || rot90 == 3) ? new Vector2(h, w) : new Vector2(w, h);
        }

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

        // ── commit ──────────────────────────────────────────────────────────

        private static void Commit()
        {
            if (_blueprint == null) return;
            _state = State.Placing;
            CaptureBoxRenderer.Clear();
            _ghosts.Clear();
            _placedThisRun = 0;
            MelonLoader.MelonCoroutines.Start(PlaceAll(_blueprint, _cursor, _rot90));
        }

        private static IEnumerator PlaceAll(Blueprint bp, Vector3 origin, int rot90)
        {
            var skipped = new Dictionary<string, int>();
            int placed = 0, n = 0;
            var pivot = PivotOf(bp);   // must match the preview exactly

            foreach (var e in bp.entries)
            {
                var block = StampGate.Check(e.id, out _);
                if (block != StampBlock.None)
                {
                    string key = e.id + " (" + StampGate.Describe(block) + ")";
                    skipped.TryGetValue(key, out int c);
                    skipped[key] = c + 1;
                    continue;
                }

                var off = RotateOffset(e.dx - pivot.x, e.dz - pivot.y, rot90);
                var pos = new Vector3(origin.x + off.x, origin.y, origin.z + off.y);
                float yaw = ((e.rot90 + rot90) & 3) * 90f;

                bool built = false;
                try { built = PlaceOne(e, pos, yaw); }
                catch (Exception ex)
                {
                    FFUIOverhaulMod.Log.Warning($"[Stamp] '{e.id}' failed: {ex.Message}");
                }
                if (built) placed++;

                // Each placement instantiates GridCells; spread the work so a big
                // blueprint doesn't hitch the frame.
                if (++n % PlacementsPerFrame == 0) yield return null;
            }

            _placedThisRun = placed;
            _state = State.Idle;
            TopDownCamera.SetActive(false);

            var summary = $"[Stamp] placed {placed} of {bp.entries.Count} building(s)";
            if (skipped.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in skipped) parts.Add(kv.Value > 1 ? $"{kv.Key} x{kv.Value}" : kv.Key);
                summary += " — skipped: " + string.Join(", ", parts.ToArray());
            }
            FFUIOverhaulMod.Log.Msg(summary + ".");
        }

        /// <summary>
        /// Place one building. Same path as the proven M0 spike: instantiate the
        /// real placeable prefab as a throwaway so it computes bounds and
        /// terrainGridData against the terrain, harvest both, then Construct.
        ///
        /// Settings ride along IN the ConstructionData — the game applies them as
        /// the building initializes, so there is no second pass and no race with
        /// the building's own setup.
        /// </summary>
        private static bool PlaceOne(BlueprintEntry e, Vector3 worldPos, float yaw)
        {
            var gm = UnitySingleton<GameManager>.Instance;
            var buildManager = gm?.buildManager;
            if (buildManager == null) return false;

            var bd = GlobalAssets.buildingSetupData?.GetBuildingData(e.id);
            if (bd == null || bd.placeablePrefab == null) return false;

            var prefabEntry = bd.GetRandomPrefabEntry();
            if (prefabEntry == null) return false;

            worldPos = MeshUtilities.SetWorldPositionToTerrain(worldPos);

            GameObject? throwaway = null;
            try
            {
                throwaway = UnityEngine.Object.Instantiate(bd.placeablePrefab, worldPos,
                    Quaternion.Euler(0f, yaw, 0f));
                var placeable = throwaway.GetComponent<Placeable>();
                if (placeable == null) return false;

                placeable.Initialize(ConstructionType.CONSTRUCT, bd, prefabEntry.PREFAB());
                placeable.UpdatePosition(worldPos, force: true);

                Bounds bounds = placeable.bounds;
                bounds.center = worldPos;
                if (!placeable.boundsAlreadyRotated)
                {
                    float snapped = MiscUtilities.SnapAngleToClosest(
                        placeable.transform.rotation.eulerAngles.y, 90f);
                    if (Mathf.Approximately(snapped, 90f) || Mathf.Approximately(snapped, 270f))
                    {
                        Vector3 s = bounds.size;
                        bounds = new Bounds(worldPos, new Vector3(s.z, s.y, s.x));
                    }
                }

                var cd = default(ConstructionData);
                cd.constructionType = ConstructionType.CONSTRUCT;
                cd.buildGroup = bd.GetBuildGroupString();
                cd.position = worldPos;
                cd.rotation = Quaternion.Euler(0f, yaw, 0f);
                cd.terrainPos = worldPos;
                cd.terrainGridData = placeable.terrainGridInstance != null
                    ? placeable.terrainGridInstance.ExtractTerrainGridData() : null;
                cd.defaultNumBuilders = bd.defaultBuilders;
                cd.maxSimultaneousBuilders = bd.maxBuilders;
                cd.bounds = bounds;
                cd.buildsiteClearingMode = bd.buildsiteClearingMode;
                cd.clearDetailsBorderWidth = bd.clearDetailsBorderWidth;
                cd.materialsRequired = bd.GetRequiredMaterialsDict();
                cd.addToStorageAfterConstruction = bd.GetAddToStorageAfterConstruction();
                cd.parent = buildManager.parent != null ? buildManager.parent.transform : null;
                cd.sceneObject = null;
                cd.prefabToConstruct = prefabEntry.PREFAB();
                cd.buildSitePrefab = bd.buildSitePrefab;
                cd.emptyLotPrefab = prefabEntry.emptyLotPrefab;
                cd.constructionStartedPrefab = prefabEntry.constructionStartedPrefab;
                cd.constructionProgressPrefabs = prefabEntry.constructionProgressPrefabs;
                cd.placedInFreeBuild = buildManager.IsFreeBuildEnabled();
                cd.mutePlacementSound = true;   // one sound per stamp, not per building

                ApplySettings(ref cd, e.settings);

                return buildManager.Construct(cd) != null;
            }
            finally
            {
                if (throwaway != null) UnityEngine.Object.Destroy(throwaway);
            }
        }

        /// <summary>Copy captured settings onto the ConstructionData. These fields
        /// exist on the struct precisely so placement can carry them.</summary>
        private static void ApplySettings(ref ConstructionData cd, BlueprintSettings s)
        {
            if (s == null) return;
            try
            {
                cd.workEnabled = s.workEnabled;
                if (s.workers >= 0) cd.userDefinedMaxWorkers = s.workers;

                if (s.recipes != null && s.recipes.Count > 0)
                {
                    var enabled = new Dictionary<Guid, bool>();
                    var batch = new Dictionary<Guid, int>();
                    foreach (var r in s.recipes)
                    {
                        if (string.IsNullOrEmpty(r.guid)) continue;
                        Guid g;
                        try { g = new Guid(r.guid); } catch { continue; }
                        enabled[g] = r.enabled;
                        if (r.batch >= 0) batch[g] = r.batch;
                    }
                    if (enabled.Count > 0) cd.manufactureDefEnabledDict = enabled;
                    if (batch.Count > 0) cd.manufactureDefBatchSizeDict = batch;
                }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[Stamp] settings not applied: " + e.Message);
            }
        }
    }
}
