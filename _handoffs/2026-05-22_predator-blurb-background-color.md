# Handoff — Predator-alert blurb: per-severity BACKGROUND color

**Date:** 2026-05-22
**From:** WotW thread (Phase 1 predator-alert overhaul)
**For:** KC thread (UI internals)
**Status:** RESOLVED in KC (v1.2.2). See `src/Patches/PredatorBlurbBackground.cs`.

## Resolution (2026-05-22, KC v1.2.2)
The wide bar is a `UIBlurb` (not `UICriticalBlurb`); its panel fill is the
child Image named "Standard" (IMG_BorderSimpleThick02B, light-tintable). KC
patches `UIBlurb.Init` + ticks a tracked set, recoloring "Standard" + "OnHover".
Severity is computed LIVE and self-contained — detected by container TYPE
(`PredatorCombatBlurbContainer`, which survives FF's consolidation reset that
wipes WotW's clone) and read from the retained `enemyUnitsInCombat`
(DamageableComponent.isRetreating) + `blurbType` (sighted vs engaging):
wolf/bear = red; fox/boar = amber if sighted or fleeing, red if engaging; flips
live as threat escalates/de-escalates. No hard dependency on WotW.

Open follow-up (WotW side): FF's consolidation resets blurbDefinition to the
vanilla sprite, which can revert WotW's animal ICON to default. KC's coloring
survives it; WotW would need to re-apply its clone on consolidate to keep the
icon.

---
(original handoff below)


## What the feature is

Replace FF's generic red "Predators are attacking!" blurb with a per-animal
version: the animal's own sprite + a severity color (amber = low threat,
red = real threat). Built in WotW as `Patches/PredatorAlertPatches.cs`.

## What already works (WotW side — leave alone)

- **Animal sprite icon** — confirmed working in-game (fox/wolf/boar all show
  their correct head sprite instead of the default bear).
- **Severity classification** — confirmed correct:
  - Fox: amber when spotted, red when engaging (attacking chickens/dog)
  - Boar: amber when spotted or fleeing (`damageableComp.isRetreating`), red when attacking
  - Wolf/Bear: always red
  - Signal = vanilla `BlurbType`: `CombatTargetSighted_Predator` (spotted) vs
    `Combat_Predator` (engaging, raised by `AggressiveAnimalTargeted{Villager,
    Building,Pet,Livestock}`).

## What does NOT work — the ask for KC

**The blurb panel background stays black; the only thing we can color is the
thin border, which is too subtle to distinguish amber from red.**

Goal: tint the **panel background fill** (or a prominent element) amber vs red
per severity so threat level is readable at a glance.

## Everything mapped so far (FF v1.1.2 decompile line refs)

Decompile: `ilspycmd` with `DOTNET_ROLL_FORWARD=Major` (only .NET 8 installed),
output was `/tmp/ff112/Assembly-CSharp.decompiled.cs` (~511k lines).

### The blurb data model
- `BlurbDefinition : ScriptableObject` (~line 301932)
  - `smallCriticalIcon` (private Sprite) → `criticalIcon` getter — drives the
    **mini icon** on the stacked-alert row only.
  - `blurbEntries` / `entries` (List<BlurbDefinitionEntry>)
- `BlurbDefinitionEntry` (~line 301905) — PUBLIC fields:
  - `public Sprite icon;` ← **the MAIN panel icon** (this is what we set)
  - `public Color backgroundColor = Color.clear;` ← **HOVER tint ONLY**, NOT
    the persistent background (this was the trap — setting it does nothing
    until you hover)

### What reads what (render)
- Main icon: `image.sprite = selectedBlurbEntry.icon;` (~line 306310, in
  `UpdateTextAndIcons`)
- Mini icon: `miniIconImage.sprite = blurbDefinition.criticalIcon;` (~306312)
- Hover color: `hoverColor = blurbDefinition.GetBlurbEntry(0).backgroundColor;`
  (~306367) — **only on hover**
- Icon (another path): `iconImage.sprite = blurbContainer.blurbDefinition.criticalIcon`
  (~309666) — gated behind `merchantDefinition == null`

### UICriticalBlurb (the widget) (~line 309532)
Private members:
- `pulseImage` (Image) — animated glow
- `outlineImage` (Image) — the border (toggled by `showOutline`)
- `defaultColor`, `noAlphaColor` (Color) — captured at `Awake` from
  `pulseImage.color` (lines ~309604); the pulse lerps between them in
  `Update`.
- `blurbContainer` (BlurbContainer)
- `Init(BlurbContainer, ...)` — bind point (two overloads)

**Key fact:** the persistent border/pulse color is **baked into the prefab**
(`defaultColor = pulseImage.color` at Awake), NOT driven by the blurb
definition. So you can't set it via the BlurbDefinition; you must recolor the
live widget.

## What WotW already does (so KC doesn't duplicate)

In `WardenOfTheWilds/Patches/PredatorAlertPatches.cs`:
1. Clones `uiAssetMap.predatorCombatBlurb` per (animal, severity), sets the
   clone's `smallCriticalIcon` + each `entry.icon` = animal sprite, and each
   `entry.backgroundColor` = severity color (the latter is the hover-only one).
2. Postfix on `PredatorCombatBlurbContainer` ctor swaps the instance's
   `blurbDefinition` + `selectedBlurbEntry` backing fields to the clone.
3. Postfix on `UICriticalBlurb.Init` recolors `outlineImage.color`,
   `pulseImage.color`, `defaultColor`, `noAlphaColor` to the severity color.
   **This is the thin border that's too subtle.**

Clone names are `WotW_PredatorBlurb_{Fox|Wolf|Bear|Boar}:{amber|red}` — KC can
detect WotW clones by that prefix and read severity from the `:amber`/`:red`
suffix on `blurbContainer.blurbDefinition.name`.

## What KC needs to find/do

1. **Locate the panel background-fill Image** in the UICriticalBlurb prefab
   hierarchy (the dark rounded rectangle behind the text). It's a child Image
   not exposed as a named field we found — likely needs walking the widget's
   transform children, or it's a serialized field we haven't identified
   (check the full UICriticalBlurb field list ~309532–309630).
2. **Tint it per severity** in the same `UICriticalBlurb.Init` postfix WotW
   already has — OR KC takes over the whole color concern and WotW drops its
   thin-border recolor (coordinate so we don't both patch Init).
3. Consider a **bolder treatment** than a tint: a colored left-edge bar, a
   stronger border weight, or a semi-transparent color wash over the panel,
   since a subtle background tint on a dark panel may still be hard to read.

## Coordination note

WotW owns the icon + severity classification (working, keep). KC owns the
color/visual treatment. Cleanest split: WotW exposes severity via the clone
name suffix (already does), KC reads it and handles ALL coloring — at which
point WotW can remove its `InitPostfix` border recolor to avoid double-patching
`UICriticalBlurb.Init`. Decide in the KC thread and tell the WotW thread to
strip its recolor if so.
