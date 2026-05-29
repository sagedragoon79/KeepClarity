# KC Audit: RestartRequired / ReloadRequired indicators are static, not "live"

**Date:** 2026-05-28
**Status:** diagnosed, not yet fixed — picked up from a Sovereign Boons session where
the user noticed every SB feature shows the cyan "!" even right after a save load when
the features are clearly already live.

---

## User-reported symptom

- Every reload-flagged setting shows the cyan "!" ("Not live — reload your save…")
  **permanently**, including immediately after the user loaded a save and the feature
  is demonstrably already applied.
- User's mental model: the **amber** RestartRequired "!" *does* clear after a full game
  restart, so they assumed reload-required should clear after a reload. (See "Reality"
  below — the amber per-row icon doesn't actually clear either; they're likely seeing
  the footer/changed-count clear, not the row icon.)

## Reality (current code)

`src/Settings/UI/ModDetailPanel.cs` ~L338–357 — the per-row indicator is gated **only
on the meta flag**, never on changed-state:

```csharp
if (e.Meta.RestartRequired && IconWarning != null) { /* amber, static */ }
else if (e.Meta.ReloadRequired && IconWarning != null) { /* cyan, static */ }
```

So **both** the amber and cyan per-row icons are static decoration — they show whenever
the flag is set, regardless of whether the value has changed or whether you've since
restarted/reloaded. Neither truly "clears."

The only existing baseline is `SettingsRegistry._sessionStartValues`
(`SettingsRegistry.cs` L13–33, `CaptureSessionStartValues`), captured **lazily on first
panel open**, used by the **Reset** button (revert-to-session-start) and the
footer/changed-count in `SettingsCanvas.cs`. It is *not* keyed to mod init or to save
load, and the per-row icons don't consult it at all. What the user perceives as the
amber "clearing after restart" is almost certainly the footer banner / changed-count
(which compares against session-start), not the row icon.

## Why restart *can* be tracked and reload *should* be too

The fix is to make the icons a **live "changed vs. the right baseline" signal** instead
of static flags, with a baseline matched to each flag's semantics:

- **RestartRequired** → baseline = value **at mod init** (process start). These are
  one-time writes that bake in at `OnInitializeMelon`. Capture an init-time snapshot;
  show amber only when `current != initValue`. Naturally clears after a full restart
  because init re-reads the saved value (baseline == current again).

- **ReloadRequired** → baseline = value **at last Map scene load**. SB boons (Greater
  Halls, Domain Expansion, Civic Pride, Bountiful Fields, etc.) apply in building
  `Awake` / on map load, so a save load makes them live. KC already gets
  `OnSceneWasInitialized(buildIndex, "Map")`. Snapshot every entry's value there; show
  cyan only when `current != lastMapLoadValue`. Clears after a reload — exactly the
  behavior the user expects.

## Proposed implementation sketch

1. In `SettingsRegistry`, add two baseline maps distinct from `_sessionStartValues`:
   - `_initValues` — captured once, after all mods register at startup.
   - `_mapLoadValues` — re-captured on each `"Map"` scene init (clear + refill).
   Add `TryGetInitValue` / `TryGetMapLoadValue` accessors mirroring
   `TryGetSessionStartValue`.
2. Hook KC's `OnSceneWasInitialized` to call the map-load capture (init capture can ride
   the existing post-registration path / first panel open, but must be a *fixed* init
   snapshot, not session-start).
3. In `ModDetailPanel.BuildEntryRow` (~L338), gate each icon:
   - amber: `e.Meta.RestartRequired && Changed(e, initValue)`
   - cyan:  `e.Meta.ReloadRequired  && Changed(e, mapLoadValue)`
   where `Changed` uses the same `!Equals(get(), baseline)` comparison the changed-count
   already uses. Keep the invisible-spacer `else` branch so row layout is unchanged.
4. Re-render the detail panel on `"Map"` load if it's open, so a reload visibly clears
   stale cyan icons (panels are usually closed in-game, so low priority).

## Touch points

- `src/Settings/SettingsRegistry.cs` — new baselines + accessors + capture calls
- `src/Settings/UI/ModDetailPanel.cs` (~L338–361) — gate icons on changed-vs-baseline
- KC `OnSceneWasInitialized` (Plugin) — trigger map-load capture
- (no SB change needed — the flags on SB's registrations are correct; this is purely
  KC's indicator-rendering logic)

## Note

SB's flag *assignments* are fine and intentional (verified 2026-05-28): the per-building
Greater Halls entries and the radius/crop boons are genuinely reload-to-apply for
existing buildings. The problem is entirely KC-side rendering: a static flag should be a
live changed-signal.
