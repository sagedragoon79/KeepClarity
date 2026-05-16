# Keep Clarity — Changelog

## v1.1.0 (2026-05-15) — Mod Manager + UI polish

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
