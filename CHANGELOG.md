# Keep Clarity — Changelog

## v1.2.8 (2026-05-29) — Bridges Anywhere: hold mid-section at bank height

### Fixed
- **Bridges over dry ravines / high-bank rivers still sagged to water level.**
  The start/end ramps positioned correctly, but the arches, pillars, and
  mid-section pathway hung at `seaLevel + bridgeHeightAboveWater` (~water + 6)
  because `BridgeContainer.AssignStartAndEndCells` hard-codes
  `meshObjsParent.position.y` to that constant (decompile line 54586).
- **Fix:** new postfix on `AssignStartAndEndCells` re-anchors
  `meshObjsParent.position.y` to
  `max(seaLevel + bridgeHeightAboveWater, max(startCell.y, endCell.y))`. For
  normal river bridges the sea-level term wins → identical to vanilla. For
  high-bank/ravine spans the bank height wins → the whole bridge sits at bank
  level. Also re-anchors `startSection` and `endSection` world Y to their
  actual bank cells so the ramps don't drift up with the parent.

---

## v1.2.7 (2026-05-29) — Bridges Anywhere: fix start-cell patch target

### Fixed
- **Bridges Anywhere had no effect** even after restart. The first of the three
  Harmony patches targeted `PlacementValidityHelper.PeformBridgeStartCellValidityChecks`,
  but at runtime that method lives on **`PlaceableBridge`** (the decompile's
  call sites confirm `PlaceableBridge.PeformBridgeStartCellValidityChecks(...)`).
  Harmony logged `Undefined target method` and `PatchAll` aborted, so none of
  the three bridge patches actually applied. Retargeted patch 1 to
  `PlaceableBridge`. All three now resolve.

---

## v1.2.6 (2026-05-29) — Bridges Anywhere

### Added
- **Bridges Anywhere** (off by default). Lifts vanilla's two restrictive bridge
  placement rules so you can span dry ravines and high-bank rivers without
  terraforming:
  - The "start cell must be adjacent to water" requirement is dropped.
  - The "snap end cell to the first non-water cell from start" pass is skipped,
    so the bridge end stays at your cursor.
  - The full-validity pass no longer requires inner/end cells to be water or
    water-adjacent.
  - Height resolves naturally — `BridgeContainer.AssignStartAndEndCells` uses
    each cell's terrain-Y, so high-bank starts/ends position the bridge at land
    height with no extra patching.
  - Patches: postfix on `PlacementValidityHelper.PeformBridgeStartCellValidityChecks`
    (typo intentional — that's the game's spelling) and `UpdateBridgeValidity`
    clear the `Overlap_Water` required-flag bit; prefix on
    `PlaceableBridge.TryToSnapToValidPosition` short-circuits the snap.
  - Toggle is live — placement re-reads the pref on every attempt.
- New "Bridges Anywhere (no water / any height)" entry under **Game and Map
  Settings** in the KC panel.

---

## v1.2.5 (2026-05-28) — Tech queue reliability, reload indicator, TW handoff

### Fixed
- **Tech-tree queue markers vanishing after a load.** For a beat after loading a save, the active save name is blank, which made the per-save queue load as empty and flicker the markers/auto-spend off until it resolved. The queue now waits for a real save name before (re)loading, the tech tree ensures the queue is loaded before numbering markers, and markers/overlay refresh the instant the queue resolves.
- **Mod-manager "!" indicators were static.** The amber (restart) and cyan (reload) per-setting markers showed whenever the flag was set, so they never cleared. They're now a live "changed vs. baseline" signal: amber shows only when a value differs from its process-start value (clears after a restart); cyan only when it differs from the value at the last save load (clears after a reload).

### Added
- **Tended Wilds rank-cap handoff.** KC's auto-research queue now respects sibling-mod rank reductions via a reflective soft-dep (`TendedWilds.TechRankCaps`), so it won't over-research a tech past the cap a mod like Tended Wilds applies.

## v1.2.4 (2026-05-22) — Pinnable Dogs & Cats

