using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.Settings.UI
{
    /// <summary>
    /// Borrows FF's native UI sprites/fonts at runtime. Uses *targeted by-name*
    /// lookups against the sprite catalog dumped via UIDump (Shift+F10).
    ///
    /// Sprite naming convention in FF:
    /// - <c>IMG_Border*</c> — 9-slice panel/section borders
    /// - <c>BTN_*</c> — button states (UP / hover / pressed)
    /// - <c>IMG_BG*</c> — background fills and shadows
    /// - <c>ICN_*</c> — icons
    ///
    /// Font naming convention:
    /// - <c>building headline text 02 (FePIrm27C) SDF</c> — medieval header
    /// - <c>building info body text (Andada-Regular) SDF</c> — body text
    /// - <c>resource numbers text (Andada-Bold) SDF</c> — values/numbers
    /// </summary>
    public static class FFNativeAssets
    {
        private static bool _probed;
        private static Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static Dictionary<string, TMP_FontAsset> _fonts = new Dictionary<string, TMP_FontAsset>();

        // === Panel / border 9-slices ===
        public static Sprite? PanelBorder => GetSprite("IMG_BorderSimpleThick01B");           // main panel frame
        public static Sprite? PanelBorderThick => GetSprite("IMG_BorderSimpleThick01");       // hairline outer frame
        public static Sprite? PanelBorderLight => GetSprite("IMG_BorderSimpleThickLight01B"); // light variant
        public static Sprite? PanelBorderDark => GetSprite("IMG_BorderSimpleThickDark01B");   // dark variant
        public static Sprite? PanelBorderSimple => GetSprite("IMG_BorderSimpleDark01");       // small simple border
        public static Sprite? PanelHeaderTop => GetSprite("IMG_BorderTopFancy01B");           // fancy top header
        // Carved warm-gradient backdrop FF uses behind the hairline frame on its
        // info windows (BuildingInfo, BuildsiteInfo, GraveyardInfo). Slice
        // values 54,28,27,49 — corners are ornate, so panels narrower than ~120
        // px lose the carved detail.
        public static Sprite? PanelBorderCarved => GetSprite("IMG_Border_BuildingInfo01");
        // Cycling chevron arrow used on FF's building-info top bar. 20x20
        // simple sprite — rotate via RectTransform.localEulerAngles to point
        // up/down/left.
        public static Sprite? ArrowChevron => GetSprite("IMG_ArrowRightTransparent02");

        // === Dividers ===
        public static Sprite? DividerDirectional => GetSprite("IMG_BorderDivider-Directional01");
        public static Sprite? DividerSimple => GetSprite("IMG_BorderDivider-Simple01");
        public static Sprite? SliderDivider => GetSprite("IMG_SliderDivider01");
        // Gold double-line accent FF uses under category names in the build menu.
        // Sliced 29,0,29,0 — fixed-size end caps with a stretchable middle.
        public static Sprite? AccentDouble => GetSprite("IMG_BtnAccent-Double01_UP");

        // === Buttons ===
        public static Sprite? ButtonFancy => GetSprite("BTN_BorderFancyFocus01_UP");   // main menu-style 220x40
        public static Sprite? ButtonFocus => GetSprite("BTN_BorderFocus01_UP");        // primary
        public static Sprite? ButtonFocusSecondary => GetSprite("BTN_BorderFocus02_UP");
        public static Sprite? ButtonGeneric => GetSprite("BTN_Border02_UP");
        public static Sprite? CategoryTabSelected => GetSprite("BTN_Category01_SelectedToggle");
        public static Sprite? HotkeyBadge => GetSprite("BTN_Hotkeys01_UP");

        // === Close buttons ===
        // FF's standard window close — round X button, used on every named
        // window (Buildings, Building Info, BuildSite Info, etc.). 32x32.
        public static Sprite? ExitRoundButton => GetSprite("BTN_ExitRound_UP");
        // Wider close-area frames (used on trading post sub-panels)
        public static Sprite? CloseButton => GetSprite("IMG_BorderCloseButton01");        // 72x32
        public static Sprite? CloseButton2x => GetSprite("IMG_BorderCloseButton2x01");    // ~100x32
        public static Sprite? CloseButton2xMed => GetSprite("IMG_BorderCloseButton2x_Med01");
        public static Sprite? CloseButton2xLg => GetSprite("IMG_BorderCloseButton2x_LG01");
        public static Sprite? CancelConstruction => GetSprite("BTN_CancelConstruction01_UP"); // demolish-style

        // === Shadows ===
        public static Sprite? ShadowSoft => GetSprite("IMG_BGShadowSoft01");
        public static Sprite? ShadowThickSoft => GetSprite("IMG_BGShadowThickSoft01");
        public static Sprite? ShadowThick => GetSprite("IMG_BGShadowThick01");

        // === Misc ===
        public static Sprite? GlowOverlay => GetSprite("IMG_BorderGlowOverlay01");
        public static Sprite? RingFancy => GetSprite("IMG_RingFancy01_Transparent");

        // === Slider parts ===
        public static Sprite? SliderTrack => GetSprite("IMG_BorderSimpleDark01");  // 9-slice 5,5,5,5
        public static Sprite? SliderFill => GetSprite("IMG_Slider02");             // 9-slice 2,2,2,2
        public static Sprite? SliderHandle => GetSprite("BTN_Slider01_UP");        // 24x24 diamond

        // === Reset / back button ===
        // FF's circular orange reload-arrow icon. Used in the New Settlement
        // screen as "Back Button" but visually it's a reload/reset symbol.
        public static Sprite? ResetIcon => GetSprite("BTN_BackRound01_UP");

        // === Checkbox parts (matches FF Settings menu toggles) ===
        // 20x20 dark-bordered box with a 16x16 orange checkmark when on.
        public static Sprite? CheckboxBox => GetSprite("IMG_BorderSimpleDark01");
        public static Sprite? CheckboxCheck => GetSprite("BTN_Checkbox01_DOWN");

        // === Indicator icons ===
        public static Sprite? IconWarning => GetSprite("ICN_Warning01_Transparent");
        public static Sprite? IconInfo => GetSprite("ICN_InfoRound01_Transparent");
        public static Sprite? IconCheckmark => GetSprite("ICN_Checkmark01_Transparent");

        // === Dropdown parts (matches FF Settings menu Color Blindness/Startup Monitor) ===
        public static Sprite? DropdownBg => GetSprite("IMG_BorderSimpleThickDark01B");
        public static Sprite? DropdownArrow => GetSprite("IMG_ArrowRightFancy01");
        public static Sprite? DropdownMask => GetSprite("UIMask");

        // === Fonts ===
        public static TMP_FontAsset? FontHeader => GetFont("building headline text 02 (FePIrm27C) SDF")
                                                   ?? GetFont("building headline text") // partial match fallback
                                                   ?? AnyFont();
        // Window title font FF uses on its modal popups (e.g. "TRANSFER ITEMS
        // TO AND FROM TRADING POST"). Bold + SmallCaps at size 16.
        public static TMP_FontAsset? FontTitle => GetFont("NotoSerifJP-Regular SDF")
                                                  ?? GetFont("NotoSerifJP-Regular")
                                                  ?? FontHeader;
        public static TMP_FontAsset? FontBody => GetFont("building info body text (Andada-Regular) SDF")
                                                 ?? GetFont("Andada-Regular") // partial fallback
                                                 ?? AnyFont();
        public static TMP_FontAsset? FontNumbers => GetFont("resource numbers text (Andada-Bold) SDF")
                                                    ?? GetFont("Andada-Bold")
                                                    ?? AnyFont();
        public static TMP_FontAsset? FontCriticalInfo => GetFont("critical info text (Andada-Bold) SDF")
                                                         ?? FontNumbers;

        // === Palette (FF-tuned) ===
        // Based on observed colors in the dump — most FF panel sprites use white
        // tint and the sprite itself carries the look. These tints are for our
        // own added elements (rail backgrounds, hover states, etc.).
        public static Color TextPrimary { get; private set; } = new Color(0.95f, 0.92f, 0.85f, 1f);
        public static Color TextDim { get; private set; } = new Color(0.70f, 0.66f, 0.58f, 1f);
        public static Color TextChanged { get; private set; } = new Color(1.00f, 0.90f, 0.55f, 1f);
        public static Color TextMuted { get; private set; } = new Color(0.55f, 0.50f, 0.42f, 1f);
        public static Color WarningAmber { get; private set; } = new Color(0.95f, 0.70f, 0.20f, 1f);

        // Panel tints — apply to PanelBorder sprites. White lets the sprite show
        // through unmodified; lower values darken.
        public static Color PanelTintDefault { get; private set; } = new Color(1f, 1f, 1f, 1f);
        public static Color PanelTintHeader { get; private set; } = new Color(0.78f, 0.74f, 0.66f, 1f);
        public static Color PanelTintRail { get; private set; } = new Color(0.85f, 0.80f, 0.72f, 1f);
        public static Color RowHover { get; private set; } = new Color(1.10f, 1.05f, 0.95f, 1f);  // brighten
        public static Color RowActive { get; private set; } = new Color(0.70f, 0.55f, 0.30f, 1f); // amber tint
        public static Color DividerTint { get; private set; } = new Color(1f, 1f, 1f, 1f);

        public static void EnsureProbed()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                ProbeFonts();
                ProbeSprites();
                Log($"FFNativeAssets: probe complete — {_sprites.Count} sprites, {_fonts.Count} fonts cached");
                Log($"  PanelBorder={(PanelBorder != null ? "✓" : "MISSING")}, ButtonFancy={(ButtonFancy != null ? "✓" : "MISSING")}, FontHeader={(FontHeader != null ? "✓" : "MISSING")}, FontBody={(FontBody != null ? "✓" : "MISSING")}");
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"FFNativeAssets probe threw: {ex.Message}");
            }
        }

        private static void ProbeFonts()
        {
            var texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(includeInactive: true);
            foreach (var t in texts)
            {
                if (t == null || t.font == null) continue;
                var name = t.font.name;
                if (string.IsNullOrEmpty(name)) continue;
                if (!_fonts.ContainsKey(name))
                    _fonts[name] = t.font;
            }
        }

        private static void ProbeSprites()
        {
            // Cache all named sprites we encounter. Use Resources.FindObjectsOfTypeAll
            // because some panels may not be active in scene yet, but their Image
            // components have sprites assigned that we still want.
            var images = Resources.FindObjectsOfTypeAll<Image>();
            foreach (var img in images)
            {
                if (img == null || img.sprite == null) continue;
                var name = img.sprite.name;
                if (string.IsNullOrEmpty(name)) continue;
                if (!_sprites.ContainsKey(name))
                    _sprites[name] = img.sprite;
            }
        }

        private static Sprite? GetSprite(string name)
        {
            if (!_probed) EnsureProbed();
            return _sprites.TryGetValue(name, out var s) ? s : null;
        }

        private static TMP_FontAsset? GetFont(string name)
        {
            if (!_probed) EnsureProbed();
            if (_fonts.TryGetValue(name, out var f)) return f;
            // partial match fallback
            foreach (var kv in _fonts)
                if (kv.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            return null;
        }

        private static TMP_FontAsset? AnyFont()
        {
            if (!_probed) EnsureProbed();
            foreach (var f in _fonts.Values) return f;
            return TMP_Settings.defaultFontAsset;
        }

        // Convenience: a 1px solid texture for fallback backgrounds.
        public static Texture2D MakeSolidTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public static Sprite SpriteFromColor(Color c)
        {
            var t = MakeSolidTex(c);
            return Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }

        private static void Log(string msg)
        {
            try { FFUIOverhaulMod.Log.Msg($"[Settings/2A] {msg}"); } catch { }
        }
    }
}
