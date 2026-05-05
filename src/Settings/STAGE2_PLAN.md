# Keep Clarity Settings Manager — Stage 2 Plan (UGUI Polish)

Stage 1 (shipped 2026-05-01) is the IMGUI prototype: data layer + auto-discovery
+ functional but unstyled `SettingsWindow.cs`. Stage 2 replaces the IMGUI panel
with a polished UGUI canvas that pulls FF's native sprites/fonts so the panel
feels like Crate built it.

## Goals

1. **Visually superior to FFModSettingsManager.** That's the bar.
2. **Native FF feel** — same panel borders, button styles, scrollbar handles, font.
3. **Master/detail layout** — mod list left, settings right. No flat scroll.
4. **Per-mod identity** — accent color stripe, optional icon, version line.
5. **Search + "only changed" filter** at the top.
6. **Same data layer** — still uses `SettingsRegistry`. Stage 2 is a UI swap, not a rewrite.

## Phase 2A — Sprite / Font Borrowing

Add `Settings/UI/FFNativeAssets.cs`:
- On first access, walks loaded UI to find FF's panel/button/toggle/dropdown sprites
- Looks at `UIWindowManager`, the existing options menu prefab, the trade window
- Caches: panel 9-slice, button normal/hover/pressed, toggle on/off, scrollbar
  thumb, dividers, the medieval serif font (TMP_FontAsset)
- Exposes: `FFNativeAssets.PanelSprite`, `.ButtonStyle`, `.HeaderFont`, etc.

Reflection probe pattern (we already do this for tooltips):
```csharp
var existingPanel = Resources.FindObjectsOfTypeAll<UIBuildingInfoWindow_New>().FirstOrDefault();
var image = existingPanel?.GetComponentInChildren<Image>();
PanelSprite = image?.sprite;
```

If a probe fails, fall back to a hand-styled approximation so we don't crash —
just look slightly less native.

## Phase 2B — Canvas + Layout

Add `Settings/UI/SettingsCanvas.cs`:
- Standalone Canvas with `RenderMode.ScreenSpaceOverlay`, `sortingOrder = 5000`
- Root panel (760×540 default, resizable later) centered, with FF's panel sprite
- Header row: title + close X, draggable
- Body split:
  - Left rail (220px): search box, "only changed" toggle, mod list (vertical)
  - Right pane: detail header (mod name, accent stripe, version), then category list

Use Unity UI's `VerticalLayoutGroup` + `LayoutElement` instead of manual rect math.

## Phase 2C — Mod List Item

`Settings/UI/ModListItem.cs`:
- Background: row sprite, tints to accent color on hover/active
- 4px left edge stripe = accent color
- Icon (32×32) if `info.IconResourcePath` is set
- Two-line: bold display name, dimmer "N settings" subtitle
- Click → set `_selectedModId`, refresh detail pane

## Phase 2D — Setting Row Controls

One factory `SettingControls.Build(SettingEntryRecord rec, RectTransform parent)` that
chooses the right control:
- `bool` → toggle prefab (FF native toggle sprite)
- numeric with Min/Max → slider with live readout label + reset arrow on hover
- numeric without range → input field
- `string` with EnumOptions → dropdown (TMP_Dropdown styled to match)
- `string` plain → input field
- `KeyCode` → "Press a key…" capture button (re-use stage-1 logic)
- enum → dropdown
- list/array → expandable JSON editor (low priority)

Each row:
- Label column (260px, with tooltip on hover — re-use Keep Clarity's existing
  TooltipManager since Plugin.cs already wires it up for the top bar)
- Control column (flex)
- Reset-to-default arrow (only visible when changed)
- Restart-required amber dot if `meta.RestartRequired && IsChanged`

## Phase 2E — Polish Pass

- Smooth scroll: replace default ScrollRect inertia with a tweened version
- Fade-in on open: CanvasGroup alpha 0→1 over 120ms
- Save indicator: small checkmark sprite that flashes after `MelonPreferences.Save()`
- Sticky footer banner if any restart-required setting changed: "Some changes require
  restarting Farthest Frontier"
- Confirmation dialog for "Reset all in category" / "Reset all for mod"
- Keyboard nav: Tab between rows, Enter activates, Escape closes

## Phase 2F — Landing Screen

When the panel opens with no mod selected, show a tile grid instead of empty space:
- 2-3 columns of cards, one per mod
- Each card: large icon, accent color top stripe, mod name, version, one-line
  description, settings count
- Click → drill into that mod's detail
- This is the "Nexus screenshot" feature

## Open Questions

- **Where to anchor the panel?** Floating draggable window (current) vs. embedded
  inside FF's actual options menu as a "Mods" tab? Embedded is more native but
  risks breaking when Crate updates the options screen. Recommend floating for now.
- **Font:** FF uses a serif medieval-style font. Identifier it via reflection on
  any existing TMP_Text and reuse the `TMP_FontAsset`.
- **Resolution:** test at 1080p, 1440p, 4K — use `CanvasScaler` ScaleWithScreenSize.
- **Localization:** FF doesn't have many localized UI strings exposed. Stage 2
  ships English-only; structure leaves room for an `ILocalizer` swap later.

## File layout (proposed)

```
src/Settings/
  SettingsMeta.cs           [done]
  SettingsRegistry.cs       [done]
  SettingsDiscovery.cs      [done]
  SettingsAPI.cs            [done]
  STAGE2_PLAN.md            [this file]
  UI/
    FFNativeAssets.cs       [stage 2A]
    SettingsCanvas.cs       [stage 2B]
    ModListItem.cs          [stage 2C]
    SettingControls.cs      [stage 2D]
    LandingScreen.cs        [stage 2F]
src/UI/
  SettingsWindow.cs         [stage 1 IMGUI — kept as fallback / debug toggle]
```

The IMGUI window stays in the codebase as `Ctrl+F10` debug fallback even after
stage 2 ships — useful when the UGUI version breaks during a game update.

## Estimated effort

- 2A native asset borrowing: 1-2 sessions (lots of reflection probing to verify)
- 2B canvas/layout: 1 session
- 2C mod list: 0.5 session
- 2D setting controls: 1-2 sessions (the slider/keybind/dropdown polish is fiddly)
- 2E polish: 1 session
- 2F landing screen: 0.5 session

Total: ~6-8 sessions of focused work. Can ship 2A-2D as a "v1.1 — UGUI panel"
release and add 2E-2F in a follow-up.
