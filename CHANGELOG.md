# Keep Clarity — Changelog

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