- **Dogs and Cats are pinnable again** (Cats & Dogs DLC). DogKennel/CatKennel are herd buildings like barns, so pet counts are summed from `dogKennelsRO`/`catKennelsRO` herds the same way as other livestock. (v1.2.3 removed them after the storage-based count read 0; the kennel-herd source gives the real population.)

## v1.2.3 (2026-05-22) — Overlay polish + fixes

### Fixed
- **Overlay z-order / collision** — pinned, tech queue, and company overlays now sit just above the top bar and minimap but below building/villager windows, so they no longer cover those panels. They also collide with the top bar (can't be dragged underneath it), matching FF's native panels.
- **Collapsed pane no longer blocks the map** — reverted to header-only dragging; the panel body is no longer a giant invisible click-blocker, so the area is clickable when collapsed.
- **Collapsed tab flips with grow direction** — when an overlay grows up, its collapsed tab now sits on the header's edge instead of hanging into the minimap.
- **Livestock counts now show** — Cattle/Goats/Chickens/Horses are summed from their actual herds (barns/stables/coops). The previous reading only counted animals in storage (always 0 for placed herds). Dogs/Cats removed from the pin list — FF tracks pets outside the resource system, so they had no countable value.

### Changed
- **Predator alert tint is set once** when the alert appears (removed the per-frame re-evaluation that caused lag spikes). Severity still reflects the threat at the moment it's raised.

## v1.2.2 (2026-05-22) — Overlay controls, grow direction, predator alerts

### Overlays
- **Per-overlay scale sliders** — independent size multipliers for Pinned Resources, Tech Queue, and Company overlays (was one combined slider; existing value is migrated).
- **Per-overlay opacity sliders** — same split for chrome opacity.
- **Grow direction (Up/Down)** — each overlay can grow its list upward from a bottom-anchored header instead of downward (e.g. put the header just above the minimap and grow up). The pinned resource-selection panel flips to match.
- **Higher z-order** — overlays now render above the HUD (top bar / minimap) so their drag handles can't get lost behind them.
- **Whole-header drag** for the company overlay (was banner-only).
- **Per-company position memory + cascade** — each company panel remembers where you put it, and newly opened panels cascade instead of stacking.
- Pinned list sizes to its content (capped to the screen) and grows back to full length; Dogs + Cats added to the Livestock pin list.
- Company roster: cleaner per-class icons (FF's banner weapon icons), crisp native font, opaque header.

### New: FF sprite exporter (Ctrl+Shift+F10)
- Dumps every loaded UI sprite to PNG plus a self-contained searchable HTML gallery, for offline reference when matching FF's UI styling.

### New: Predator alert severity tint
- Tints the "Predators are attacking!" bar by live threat severity — amber for a fox/boar that's merely sighted or fleeing (chased away), red for wolves/bears or an actively engaging fox/boar — and updates live as the threat escalates or backs off. (Pairs with Warden of the Wilds' per-animal icons.)

### Fixed
- Mod-manager UI dump (Shift+F10) no longer aborts on input fields whose caret material isn't initialized.

## v1.2.1 (2026-05-20) — Pinned resources + mod-manager caret fixes

### Fixed
- **Pinned resources showing `0` for items you owned.** Several catalog entries used item IDs that didn't match FF's internal `Item.name` (e.g. `ItemMushrooms`→`ItemMushroom`, `ItemWoodPlanks`→`ItemPlanks`, `ItemCattle`→`ItemCow`), so the lookup returned null and rendered a bare `0`. Two of the default pins (Heavy Tools, Clothing) were broken out of the box. All IDs corrected, and a one-time migration auto-remaps existing saved pins.
- **Mod-manager text fields had invisible caret + selection highlight.** Three stacked bugs: (1) slider numeric readouts added `TMP_InputField` to an already-active object, killing TMP's caret subsystem so no caret was ever created; (2) the scroll viewport's `RectMask2D` left lazily-created carets stuck culled; (3) `customCaretColor` was off so the caret color was ignored. The caret and selection highlight now render reliably on every field, including the first field on first open and after switching mods.

### Changed
- **Per-tier military/equipment pins.** FF tracks weapons/armor/clothing per tier, not as aggregates, so the catalog now lists real items: Simple/Standard/Heavy Weapons, Shields/Hauberks/Platemail, Clothing (Linen Clothes), Hide Coats, Tools, plus Pottery, Books, and Horses. Removed the non-existent Linen/Leather entries.
- Pinned-resource scroll pane is 30% taller.

## v1.2.0 (2026-05-17) — Company Overlay polish

Follow-up release focused on the Company Roster Overlay introduced in v1.1.1.

### Fixed
- **HP bars now visualize health %.** Previously the whole bar shifted color uniformly (no width feedback) — `Image.Type.Filled` was being used without a sprite, which silently ignored `fillAmount`. Bars now shrink in width as HP drops, exposing the dark row background behind them.
- **Right-click move orders work from row-click selection.** Clicking a soldier row used to route through `SelectGameObject`, whose input state didn't accept move/attack orders — the soldier looked selected but right-clicks just deselected. Now routes through `SelectVillager`, the same state the game uses when you click a soldier in the world.

### Changed
- **Full-row HP bar.** The thin bottom strip is gone — the HP bar now fills the entire row height, with name + status text overlaid.
- **HP color gradient.** Green at full → yellow at 50% → red at low, matching the width feedback.
- **Status text per row.** Each row shows the soldier's current activity (Attacking / Retreating / Moving To Destination / Waiting for Command etc.) on the right side, same source FF's barracks UI uses.
- White text with dark outline for clean reads on any HP color.

## v1.1.1 (2026-05-15) — Mod Manager + UI polish

The first full feature release since v1.0.0 (v1.1.0 was a version-bump-only tag).

### New: In-game Mod Settings Panel (F10)
- **Polished UGUI panel** matching FF's native styling — header / mod list / detail / footer with FF sprites, fonts, accent colors. Zoom-in/out animation from the top-right corner over ~1s.
- **Auto-discovery** — finds every MelonLoader mod's preferences automatically. Mods that don't ship an integration still show up with usable controls.
- **KC SettingsAPI** — soft-dep integration pattern. Sister mods (Warden of the Wilds, Manifest Delivery, Tended Wilds, Forageable Transplantation, Rivers Restored, Sovereign Boons, Essential Provisions) register rich metadata (labels, tooltips, min/max ranges, category groupings, visibility predicates) when KC is installed; everything still works standalone when it isn't.
- **Mod list**: alphabetical with display-order overrides, FF-style row sprites, accent stripes, search bar, "Only Changed" filter.
- **Detail panel**: sections, indented sub-settings, scroll position preserved on refresh, per-entry reset arrow that reverts to the value held at panel-open time (not the hard-coded default).
- **Controls**: toggles, sliders with editable numeric inputs (clamped to range), TMP dropdowns for enums, keybind capture buttons, text inputs styled to match FF's Trading Post fields.
- **Tooltips** on hover, on-top z-order (no panel-occlusion).
- **Save & Close vs Close**: Save & Close stays green after save; subsequent edits flip it back to white. Close on a dirty panel prompts "Unsaved changes — close anyway?".
- **Restart-required banner**: amber footer banner appears when a flagged setting changes; reads "Changes have been made that require a restart of Farthest Frontier to take effect."
- **Reload** button re-runs discovery without restarting (useful when iterating on a sibling mod).
- **Legacy IMGUI fallback**: hold Ctrl+F10 (or flip `UseLegacyImguiPanel`) for the original prototype if the UGUI panel misbehaves.
- **Shift+F10** dumps the active UI hierarchy to log — diagnostic tool used for sprite/style discovery.

### New: Company Roster Overlay
- **Shift+Left-Click a company banner** at the bottom of the screen to open a draggable roster panel with one row per soldier.
- HP bar per soldier; single-click selects, double-click centers the camera on them.
- Position persists between sessions.

### New game / map QoL
- **Keep Map Type on Reroll** — dice button rerolls the seed within your selected biome instead of forcing Random.
- **Remember Custom Settings** — Custom Settings panel selections are snapshotted on Confirm and restored on next open.
- **Skip Start-of-Game Cinematic** — go straight to map load; no video, no narration.
- **Sync Map Type with Rivers Restored Preset** — bidirectional sync between FF's terrain selector and RR's RiverPreset.
- **Custom Population Cap** — override the slider stops (200/500/1000/2000) with any value, including in-between (e.g. 250, 750).
- **Ignore Upgrade Population Requirement** — bypass population gates on Town Center tier ups so low-pop saves can still progress.

### Overlay improvements
- Pinned and Tech Queue overlays are now **movable and scalable** — drag the header to reposition; positions persist across sessions and resolutions.
- **Overlay Opacity** and **Overlay UI Scale** sliders (apply live, independent of FF's UI Scale).
- Single **Toggle Overlays** hotkey (default F5) shows/hides both overlays together.
- Right-click on a panel header resets its position.
- FF-native reskin: headers, fonts, accents match the rest of the game.

### Tech tree
- **Per-save tech queue persistence** — each save carries its own queue; loading a save swaps in the right one.
- **Auto-spend banked KP retroactively** — if you accumulate knowledge points before queueing, they spend immediately when a tech is queued.

### Hotkey / input changes
- **Salvage Building (T)** replaces the old Demolish on Delete. Delete still works as a hardcoded fallback that can't be rebound. One-shot migration resets existing installs to T.
- **Delete Build Site (T)** matches the same convention — Delete remains a fixed fallback.
- **Forageable hotkeys**: Harvest (H), Delete (Del), Prioritize (P), Relocate (R — shares the building Relocate key).
- **Pause + F2–F5 allowlist** while a modal/info window is open, so menu/help shortcuts still work.

### Top bar
- **Dismissible resource alerts** — click a "!" warning to silence it until the next month change.
- Staggered + throttled producer-tooltip rebuilds (less stutter when hovering across the top bar).

### Fixes & polish
- Overlays hide correctly on the main menu (gameplay scene is "Frontier", not "Map").
- Cursor no longer disappears after Skip Start-of-Game Cinematic.
- External Custom Settings button syncs on restore.
- Missing-sprite re-probe + chevron retry assignment (handles FF asset-init race conditions).
- Removed obsolete diagnostic logs; pause-on-load delay is now configurable (default 2.5s).

---

## v1.0.0 (2026-04-30) — initial release

### Hotkeys
- Building info window: U / R / E / Del / ← / →
- Build/deconstruction site: Del cancel, P prioritize, O construction-enabled, -/= builder count
- Modal Y/N/Enter/Esc
- Q to queue hovered tech node for auto-research
- Vanilla-shortcut suppression while building/site/modal is open, with Pause + F2–F5 allowlist

### Pinned Resource Overlay
- Pinnable list of any resources, color-coded by category, red on critical
- Settlement-wide max quota display: `X/N` or `X/∞`
- Collapsible to a tab; "All" / "None" bulk select in config
- Selections persist across sessions

### Auto-Research Tech Queue
- Right-click-style Q hotkey adds techs to a queue
- Auto-spend on knowledge-point gain
- Auto-walks prereqs to research the chain back to a `PrereqsMet` ancestor
- Numbered pin on each queued node + active-prereq pin on the node currently being researched
- Main-screen queue panel (collapsible)

### Top-Bar Additions
- Sand / Glass / Coal / Iron resource entries with low-resource alerts
- Laborer / Builder count next to villagers (red when below recommended)
- "PLAN" button linking to the Farthest Frontier Planner web tool
- Producer-breakdown tooltips with monthly production rate

### Build Menu
- Per-tile placed-count badge with `◀` / `▶` cycle arrows that lerp the camera to the next/previous instance
- Auto vertical spacing on tier grids

### Other
- Pause-on-load (configurable, 2.5s post-load delay so the world finishes rendering)
- Trader departure warning
- Manufacturing-time on production tooltips
- Villager and top-bar tooltip enhancements
