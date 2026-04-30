# Keep Clarity

*A UI Enhancement for Farthest Frontier*

A comprehensive quality-of-life mod that adds context-aware hotkeys, a pinnable resource overlay, an auto-research tech queue, deeper top-bar tooltips, and an in-build-menu building counter — without changing any gameplay balance.

## Features

### Hotkeys (configurable)

- **Building info window**: `U` upgrade, `R` relocate, `E` toggle employment, `Del` demolish, `←`/`→` cycle to previous/next building of same type
- **Build / deconstruction site window**: `Del` cancel, `P` toggle prioritized, `O` toggle construction enabled, `−` / `=` decrement / increment builders
- **Y/N/Enter/Esc** confirm / cancel modal dialogs
- **Q** (in tech tree) — queue the hovered tech for auto-research
- **Suppression with allowlist**: while a building/site/modal is open, vanilla shortcuts (B, P, etc.) don't bleed through. Pause and `F2`–`F5` are still let through.

### Pinnable Resource Overlay

- ONI-style sidebar listing any resources you choose
- Auto-shows current count + settlement-wide max quota: `Smoked Meat 396/500`, `∞` if no limit set
- Color-coded by category (Food / Raw Materials / Produced / Usable / Livestock)
- Critical threshold turns the row red
- Collapsible to a thin tab on the screen edge
- "All" / "None" bulk-select buttons in the configuration panel
- Persists pin selections across sessions

### Auto-Research Tech Queue

- Right-click `Q` on any tech node to add it to a queue
- When knowledge points are earned, the mod auto-spends them on the topmost queued tech
- **Auto-walks prereqs**: queue a Tier 4 tech with prereqs unmet and the mod will recursively research the chain back to the deepest `PrereqsMet` ancestor
- Queue is persistent across game sessions
- Visual: numbered gold pin on each queued node, dimmer brown `•` pin on the prereq currently being auto-spent on
- Dedicated main-screen overlay panel showing the queue (collapsible)

### Top-Bar Enhancements

- Added resources right-side: **Sand**, **Glass**, **Coal**, **Iron** (with per-resource icons)
- Added laborer / builder count next to villagers — turns red when below recommended thresholds
- Added "PLAN" button linking to [SageDragoon's Farthest Frontier Planner](https://sagedragoon79.github.io/FarthestFrontierPlanner/)
- **Producer-breakdown tooltips** on Logs / Wood Planks / Firewood / Stone / Brick / Clay / Iron / Sand / Glass / Coal — shows monthly production rate average + per-building-type producer count

### Build Menu

- Each building tile shows a count badge (how many you've placed) + `◀` / `▶` arrows to cycle the camera through your existing instances
- Vertical row spacing auto-adjusts so counters don't overlap

### Other

- **Pause on Load** option (default on) — pauses the game ~2.5s after a save finishes loading
- Escape closes the topmost UI window first; otherwise opens the options menu
- Trader departure warning (configurable days)
- Manufacturing time on production tooltips
- Villager tooltip enhancements

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) (Mono build) for Farthest Frontier.
2. Download `KeepClarity.dll` from [Releases](https://github.com/sagedragoon79/KeepClarity/releases).
3. Copy it to: `<game folder>\Farthest Frontier (Mono)\Mods\`
4. Launch the game.

## Compatibility

- Built for **Farthest Frontier v1.1.0 (Mono)**.
- No save-file changes — safe to add or remove at any time.
- Plays well alongside SageDragoon's other mods (Tended Wilds, Forageable Transplantation, Manifest Delivery, etc.).

## Configuration

All hotkeys and toggles live in `MelonPreferences.cfg` under `[FFUIOverhaul]`:

- `UpgradeHotkey`, `RelocateHotkey`, `ToggleEmployHotkey`, `DemolishHotkey`, `PrioritizeHotkey`, `ConstructionEnabledHotkey`, `IncrementBuildersHotkey`, `DecrementBuildersHotkey`, `CycleBuildingLeftHotkey`, `CycleBuildingRightHotkey`
- `ConfirmHotkey`, `CancelHotkey`
- `ToggleOverlayHotkey`, `PinnedCollapsed`, `PinnedResources`
- `TechResearchQueue`
- `PauseOnLoad`, `TraderWarningDays`

## Author

SageDragoon · [Steam Workshop](https://steamcommunity.com/profiles/sagedragoon79) · [GitHub](https://github.com/sagedragoon79)
