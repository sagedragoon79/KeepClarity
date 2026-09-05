using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using FFUIOverhaul.UI;
using FFUIOverhaul.Utils;
using FFUIOverhaul.TechTree;
using FFUIOverhaul.Settings;

[assembly: MelonInfo(typeof(FFUIOverhaul.FFUIOverhaulMod), "Keep Clarity", "1.6.0", "sagedragoon79")]
[assembly: MelonGame("Crate Entertainment", "Farthest Frontier")]

namespace FFUIOverhaul
{
    public class FFUIOverhaulMod : MelonMod
    {
        public static FFUIOverhaulMod Instance { get; private set; } = null!;
        public static MelonLogger.Instance Log => Instance.LoggerInstance;

        private MelonPreferences_Category _prefs = null!;

        // Hotkey preferences
        public static MelonPreferences_Entry<KeyCode> ReportsHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> UpgradeHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> RelocateHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ToggleEmployHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> DemolishHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CycleBuildingLeftHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CycleBuildingRightHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> PrioritizeHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ConstructionEnabledHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> DecrementBuildersHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> IncrementBuildersHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> DeleteBuildSiteHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ForageableHarvestHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ForageableDeleteHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ForageablePrioritizeHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ConfirmHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CancelHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> ToggleOverlayHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> MoveWorkAreaHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CullForWoodHotkey { get; private set; } = null!;
        // Crop Field UI
        public static MelonPreferences_Entry<KeyCode> CropCopyHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CropPasteHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CropExpandHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CropClearHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<KeyCode> CropSalvageHotkey { get; private set; } = null!;

        // Trader warning
        public static MelonPreferences_Entry<int> TraderWarningDays { get; private set; } = null!;

        // Pinned overlay
        public static MelonPreferences_Entry<string> PinnedResourcesJson { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> PinnedCollapsed { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PinnedOverlayPosX { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PinnedOverlayPosY { get; private set; } = null!;
        public static MelonPreferences_Entry<float> TechQueueOverlayPosX { get; private set; } = null!;
        public static MelonPreferences_Entry<float> TechQueueOverlayPosY { get; private set; } = null!;
        public static MelonPreferences_Entry<float> OverlayOpacity { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PinnedOverlayOpacity { get; private set; } = null!;
        public static MelonPreferences_Entry<float> TechQueueOverlayOpacity { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayOpacity { get; private set; } = null!;
        public static MelonPreferences_Entry<UI.OverlayGrowDirection> PinnedGrowDirection { get; private set; } = null!;
        public static MelonPreferences_Entry<UI.OverlayGrowDirection> TechQueueGrowDirection { get; private set; } = null!;
        public static MelonPreferences_Entry<UI.OverlayGrowDirection> CompanyGrowDirection { get; private set; } = null!;
        public static MelonPreferences_Entry<float> OverlayUIScale { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PinnedOverlayScale { get; private set; } = null!;
        public static MelonPreferences_Entry<float> TechQueueOverlayScale { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayScale { get; private set; } = null!;

        // Tech research queue
        public static MelonPreferences_Entry<string> TechResearchQueue { get; private set; } = null!;

        // Pause on load
        public static MelonPreferences_Entry<bool> PauseOnLoad { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PauseOnLoadDelay { get; private set; } = null!;

        // Double-click a save on the start screen to load without confirmation
        public static MelonPreferences_Entry<bool> DoubleClickLoad { get; private set; } = null!;
        public static MelonPreferences_Entry<float> DoubleClickLoadWindow { get; private set; } = null!;

        // Planner button
        public static MelonPreferences_Entry<bool> ShowPlannerButton { get; private set; } = null!;

        // New game menu QoL
        public static MelonPreferences_Entry<bool> KeepMapTypeOnReroll { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> RememberCustomSettings { get; private set; } = null!;
        public static MelonPreferences_Entry<string> RememberCustomSettingsSnapshot { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> SkipNewMapIntro { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> SyncMapTypeWithRiverPreset { get; private set; } = null!;
        public static MelonPreferences_Entry<int> CustomPopulationCap { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> IgnoreUpgradePopulationRequirement { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> BridgeAnywhere { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> DismissibleResourceAlerts { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> EnableCompanyOverlay { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayPosX { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayPosY { get; private set; } = null!;
        public static MelonPreferences_Entry<string> CompanyOverlayPositions { get; private set; } = null!;

        // Build priority (hijacks the builders X/X arrows as 1-9 priority)
        public static MelonPreferences_Entry<bool> EnableBuildPriority { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildPriorityWeight { get; private set; } = null!;

        // Build queue overlay (top 10 build sites by priority)
        public static MelonPreferences_Entry<bool> EnableBuildQueueOverlay { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildQueueOverlayPosX { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildQueueOverlayPosY { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildQueueOverlayScale { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildQueueOverlayOpacity { get; private set; } = null!;
        public static MelonPreferences_Entry<UI.OverlayGrowDirection> BuildQueueGrowDirection { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> BuildQueueCollapsed { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> BuildQueueShowSalvage { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> BuildQueueShowExcavation { get; private set; } = null!;

        // Overlay snapping (drag a panel near another overlay / minimap / screen edge → snaps)
        public static MelonPreferences_Entry<bool> SnapOverlaysEnabled { get; private set; } = null!;
        public static MelonPreferences_Entry<float> SnapThresholdPx { get; private set; } = null!;

        // Auto-shift the native building info window left so the build menu never covers it
        public static MelonPreferences_Entry<bool> EnableInfoWindowDock { get; private set; } = null!;

        // Building colour variety (per-instance tint)
        public static MelonPreferences_Entry<bool> EnableBuildingVariety { get; private set; } = null!;
        public static MelonPreferences_Entry<float> BuildingVarietyIntensity { get; private set; } = null!;

        // Crisp Mode (native post-process: CAS sharpen + vibrance)
        public static MelonPreferences_Entry<bool> EnableCrispMode { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CrispSharpness { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CrispVibrance { get; private set; } = null!;

        // Localization: force a UI language for the mod panel ("Auto" = follow the game)
        public static MelonPreferences_Entry<string> LanguageOverride { get; private set; } = null!;

        // Road length counter: live tile count while dragging a road
        public static MelonPreferences_Entry<bool> EnableRoadLengthCounter { get; private set; } = null!;

        // Blueprints (Master Mason): capture a layout and stamp it elsewhere
        public static MelonPreferences_Entry<bool> EnableBlueprints { get; private set; } = null!;
        public static MelonPreferences_Entry<string> CaptureHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<string> StampHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<string> PanelHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<string> RotateHotkey { get; private set; } = null!;

        // Forage Calendar (seasonality bar under the top-bar season strip + in-season popup line)
        public static MelonPreferences_Entry<bool> EnableForageCalendar { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> ForageCalendarAlwaysShow { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> EnableInSeasonReadout { get; private set; } = null!;

        // Settings panel
        public static MelonPreferences_Entry<KeyCode> SettingsPanelHotkey { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> SettingsVerboseLog { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> UseLegacyImguiPanel { get; private set; } = null!;
        private bool _settingsRegistered;

        // Runtime state
        public static GameObject? UITopBarIronEntry;
        public static GameObject? UITopBarSandEntry;
        public static GameObject? UITopBarGlassEntry;
        public static GameObject? UITopBarCoalEntry;
        public static GameObject? UITopBarLaborerBuilderEntry;
        public static GameObject? UITopBarPlannerButton;
        private PinnedResourceOverlay? _overlay;
        private TechQueueMainOverlay? _techQueueOverlay;
        private BuildQueueOverlay? _buildQueueOverlay;

        public void RefreshTechQueueOverlay() => _techQueueOverlay?.RefreshDisplay();

        // Per-frame cached context flags — read by HotkeySuppressPatch to avoid per-call overhead
        public static bool FrameModalVisible { get; private set; }
        public static bool FrameBuildingWindowActive { get; private set; }
        public static bool FrameBuildSiteWindowActive { get; private set; }
        public static bool FrameForageableActive { get; private set; }
        public static bool FrameCropfieldActive { get; private set; }


        public override void OnInitializeMelon()
        {
            Instance = this;

            _prefs = MelonPreferences.CreateCategory("FFUIOverhaul");

            ReportsHotkey = _prefs.CreateEntry("ReportsHotkey", KeyCode.CapsLock,
                display_name: "Reports Hotkey",
                description: "Hotkey to toggle the 12-month report window");

            UpgradeHotkey = _prefs.CreateEntry("UpgradeHotkey", KeyCode.U,
                display_name: "Upgrade Hotkey",
                description: "Hotkey to upgrade selected building");

            RelocateHotkey = _prefs.CreateEntry("RelocateHotkey", KeyCode.R,
                display_name: "Relocate",
                description: "Hotkey to relocate selected building. Also relocates forageables when Tended Wilds is installed.");

            ToggleEmployHotkey = _prefs.CreateEntry("ToggleEmployHotkey", KeyCode.E,
                display_name: "Toggle Building Production",
                description: "Hotkey to start/stop production on the selected building (toggles the employment checkbox).");

            DemolishHotkey = _prefs.CreateEntry("DemolishHotkey", KeyCode.T,
                display_name: "Salvage Building",
                description: "Hotkey to salvage the selected building. Delete also works as a fixed fallback and can't be rebound.");

            // One-shot migration. The DemolishHotkey pref kept its old key
            // when renamed (so customizations could persist), but existing
            // users still had its legacy default of Delete saved. Now Delete
            // is the hardcoded fallback and T is the configurable default —
            // force-reset to T once so the panel reflects the new convention.
            var _demolishHotkeyMigratedToT = _prefs.CreateEntry("DemolishHotkeyMigratedToT", false,
                display_name: "(internal) Salvage hotkey migrated to T",
                description: "Internal — one-shot migration flag for the v1.1.0 Demolish→Salvage rename",
                is_hidden: true);
            if (!_demolishHotkeyMigratedToT.Value)
            {
                DemolishHotkey.Value = KeyCode.T;
                _demolishHotkeyMigratedToT.Value = true;
            }

            CycleBuildingLeftHotkey = _prefs.CreateEntry("CycleBuildingLeftHotkey", KeyCode.LeftArrow,
                display_name: "Cycle Building Left",
                description: "Cycle to the previous building of the same type (mirrors the < button at top of the info panel)");

            CycleBuildingRightHotkey = _prefs.CreateEntry("CycleBuildingRightHotkey", KeyCode.RightArrow,
                display_name: "Cycle Building Right",
                description: "Cycle to the next building of the same type (mirrors the > button at top of the info panel)");

            PrioritizeHotkey = _prefs.CreateEntry("PrioritizeHotkey", KeyCode.P,
                display_name: "Prioritize",
                description: "Hotkey to toggle the Prioritized checkbox on a build/deconstruction site");

            ConstructionEnabledHotkey = _prefs.CreateEntry("ConstructionEnabledHotkey", KeyCode.O,
                display_name: "Construction Toggle",
                description: "Hotkey to toggle the Construction Enabled checkbox on a build/deconstruction site");

            DeleteBuildSiteHotkey = _prefs.CreateEntry("DeleteBuildSiteHotkey", KeyCode.T,
                display_name: "Delete Build Site",
                description: "Hotkey to cancel a build / deconstruction site. Delete also works as a fixed fallback and can't be rebound.");

            DecrementBuildersHotkey = _prefs.CreateEntry("DecrementBuildersHotkey", KeyCode.Minus,
                display_name: "Decrement Builders (-)",
                description: "Hotkey to remove a builder from a build/deconstruction site");

            IncrementBuildersHotkey = _prefs.CreateEntry("IncrementBuildersHotkey", KeyCode.Equals,
                display_name: "Increment Builders (+)",
                description: "Hotkey to add a builder to a build/deconstruction site");

            ConfirmHotkey = _prefs.CreateEntry("ConfirmHotkey", KeyCode.Y,
                display_name: "Confirm",
                description: "Hotkey to confirm a Yes/No dialog. Enter also works as a fixed fallback and can't be rebound.");

            CancelHotkey = _prefs.CreateEntry("CancelHotkey", KeyCode.N,
                display_name: "Cancel",
                description: "Hotkey to cancel a Yes/No dialog. Esc also works as a fixed fallback and can't be rebound.");

            ForageableHarvestHotkey = _prefs.CreateEntry("ForageableHarvestHotkey", KeyCode.H,
                display_name: "Harvest",
                description: "Hotkey to flag the selected forageable / bush / fruit tree for harvest. No-op if the forageable has no harvest action.");

            ForageableDeleteHotkey = _prefs.CreateEntry("ForageableDeleteHotkey", KeyCode.Delete,
                display_name: "Delete",
                description: "Hotkey to flag the selected forageable for clearing. No-op if the forageable has no clear action.");

            MoveWorkAreaHotkey = _prefs.CreateEntry("MoveWorkAreaHotkey", KeyCode.Q,
                display_name: "Move Work Area",
                description: "With a building selected, start moving its work area (hunters, foresters, herbalists, etc.). Same as the in-panel button; does nothing for buildings without a work area.");

            CullForWoodHotkey = _prefs.CreateEntry("CullForWoodHotkey", KeyCode.C,
                display_name: "Cull Fruit Tree for Wood",
                description: "With a fruit tree selected, mark it to be cut down for wood (the native 'Cull For Wood' action).");

            CropCopyHotkey = _prefs.CreateEntry("CropCopyHotkey", KeyCode.C,
                display_name: "Crop Field: Copy Settings", description: "Copy this crop field's settings (native Copy button).");
            CropPasteHotkey = _prefs.CreateEntry("CropPasteHotkey", KeyCode.V,
                display_name: "Crop Field: Paste Settings", description: "Paste copied crop field settings (native Paste button).");
            CropExpandHotkey = _prefs.CreateEntry("CropExpandHotkey", KeyCode.E,
                display_name: "Crop Field: Expand", description: "Expand the crop field (native Expand button).");
            CropClearHotkey = _prefs.CreateEntry("CropClearHotkey", KeyCode.X,
                display_name: "Crop Field: Clear Selected Crop", description: "Clear the selected crop (native Clear button).");
            CropSalvageHotkey = _prefs.CreateEntry("CropSalvageHotkey", KeyCode.T,
                display_name: "Crop Field: Salvage", description: "Salvage the crop field (native Salvage button).");

            ForageablePrioritizeHotkey = _prefs.CreateEntry("ForageablePrioritizeHotkey", KeyCode.P,
                display_name: "Prioritize",
                description: "Hotkey to toggle the Prioritized checkbox on the selected forageable. No-op if the forageable has no priority toggle.");

            ToggleOverlayHotkey = _prefs.CreateEntry("ToggleOverlayHotkey", KeyCode.F5,
                display_name: "Toggle Overlays",
                description: "Hotkey to show/hide both the pinned resource overlay and the tech queue overlay together.");

            TraderWarningDays = _prefs.CreateEntry("TraderWarningDays", 5,
                display_name: "Trader Departure Warning Days",
                description: "Days before a trader leaves to show a 'Trader Departing' notification (0 disables).");

            PinnedResourcesJson = _prefs.CreateEntry("PinnedResources", "",
                display_name: "Pinned Resources",
                description: "Internal — pinned item list (managed by overlay UI)",
                is_hidden: true);

            PinnedCollapsed = _prefs.CreateEntry("PinnedCollapsed", false,
                display_name: "Pinned Overlay Collapsed",
                description: "True when the pinned resource overlay is collapsed to a tab");

            // Saved as 0..1 normalized canvas-space pivot positions so the
            // panels return to the right spot across resolutions. Defaults
            // place pinned at top-right and tech queue at top-left, matching
            // the original anchor layout. Right-click on the drag handle
            // resets to these defaults. Marked is_hidden so they don't
            // clutter the settings panel — they're internal state, not
            // user-editable settings (drag the header to change).
            PinnedOverlayPosX = _prefs.CreateEntry("PinnedOverlayPosX", 0.995f,
                display_name: "Pinned Overlay X", description: "Internal — drag the panel header to change.", is_hidden: true);
            PinnedOverlayPosY = _prefs.CreateEntry("PinnedOverlayPosY", 0.95f,
                display_name: "Pinned Overlay Y", description: "Internal — drag the panel header to change.", is_hidden: true);
            TechQueueOverlayPosX = _prefs.CreateEntry("TechQueueOverlayPosX", 0.005f,
                display_name: "Tech Queue Overlay X", description: "Internal — drag the panel header to change.", is_hidden: true);
            TechQueueOverlayPosY = _prefs.CreateEntry("TechQueueOverlayPosY", 0.78f,
                display_name: "Tech Queue Overlay Y", description: "Internal — drag the panel header to change.", is_hidden: true);

            OverlayOpacity = _prefs.CreateEntry("OverlayOpacity", 0.92f,
                display_name: "Overlay Opacity",
                description: "Legacy combined overlay opacity — kept only to seed the per-overlay opacities below for existing users.",
                is_hidden: true);
            // Per-overlay opacities, seeded from the old combined value.
            PinnedOverlayOpacity = _prefs.CreateEntry("PinnedOverlayOpacity", OverlayOpacity.Value,
                display_name: "Pinned Overlay Opacity",
                description: "How opaque the Pinned Resources panel chrome is. Text/buttons stay readable. Applies live.");
            TechQueueOverlayOpacity = _prefs.CreateEntry("TechQueueOverlayOpacity", OverlayOpacity.Value,
                display_name: "Tech Queue Overlay Opacity",
                description: "How opaque the Tech Queue panel chrome is. Text/buttons stay readable. Applies live.");
            CompanyOverlayOpacity = _prefs.CreateEntry("CompanyOverlayOpacity", OverlayOpacity.Value,
                display_name: "Company Overlay Opacity",
                description: "How opaque the Company roster panel body is. Header / text stay solid. Applies live.");

            PinnedGrowDirection = _prefs.CreateEntry("PinnedGrowDirection", UI.OverlayGrowDirection.Down,
                display_name: "Pinned Overlay Grow Direction",
                description: "Whether the Pinned Resources list grows Down from the header (default) or Up (header on bottom). Applies live.");
            TechQueueGrowDirection = _prefs.CreateEntry("TechQueueGrowDirection", UI.OverlayGrowDirection.Down,
                display_name: "Tech Queue Grow Direction",
                description: "Whether the Tech Queue list grows Down from the header (default) or Up (header on bottom). Applies live.");
            CompanyGrowDirection = _prefs.CreateEntry("CompanyGrowDirection", UI.OverlayGrowDirection.Down,
                display_name: "Company Overlay Grow Direction",
                description: "Whether the Company roster grows Down from the header (default) or Up (header on bottom). Applies live.");

            OverlayUIScale = _prefs.CreateEntry("OverlayUIScale", 1.0f,
                display_name: "Overlay UI Scale",
                description: "Legacy combined overlay scale — kept only to seed the per-overlay scales below for existing users.",
                is_hidden: true);
            // Per-overlay scales. Default each to the old combined value so a
            // user who'd set OverlayUIScale keeps it; new entries persist
            // independently thereafter.
            PinnedOverlayScale = _prefs.CreateEntry("PinnedOverlayScale", OverlayUIScale.Value,
                display_name: "Pinned Overlay Scale",
                description: "Pinned Resources panel size multiplier. 1.0 = normal. Applies live.");
            TechQueueOverlayScale = _prefs.CreateEntry("TechQueueOverlayScale", OverlayUIScale.Value,
                display_name: "Tech Queue Overlay Scale",
                description: "Tech Queue panel size multiplier. 1.0 = normal. Applies live.");
            CompanyOverlayScale = _prefs.CreateEntry("CompanyOverlayScale", OverlayUIScale.Value,
                display_name: "Company Overlay Scale",
                description: "Company roster panel size multiplier. 1.0 = normal. Applies live.");

            TechResearchQueue = _prefs.CreateEntry("TechResearchQueue", "",
                display_name: "Tech Research Queue",
                description: "Internal — per-save tech queue state (managed by the tech tree overlay).",
                is_hidden: true);

            PauseOnLoad = _prefs.CreateEntry("PauseOnLoad", false,
                display_name: "Pause on Load",
                description: "Automatically pause the game once it finishes loading a save");

            ShowPlannerButton = _prefs.CreateEntry("ShowPlannerButton", true,
                display_name: "Show Planner Button",
                description: "Show the PLAN icon in the top bar that opens SageDragoon's Farthest Frontier Planner in your browser. Restart required to take effect.");

            KeepMapTypeOnReroll = _prefs.CreateEntry("KeepMapTypeOnReroll", true,
                display_name: "Keep Map Type on Reroll",
                description: "When enabled, the dice button in the New Game menu rerolls the seed within your selected terrain type (Lush Forest, Cold Mountains, etc.) instead of forcing it to Random. Disable to restore vanilla behavior.");

            RememberCustomSettings = _prefs.CreateEntry("RememberCustomSettings", false,
                display_name: "Remember Custom Settings",
                description: "When enabled, your selections in the New Game → Custom Settings panel are saved on Confirm and restored automatically the next time you open that panel.");

            RememberCustomSettingsSnapshot = _prefs.CreateEntry("RememberCustomSettingsSnapshot", "",
                display_name: "(internal) Custom Settings Snapshot",
                description: "Internal — saved values for the Remember Custom Settings feature. Edit only if you know the format.",
                is_hidden: true);

            SkipNewMapIntro = _prefs.CreateEntry("SkipNewMapIntro", false,
                display_name: "Skip Start-of-Game Cinematic",
                description: "Skip the cinematic video that plays after starting a new settlement. Goes straight to map load — no video, no narration.");

            BridgeAnywhere = _prefs.CreateEntry("BridgeAnywhere", false,
                display_name: "Bridges Anywhere (no water / any height)",
                description: "Lifts the 'must be over water' placement rule on bridges AND the 'snap to water level' height rule. Lets you span dry ravines and high-bank rivers without terraforming. The bridge sits at the start/end cells' terrain height. Off by default.");

            SyncMapTypeWithRiverPreset = _prefs.CreateEntry("SyncMapTypeWithRiverPreset", true,
                display_name: "Sync Map Type with Rivers Restored Preset",
                description: "When Rivers Restored is installed, the New Settlement default terrain matches your RR River Preset (Plains, Alpine Valleys, etc.) and changing the terrain in-panel updates RR's preset. Custom / Random / unknown values are not synced.");

            CustomPopulationCap = _prefs.CreateEntry("CustomPopulationCap", 0,
                display_name: "Custom Population Cap",
                description: "Override the population cap with any value. 0 = use whatever you set in the game's slider (200/500/1000/etc). Anything else = exact cap. Useful for picking values between the slider stops (e.g. 250, 750).");

            IgnoreUpgradePopulationRequirement = _prefs.CreateEntry("IgnoreUpgradePopulationRequirement", false,
                display_name: "Ignore Upgrade Population Requirement",
                description: "Bypass the population requirement on building upgrades (Town Center tier ups, etc). Useful if you set a low Custom Population Cap and would otherwise be unable to progress.");

            DismissibleResourceAlerts = _prefs.CreateEntry("DismissibleResourceAlerts", true,
                display_name: "Dismissible Resource Alerts",
                description: "Click a top-bar resource '!' warning to dismiss it until the next month change. Common vanilla annoyance — you know about it and don't need the popup nagging you for the rest of the month.");

            EnableCompanyOverlay = _prefs.CreateEntry("EnableCompanyOverlay", true,
                display_name: "Company Roster Overlay",
                description: "Shift+Left-Click a company banner (bottom of screen) to open a draggable roster panel showing each soldier with an HP bar. Single-click a row to select the soldier; double-click to center the camera on them.");
            CompanyOverlayPosX = _prefs.CreateEntry("CompanyOverlayPosX", 0.5f,
                display_name: "Company Overlay X", description: "Internal — drag the banner sprite to change.", is_hidden: true);
            CompanyOverlayPosY = _prefs.CreateEntry("CompanyOverlayPosY", 0.7f,
                display_name: "Company Overlay Y", description: "Internal — drag the banner sprite to change.", is_hidden: true);
            CompanyOverlayPositions = _prefs.CreateEntry("CompanyOverlayPositions", "",
                display_name: "Company Overlay Positions",
                description: "Internal — per-company saved panel positions, 'name=x,y;…'. Drag a company header to change.", is_hidden: true);

            DoubleClickLoad = _prefs.CreateEntry("DoubleClickLoad", false,
                display_name: "Double-Click to Load",
                description: "Double-click a save (start screen or in-game Load menu) to load it immediately, skipping the confirmation. A single click still shows the confirmation.");
            DoubleClickLoadWindow = _prefs.CreateEntry("DoubleClickLoadWindow", 0.35f,
                display_name: "Double-Click Speed (seconds)",
                description: "Max gap between the two clicks. Also the tiny delay before a single click shows the confirmation.");

            PauseOnLoadDelay = _prefs.CreateEntry("PauseOnLoadDelaySeconds", 2.5f,
                display_name: "Pause on Load Delay (seconds)",
                description: "How many seconds to wait after the game finishes loading before pausing. Gives lighting/post-processing time to settle so the player doesn't see a black 'void' frame.");

            EnableBuildPriority = _prefs.CreateEntry("EnableBuildPriority", false,
                display_name: "Build Priority",
                description: "Repurposes each build site's builder up/down arrows as a 1-9 PRIORITY. Builders serve higher-priority sites first, and every site can take its full number of builders (no per-site cap). Applies live. NOTE: turning this off after setting priorities can leave odd builder counts until readjusted.");
            BuildPriorityWeight = _prefs.CreateEntry("BuildPriorityWeight", 1000f,
                display_name: "Build Priority Strength",
                description: "How strongly priority overrides distance. Higher = priority dominates (a far high-priority site beats a near low-priority one). 1000 = priority decides order, distance only breaks ties. Applies live.");

            SnapOverlaysEnabled = _prefs.CreateEntry("SnapOverlaysEnabled", true,
                display_name: "Snap Overlays",
                description: "When dragging a Keep Clarity overlay, snap its edges to the other overlays, the minimap, and the screen edges.");
            SnapThresholdPx = _prefs.CreateEntry("SnapThresholdPx", 12f,
                display_name: "Snap Distance",
                description: "How close (pixels) an edge must get before it snaps. 0 disables snapping. Applies live.");

            EnableInfoWindowDock = _prefs.CreateEntry("EnableInfoWindowDock", false,
                display_name: "Shift Building Info Off Build Menu (experimental)",
                description: "When the Build menu is open, slide the building info window left so it never hides behind the menu. Experimental — FF re-asserts the window position each frame, so it can visibly snap back. Off by default.");

            EnableBuildQueueOverlay = _prefs.CreateEntry("EnableBuildQueueOverlay", false,
                display_name: "Build Queue Overlay",
                description: "Show a movable panel listing the top 10 active build sites by Build Priority, highest first. Drag to move; scale/opacity below.");
            BuildQueueShowSalvage = _prefs.CreateEntry("BuildQueueShowSalvage", true,
                display_name: "Build Queue: Include Salvage",
                description: "Also list buildings being deconstructed and abandoned camps being salvaged in the Build Queue, tagged [SALV].");
            BuildQueueShowExcavation = _prefs.CreateEntry("BuildQueueShowExcavation", true,
                display_name: "Build Queue: Include Excavation",
                description: "Also list ruins being excavated for relics in the Build Queue, tagged [EXC].");
            BuildQueueOverlayPosX = _prefs.CreateEntry("BuildQueueOverlayPosX", 0.005f,
                display_name: "Build Queue Overlay X", description: "Internal — drag the panel to change.", is_hidden: true);
            BuildQueueOverlayPosY = _prefs.CreateEntry("BuildQueueOverlayPosY", 0.55f,
                display_name: "Build Queue Overlay Y", description: "Internal — drag the panel to change.", is_hidden: true);
            BuildQueueOverlayScale = _prefs.CreateEntry("BuildQueueOverlayScale", 1.0f,
                display_name: "Build Queue Overlay Scale", description: "Build Queue panel size multiplier. Applies live.");
            BuildQueueOverlayOpacity = _prefs.CreateEntry("BuildQueueOverlayOpacity", 0.92f,
                display_name: "Build Queue Overlay Opacity", description: "Build Queue panel opacity. Applies live.");
            BuildQueueGrowDirection = _prefs.CreateEntry("BuildQueueGrowDirection", UI.OverlayGrowDirection.Down,
                display_name: "Build Queue Grow Direction", description: "Build Queue: list grows Down from header (default) or Up (header at bottom).");
            BuildQueueCollapsed = _prefs.CreateEntry("BuildQueueCollapsed", false,
                display_name: "Build Queue Collapsed", description: "Internal — collapsed state.", is_hidden: true);

            EnableBuildingVariety = _prefs.CreateEntry("EnableBuildingVariety", false,
                display_name: "Building Color Variety",
                description: "Gives each structure a subtle, unique weathered tint so identical building types don't all look clone-stamped. Deterministic per building (stable across reloads). Applies live.");
            BuildingVarietyIntensity = _prefs.CreateEntry("BuildingVarietyIntensity", 0.6f,
                display_name: "Variety Intensity",
                description: "Range of color variation between buildings. 0 = all identical, 1 = deliberately too much (so you can see the extreme and dial back). Default 0.6 is a comfortable middle. Applies live.");

            EnableCrispMode = _prefs.CreateEntry("EnableCrispMode", false,
                display_name: "Crisp Mode",
                description: "Native sharpen + vibrance post-process (a lightweight, built-in alternative to a ReShade CAS preset). Applied to the world only — your HUD stays untouched. Applies live.");
            CrispSharpness = _prefs.CreateEntry("CrispSharpness", 0.75f,
                display_name: "Crisp Sharpness",
                description: "Contrast-adaptive sharpening strength. 0 = soft, 1 = sharp. Applies live.");
            CrispVibrance = _prefs.CreateEntry("CrispVibrance", 0.6f,
                display_name: "Crisp Vibrance",
                description: "Color pop / saturation boost, weighted toward less-saturated pixels. 0 = none. Applies live.");

            LanguageOverride = _prefs.CreateEntry("LanguageOverride", "Auto",
                display_name: "Mod Panel Language",
                description: "Language for mod settings text. 'Auto' follows Farthest Frontier's own language setting. Set it explicitly if the panel ever shows the wrong language.");

            EnableBlueprints = _prefs.CreateEntry("EnableBlueprints", true,
                display_name: "Blueprints",
                description: "Capture a group of placed buildings and stamp the layout elsewhere as build orders.");
            CaptureHotkey = _prefs.CreateEntry("BlueprintCaptureHotkey", "Shift+C",
                display_name: "Capture Layout",
                description: "Arms capture: click one corner, move, click again to copy the buildings inside.");
            StampHotkey = _prefs.CreateEntry("BlueprintStampHotkey", "Shift+V",
                display_name: "Stamp Layout",
                description: "Arms stamping with the selected blueprint. Click to place.");
            PanelHotkey = _prefs.CreateEntry("BlueprintPanelHotkey", "Shift+B",
                display_name: "Blueprint Library",
                description: "Opens the blueprint library: name and save captures, pick one to stamp.");
            RotateHotkey = _prefs.CreateEntry("BlueprintRotateHotkey", "Tab",
                display_name: "Rotate Blueprint",
                description: "Rotates the blueprint 90 degrees while stamping.");

            EnableRoadLengthCounter = _prefs.CreateEntry("EnableRoadLengthCounter", true,
                display_name: "Road Length Counter",
                description: "While dragging a road, show how many grid squares it will cover next to the cursor.");

            EnableForageCalendar = _prefs.CreateEntry("EnableForageCalendar", true,
                display_name: "Forage Season Bar",
                description: "Gantt-style bar under the top-bar season strip showing when each forageable on your map is in season. Shows with the game's own season popup (hover the strip, or pin it with FF's info toggle).");
            ForageCalendarAlwaysShow = _prefs.CreateEntry("ForageCalendarAlwaysShow", false,
                display_name: "Always Show Season Bar",
                description: "Keep the forage season bar visible at all times instead of only with the season popup.");
            EnableInSeasonReadout = _prefs.CreateEntry("EnableInSeasonReadout", true,
                display_name: "In-Season Readout",
                description: "Adds a live 'In season: ...' line to the date/weather popup text, updating as the calendar advances.");

            SettingsPanelHotkey = _prefs.CreateEntry("SettingsPanelHotkey", KeyCode.F10,
                display_name: "Settings Panel Hotkey",
                description: "Hotkey to open the Keep Clarity settings panel for all installed mods");

            SettingsVerboseLog = _prefs.CreateEntry("SettingsVerboseLog", false,
                display_name: "Verbose Settings Log",
                description: "Log every settings registration claim and discovery decision. Off by default; turn on only when debugging the panel.");

            UseLegacyImguiPanel = _prefs.CreateEntry("UseLegacyImguiPanel", false,
                display_name: "Use Legacy IMGUI Panel",
                description: "Stage 1 fallback. When false (default), F10 opens the polished UGUI canvas. When true, F10 opens the original IMGUI prototype. Switch to true if the UGUI panel misbehaves.");

            TechAutoQueue.Load();

            // Note: MelonMod.HarmonyInit() (called by MelonLoader BEFORE OnInitializeMelon)
            // already calls HarmonyInstance.PatchAll(). Calling it again here would
            // register every patch twice — we observed double-firing of postfixes
            // ([BuildingsWindow] Awake postfix logs appearing twice) when this was
            // duplicated. So we don't call it again.
            LoggerInstance.Msg("Keep Clarity initialized");
        }

        /// <summary>IMGUI pass — currently only the blueprint library panel.</summary>
        public override void OnGUI()
        {
            if (EnableBlueprints.Value) Blueprints.BlueprintPanel.OnGUI();
        }

        public override void OnUpdate()
        {
            if (!Application.isPlaying) return;

            // Settings panel hotkey works on the main menu too — it doesn't
            // need a GameManager and the panel is useful pre-load (e.g. flipping
            // a master toggle that requires a restart anyway).
            HandleSettingsPanelHotkey();

            // Runs on the start scene (no GameManager yet) and the gameplay
            // scene: flushes a held single-click on a save into the normal
            // confirmation once the double-click window lapses.
            Patches.DoubleClickLoad.Tick();

            // Localization: arms once I2's sources exist (menu included), loads
            // drop-in packs from Mods/KCLocalization/, registers pending terms.
            Localization.KcLoc.Tick();
            UI.RoadLengthCounter.Tick();

            if (EnableBlueprints.Value)
            {
                Blueprints.CaptureInput.OnUpdate();
                Blueprints.PanelInput.OnUpdate();
                Blueprints.StampInput.OnUpdate();
                Blueprints.BlueprintPanel.Tick();
            }

            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return;

            // Compute context flags once per frame from the input state machine —
            // it's the source of truth for which input context owns the keyboard.
            // GameObject-based detection is unreliable: panels often hide via
            // CanvasGroup rather than SetActive, leaving activeInHierarchy=true.
            var stateName = InputStateHelper.GetCurrentInputStateType()?.Name;
            FrameModalVisible = stateName == "Input_ModalWindow";
            FrameCropfieldActive = stateName == "Input_CropfieldInfo";

            // Resolve the selected GameObject's component types once per frame —
            // both flags need it, no point in calling reflection twice.
            Building? selectedBuilding = null;
            BuildSiteResource? selectedBuildSite = null;
            bool selectedForageable = false;
            bool selectedTreeOrStone = false;
            bool selectedFruitTree = false;
            if (stateName == "Input_SelectGameObject")
            {
                var selectedObj = GetSelectedGameObject(gm);
                if (selectedObj != null)
                {
                    selectedBuilding = selectedObj.GetComponent<Building>()
                        ?? selectedObj.GetComponentInParent<Building>();
                    selectedBuildSite = selectedObj.GetComponent<BuildSiteResource>()
                        ?? selectedObj.GetComponentInParent<BuildSiteResource>();
                    if (selectedBuilding == null && selectedBuildSite == null)
                    {
                        selectedForageable = ForageableActions.IsForageable(selectedObj);
                        if (!selectedForageable)
                            selectedTreeOrStone = ForageableActions.IsTreeOrStone(selectedObj);
                        if (!selectedForageable && !selectedTreeOrStone)
                            selectedFruitTree = ForageableActions.IsFruitTree(selectedObj);
                    }
                }
            }
            FrameBuildingWindowActive = selectedBuilding != null;
            FrameBuildSiteWindowActive = selectedBuildSite != null;
            // Forageables, tree/stone deposits and fruit trees all drive the same
            // suppression: their info window owns Del/P/H/C so FF's own binds must
            // not also fire.
            FrameForageableActive = selectedForageable || selectedTreeOrStone || selectedFruitTree;


            // Order matters: modal handler first (consumes Y/N/Enter/Esc in dialogs)
            if (FrameModalVisible)
            {
                HandleModalHotkeys(gm);
                return; // Modal is the ONLY thing that responds — no other hotkeys
            }

            HandleReportsHotkey(gm);
            if (selectedBuilding != null) HandleBuildingHotkeys(selectedBuilding, gm);
            else if (selectedBuildSite != null) HandleBuildSiteHotkeys(selectedBuildSite, gm);
            else if (selectedForageable) HandleHarvestableHotkeys(canRelocate: true);
            else if (selectedTreeOrStone) HandleHarvestableHotkeys(canRelocate: false);
            else if (selectedFruitTree) HandleFruitTreeHotkeys();
            else if (FrameCropfieldActive) HandleCropfieldHotkeys();
            HandleEscapeKey(gm);
            HandleOverlayToggle();
            _overlay?.Tick();
            _techQueueOverlay?.Tick();
            _buildQueueOverlay?.Tick();
            TechQueueInput.Tick();
            HandlePauseOnLoad(gm);
            Patches.DismissibleResourceAlerts.Tick();
            UI.CompanyOverlayManager.Tick();
            Patches.InfoWindowDock.Tick();
            UI.ForageCalendarBar.Tick();
            UI.CrispMode.Tick();
            UI.BuildingVariety.Tick();
        }

        private void HandlePauseOnLoad(GameManager gm)
        {
            if (_pauseOnLoadDone || !PauseOnLoad.Value) return;
            // Gate on gameFullyInitialized, NOT gameReadyToPlay: the latter is already true
            // during a NEW game's Town Center placement screen, so pausing there traps the
            // player (you can't unpause during placement). gameFullyInitialized flips only
            // once the simulation actually starts — after the TC is placed, or after a save
            // finishes loading. (It's the same flag FF's own sim loop gates on.)
            if (!GameManager.gameFullyInitialized) return;

            // Tick the delay only once the sim is live. Lighting and post-processing take a
            // bit to settle after that flag flips, so pausing immediately puts the player in
            // a black "void" frame. Use unscaled time so a paused-by-something-else state
            // doesn't freeze the timer.
            _pauseOnLoadTimer += Time.unscaledDeltaTime;
            if (_pauseOnLoadTimer < PauseOnLoadDelay.Value) return;

            gm.TogglePause();
            _pauseOnLoadDone = true;
        }

        private bool _pauseOnLoadDone;
        private float _pauseOnLoadTimer;

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            // FF's gameplay scene is "Frontier" (not "Map" — assumed wrong
            // earlier). Any scene other than Frontier is non-gameplay (main
            // menu, loading splashes, credits) and the overlays must hide.
            bool isMap = sceneName == "Frontier";

            // Any scene change invalidates a held save-click's menu ref.
            Patches.DoubleClickLoad.ResetState();

            if (isMap)
            {
                // Construct lazily — both overlays use DontDestroyOnLoad so they
                // survive scene transitions. Re-creating on every Map load would
                // stack canvases (visible as a "double background") and leak.
                if (_overlay == null) _overlay = new PinnedResourceOverlay();
                _overlay.Visible = true;

                if (_techQueueOverlay == null) _techQueueOverlay = new TechQueueMainOverlay();
                _techQueueOverlay.Visible = true;

                if (_buildQueueOverlay == null) _buildQueueOverlay = new BuildQueueOverlay();
                _buildQueueOverlay.Visible = true; // clear the toggle-hidden latch each load, like the siblings above

                if (EnableBlueprints.Value)
                {
                    Blueprints.BlueprintCapture.OnMapLoaded();
                    Blueprints.BlueprintStamp.OnMapLoaded();
                    Blueprints.CaptureInput.OnMapLoaded();
                    Blueprints.PanelInput.OnMapLoaded();
                    Blueprints.StampInput.OnMapLoaded();
                    Blueprints.BlueprintPanel.OnMapLoaded();
                }

                Patches.InfoWindowDock.ResetState(); // clear stale dock baseline on each save load
                UI.ForageCalendarBar.ResetState();   // scene objects died with the old map; rebuild lazily

                _pauseOnLoadDone = false; // re-arm pause-on-load for this Map session
                _pauseOnLoadTimer = 0f;

                // Snapshot every setting's value as of this save load — the
                // baseline for the cyan "reload required" indicator. Re-capturing
                // each load is what makes that "!" CLEAR after a reload (the
                // applied value becomes the new baseline). ReloadRequired
                // features apply on load, so this is the value the running game
                // is actually using.
                Settings.SettingsRegistry.CaptureMapLoadValues();

                // Re-load the tech queue for whichever save just opened. The
                // pref is per-save scoped (key = SaveManager.activeSaveFileName),
                // so a load-from-menu has to reread to swap in the right queue.
                TechAutoQueue.EnsureLoadedForCurrentSave();

                // Apply custom population cap if set. Has to happen after
                // SettingsManager is initialized for the gameplay scene.
                Patches.PopulationCapOverride.ApplyIfSet();

                // Settings discovery — runs once per session, here rather than
                // OnInitializeMelon because MelonLoader throttles log output
                // during init, and other mods may register prefs lazily.
                if (!_settingsRegistered)
                {
                    InitSettingsManager();
                    _settingsRegistered = true;
                }
                SettingsWindow.EnsureInstance();

                // Manually patch UIVillagerWindow once the game types are loaded
                // (self-guarded; appends EP education/mastery bonuses to the panel).
                Patches.VillagerWorkInfoPatch.Initialize();
                Patches.VillagerDiseaseInfoPatch.Initialize();
                Patches.ForageCalendarPatch.Initialize();
            }
            else
            {
                // Hide overlays on the main menu / any non-gameplay scene.
                if (_overlay != null) _overlay.Visible = false;
                if (_techQueueOverlay != null) _techQueueOverlay.Visible = false;
                UI.CrispMode.OnSceneChanged(); // world camera is gone; drop our ref so we re-attach next map
                UI.BuildingVariety.OnSceneChanged();

            }
        }

        private void HandleReportsHotkey(GameManager gm)
        {
            if (Input.GetKeyDown(ReportsHotkey.Value))
            {
                gm.uiManager.windowManager.ToggleReportWindow();
            }
        }

        private void HandleSettingsPanelHotkey()
        {
            bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrlHeldNow = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            // Ctrl+Alt+F10 → export the localization template (every settings
            // string in the fleet, pack format) to Mods/KCLocalization/. The
            // translator kit — see Localization.KcLoc.
            if (Input.GetKeyDown(SettingsPanelHotkey.Value) && altHeld && ctrlHeldNow)
            {
                Localization.KcLoc.ExportTemplate();
                return;
            }

            // Ctrl+Shift+F10 → export every loaded sprite to PNG + an HTML
            // gallery under MelonLoader/KC_SpriteExport_*/. Offline reference so
            // we stop guessing sprite names. Checked before the Shift-only dump.
            if (Input.GetKeyDown(SettingsPanelHotkey.Value) && shiftHeld && ctrlHeldNow)
            {
                Settings.UI.SpriteExport.Run();
                return;
            }

            // Ctrl+Shift+F9 → dump the selected building's render structure
            // (renderers, shaders, colour properties, material sharing) so we
            // can design per-building colour variety from real data.
            if (Input.GetKeyDown(KeyCode.F9) && shiftHeld && ctrlHeldNow)
            {
                Utils.BuildingInspector.Run();
                return;
            }

            // Ctrl+Shift+F8 → dump the entire tech tree (every node's effects,
            // values, ranks, prereqs) to log + file. Ground-truth tech->effect map.
            if (Input.GetKeyDown(KeyCode.F8) && shiftHeld && ctrlHeldNow)
            {
                Utils.TechTreeDumper.Run();
                return;
            }

            // Shift+F10 → dump every active UI element to MelonLoader/Logs/.
            // Used to study FF's native styling so we can write targeted asset
            // lookups for the UGUI panel (Stage 2A).
            if (Input.GetKeyDown(SettingsPanelHotkey.Value) && shiftHeld)
            {
                Settings.UI.UIDump.Run();
                return;
            }

            if (Input.GetKeyDown(SettingsPanelHotkey.Value))
            {
                // Lazy discovery — ensures the panel is populated even when
                // first opened on the main menu before any save is loaded.
                if (!_settingsRegistered)
                {
                    InitSettingsManager();
                    _settingsRegistered = true;
                }

                bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool useImgui = UseLegacyImguiPanel.Value || ctrlHeld;
                if (useImgui)
                {
                    // Make sure UGUI panel isn't simultaneously open
                    Settings.UI.SettingsCanvas.Close();
                    SettingsWindow.Toggle();
                }
                else
                {
                    // Make sure IMGUI panel isn't simultaneously open
                    SettingsWindow.Close();
                    Settings.UI.SettingsCanvas.Toggle();
                }
            }
        }

        private void InitSettingsManager()
        {
            try
            {
                // Register Keep Clarity's own prefs FIRST so the category claim
                // is in place before discovery walks MelonPreferences. Without
                // this ordering, auto-discovery would create a phantom
                // "FFUIOverhaul" mod row alongside the proper "Keep Clarity" one.
                RegisterOwnSettings();

                int discovered = SettingsDiscovery.RunDiscovery();
                LoggerInstance.Msg($"[Settings] Auto-discovered {discovered} entries from MelonPreferences");

                SettingsDiscovery.DumpToLog();
            }
            catch (System.Exception ex)
            {
                LoggerInstance.Error($"[Settings] InitSettingsManager failed: {ex}");
            }
        }

        private void RegisterOwnSettings()
        {
            const string id = "KeepClarity";
            const string name = "Keep Clarity";

            SettingsAPI.RegisterMod(id, name,
                description: "UI quality-of-life: enhanced tooltips, pinnable resource overlay, building hotkeys, settings panel.",
                version: "1.0.0",
                accentRgb: new[] { 0.45f, 0.65f, 0.40f, 1f },
                order: 0);

            // Hotkeys — pass an auto-incrementing Order so the panel displays
            // entries in registration order rather than alphabetical-by-key.
            // ModDetailPanel sorts within-category by Meta.Order ascending.
            int _order = 0;
            int Next() => (_order += 10);
            void K(string cat, MelonPreferences_Entry<KeyCode> entry, string label, string? tip = null) =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip, Order = Next() });
            void R(string cat, MelonPreferences_Entry<bool> entry, string label, string? tip = null) =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip, Order = Next() });
            void RF(string cat, MelonPreferences_Entry<float> entry, string label, float min, float max, string? tip = null, System.Func<bool>? visibleWhen = null) =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip, Min = min, Max = max, Order = Next(), VisibleWhen = visibleWhen });
            void RI(string cat, MelonPreferences_Entry<int> entry, string label, int min, int max, string? tip = null) =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip, Min = min, Max = max, Order = Next() });
            void RE<T>(string cat, MelonPreferences_Entry<T> entry, string label, string? tip = null) where T : struct =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip, Order = Next() });

            // ── Reports & Panels ─────────────────────────────────────────
            _order = 0;
            K("Hotkeys — Reports & Panels", ReportsHotkey, "Toggle Reports", "Open/close the 12-month report window");
            K("Hotkeys — Reports & Panels", ToggleOverlayHotkey, "Toggle Overlays", "Show/hide the pinned, tech queue, build queue, and military company overlays");
            K("Hotkeys — Reports & Panels", SettingsPanelHotkey, "Open Settings Panel", "Opens this very window");

            // ── Overlay Settings (pinned + tech queue overlays) ─────────
            _order = 0;
            R("Overlay Settings", PinnedCollapsed, "Pinned Overlay Starts Collapsed",
                "Pinned overlay opens collapsed to a tab");
            R("Overlay Settings", EnableCompanyOverlay, "Company Roster Overlay",
                "Shift+Left-Click a company banner (bottom of screen) opens a draggable roster panel; Ctrl+Left-Click opens that company's barracks.");
            R("Overlay Settings", EnableInfoWindowDock, "Shift Building Info Off Build Menu (experimental)",
                "When the Build menu is open, slide the building info window left so the menu never covers it. Restores when the menu closes.");
            RF("Overlay Settings", PinnedOverlayOpacity, "Pinned Overlay Opacity", 0.05f, 1f,
                "How opaque the Pinned Resources chrome is. Text/buttons stay readable. Applies live.");
            RF("Overlay Settings", TechQueueOverlayOpacity, "Tech Queue Overlay Opacity", 0.05f, 1f,
                "How opaque the Tech Queue chrome is. Text/buttons stay readable. Applies live.");
            RF("Overlay Settings", CompanyOverlayOpacity, "Company Overlay Opacity", 0.05f, 1f,
                "How opaque the Company roster body is. Header/text stay solid. Applies live.");
            RF("Overlay Settings", PinnedOverlayScale, "Pinned Overlay Scale", 0.5f, 2f,
                "Pinned Resources panel size multiplier. Independent of FF's UI Scale. Applies live.");
            RF("Overlay Settings", TechQueueOverlayScale, "Tech Queue Overlay Scale", 0.5f, 2f,
                "Tech Queue panel size multiplier. Independent of FF's UI Scale. Applies live.");
            RF("Overlay Settings", CompanyOverlayScale, "Company Overlay Scale", 0.5f, 2f,
                "Company roster panel size multiplier. Independent of FF's UI Scale. Applies live.");
            RE("Overlay Settings", PinnedGrowDirection, "Pinned Grow Direction",
                "Pinned Resources: list grows Down from header (default) or Up.");
            RE("Overlay Settings", TechQueueGrowDirection, "Tech Queue Grow Direction",
                "Tech Queue: list grows Down from header (default) or Up.");
            RE("Overlay Settings", CompanyGrowDirection, "Company Grow Direction",
                "Company roster: grows Down from header (default) or Up.");

            // ── Build Priority ───────────────────────────────────────────
            _order = 0;
            R("Build Priority", EnableBuildPriority, "Build Priority",
                "Repurpose the build site builder arrows as a 1-9 priority. Builders serve higher-priority sites first; every site can take its full builder count.");
            RF("Build Priority", BuildPriorityWeight, "Priority Strength", 0f, 3000f,
                "How strongly priority beats distance. 1000 = priority decides order. Applies live.",
                visibleWhen: () => EnableBuildPriority.Value);
            R("Build Priority", EnableBuildQueueOverlay, "Build Queue Overlay",
                "Movable on-screen panel listing the top 10 build sites by priority, highest first.");
            R("Build Priority", BuildQueueShowSalvage, "Build Queue: Include Salvage",
                "Also list buildings being deconstructed and abandoned camps being salvaged, tagged [SALV].");
            R("Build Priority", BuildQueueShowExcavation, "Build Queue: Include Excavation",
                "Also list ruins being excavated for relics, tagged [EXC].");
            RF("Build Priority", BuildQueueOverlayScale, "Build Queue Scale", 0.5f, 2f,
                "Build Queue panel size. Applies live.", visibleWhen: () => EnableBuildQueueOverlay.Value);
            RF("Build Priority", BuildQueueOverlayOpacity, "Build Queue Opacity", 0.05f, 1f,
                "Build Queue panel opacity. Applies live.", visibleWhen: () => EnableBuildQueueOverlay.Value);
            RE("Build Priority", BuildQueueGrowDirection, "Build Queue Grow Direction",
                "List grows Down from the header (default) or Up (header at bottom).");

            // ── Snapping ─────────────────────────────────────────────────
            _order = 0;
            R("Snapping", SnapOverlaysEnabled, "Snap Overlays",
                "When you drag a Keep Clarity overlay, snap it into alignment with the other overlays, the minimap, and the screen edges.");
            RF("Snapping", SnapThresholdPx, "Snap Distance", 0f, 60f,
                "How close (pixels) an edge must get before it snaps. 0 turns snapping off. Applies live.",
                visibleWhen: () => SnapOverlaysEnabled.Value);

            // ── Building Variety ─────────────────────────────────────────
            // Live — applied/cleared via MaterialPropertyBlock each scan.
            _order = 0;
            R("Building Variety", EnableBuildingVariety, "Building Color Variety",
                "Each structure gets a subtle unique weathered tint so identical building types don't look clone-stamped. Deterministic per building (stable across reloads).");
            RF("Building Variety", BuildingVarietyIntensity, "Variety Intensity", 0f, 1f,
                "Range of variation between buildings. 0 = identical, 1 = widest (still tasteful). Applies live.",
                visibleWhen: () => EnableBuildingVariety.Value);

            // ── Crisp Mode ───────────────────────────────────────────────
            // All live (read every frame in OnRenderImage) — no restart/reload flags.
            _order = 0;
            R("Crisp Mode", EnableCrispMode, "Crisp Mode",
                "Native sharpen + vibrance post-process — a lightweight built-in alternative to a ReShade CAS preset. World only; HUD untouched.");
            RF("Crisp Mode", CrispSharpness, "Sharpness", 0f, 1f,
                "Contrast-adaptive sharpening strength. 0 = soft, 1 = sharp. Applies live.",
                visibleWhen: () => EnableCrispMode.Value);
            RF("Crisp Mode", CrispVibrance, "Vibrance", 0f, 1f,
                "Color pop / saturation boost, weighted toward less-saturated pixels. Applies live.",
                visibleWhen: () => EnableCrispMode.Value);

            // ── Building ─────────────────────────────────────────────────
            _order = 0;
            R("Building", EnableRoadLengthCounter, "Road Length Counter",
                "While dragging a road, show how many grid squares it will cover next to the cursor. Applies live.");
            R("Building", EnableBlueprints, "Blueprints",
                "Capture a group of placed buildings and stamp the layout elsewhere as build orders. Opens with the Blueprint Library hotkey.");
            // Chord strings ("Shift+C"), not KeyCode, so these can carry modifiers
            // and stay clear of the game's own single-key bindings. KC renders a
            // string pref as a text field, which is enough for typing a chord.
            void RBK(MelonPreferences_Entry<string> entry, string label, string tip) =>
                SettingsAPI.Register(id, name, "Building", entry,
                    new SettingsMeta
                    {
                        Label = label,
                        Tooltip = tip,
                        Order = Next(),
                        VisibleWhen = () => EnableBlueprints.Value,
                    });

            RBK(CaptureHotkey, "Blueprint: Capture Layout",
                "Arms capture: click one corner, move, click again to copy the buildings inside. A key or chord, e.g. Shift+C.");
            RBK(StampHotkey, "Blueprint: Stamp Layout",
                "Arms stamping with the selected blueprint. Click to place. A key or chord, e.g. Shift+V.");
            RBK(PanelHotkey, "Blueprint: Library Panel",
                "Opens the blueprint library: name and save captures, pick one to stamp. A key or chord, e.g. Shift+B.");
            RBK(RotateHotkey, "Blueprint: Rotate",
                "Rotates the blueprint 90 degrees while stamping. Default Tab.");

            // ── Forage Calendar ──────────────────────────────────────────
            _order = 0;
            R("Forage Calendar", EnableForageCalendar, "Forage Season Bar",
                "Bar under the top-bar season strip showing when each forageable on your map is in season. Hover the strip to peek; pin with the game's own info toggle. Applies live.");
            R("Forage Calendar", ForageCalendarAlwaysShow, "Always Show Bar",
                "Keep the season bar visible at all times instead of only with the season popup. Applies live.");
            R("Forage Calendar", EnableInSeasonReadout, "In-Season Readout",
                "Adds a live 'In season: ...' line to the date/weather popup. Applies live.");

            // ── Hotkeys — Building ───────────────────────────────────────
            _order = 0;
            K("Hotkeys — Building", UpgradeHotkey, "Upgrade");
            K("Hotkeys — Building", RelocateHotkey, "Relocate");
            K("Hotkeys — Building", ToggleEmployHotkey, "Toggle Building Production");
            K("Hotkeys — Building", DemolishHotkey, "Salvage Building", "Default T. Delete still works as a fixed fallback regardless of rebind.");
            K("Hotkeys — Building", CycleBuildingLeftHotkey, "Cycle Building Left");
            K("Hotkeys — Building", CycleBuildingRightHotkey, "Cycle Building Right");
            K("Hotkeys — Building", MoveWorkAreaHotkey, "Move Work Area", "Buildings with a work area (hunters, foresters, herbalists…) — start moving it. Same as the panel's retarget button.");

            // ── Hotkeys — Build Site ─────────────────────────────────────
            _order = 0;
            K("Hotkeys — Build Site", DeleteBuildSiteHotkey, "Delete Build Site", "Default T. Delete still works as a fixed fallback regardless of rebind.");
            K("Hotkeys — Build Site", ConstructionEnabledHotkey, "Construction Toggle");
            K("Hotkeys — Build Site", PrioritizeHotkey, "Prioritize");
            K("Hotkeys — Build Site", DecrementBuildersHotkey, "Decrement Builders (-)");
            K("Hotkeys — Build Site", IncrementBuildersHotkey, "Increment Builders (+)");

            // ── Hotkeys — Forageables ────────────────────────────────────
            // Relocate is the SAME RelocateHotkey pref as the Building section
            // — re-registered here in a second category so the player sees it
            // alongside the forageable-specific hotkeys. Editing either entry
            // edits the same underlying value.
            _order = 0;
            K("Hotkeys — Forageables", RelocateHotkey, "Relocate", "Forageables only — shares the same key as Building Relocate (rebinding here rebinds there). Trees, stone deposits, and ruins can't be relocated.");
            K("Hotkeys — Forageables", ForageableHarvestHotkey, "Harvest", "Fires the native Harvest button. Works on forageables, trees, stone deposits, and excavated ruins.");
            K("Hotkeys — Forageables", ForageableDeleteHotkey, "Delete", "Fires the native Delete/Clear button. Works on forageables, trees, stone deposits, and excavated ruins.");
            K("Hotkeys — Forageables", ForageablePrioritizeHotkey, "Prioritize", "Fires the native Prioritize toggle. Works on forageables, trees, stone deposits, excavated ruins, and fruit trees.");
            K("Hotkeys — Forageables", CullForWoodHotkey, "Cull Fruit Tree for Wood", "Fruit trees only — mark the tree to be cut down for wood (native 'Cull For Wood').");

            // ── Hotkeys — Crop Field ─────────────────────────────────────
            _order = 0;
            K("Hotkeys — Crop Field", CropCopyHotkey, "Copy Settings", "Copy this crop field's settings (native button).");
            K("Hotkeys — Crop Field", CropPasteHotkey, "Paste Settings", "Paste copied crop field settings (native button).");
            K("Hotkeys — Crop Field", CropExpandHotkey, "Expand Field", "Expand the crop field (native button).");
            K("Hotkeys — Crop Field", CropClearHotkey, "Clear Selected Crop", "Clear the selected crop (native button).");
            K("Hotkeys — Crop Field", CropSalvageHotkey, "Salvage", "Salvage the crop field (native button).");

            // ── Hotkeys — Confirmation Dialog ────────────────────────────
            _order = 0;
            K("Hotkeys — Confirmation Dialog", ConfirmHotkey, "Confirm", "Default Y. Enter also works as a fixed fallback.");
            K("Hotkeys — Confirmation Dialog", CancelHotkey, "Cancel", "Default N. Esc also works as a fixed fallback.");

            // ── Notifications ────────────────────────────────────────────
            _order = 0;
            RI("Notifications", TraderWarningDays, "Trader Departure Warning Days", 0, 28,
                "Days before a trader leaves to show a 'Trader Departing' notification (0 disables)");
            R("Notifications", DismissibleResourceAlerts, "Dismissible Resource Alerts",
                "Click a top-bar '!' warning to silence it until next month change.");

            // ── Game and Map Settings ────────────────────────────────────
            _order = 0;
            R("Game and Map Settings", KeepMapTypeOnReroll, "Keep Map Type on Reroll",
                "Dice button rerolls the seed within your selected biome instead of forcing Random.");
            R("Game and Map Settings", RememberCustomSettings, "Remember Custom Settings",
                "Snapshot the Custom Settings panel selections on Confirm and restore on next open.");
            R("Game and Map Settings", SyncMapTypeWithRiverPreset, "Sync Map Type with Rivers Restored",
                "Two-way sync between FF's terrain selector and RR's RiverPreset. Disabled when RR is disabled or set to Custom.");
            // BridgeAnywhere is live — patches re-read the toggle on every
            // bridge placement attempt, so no flag needed.
            R("Game and Map Settings", BridgeAnywhere, "Bridges Anywhere (no water / any height)",
                "Lifts the 'must be over water' and 'snap to water level' rules so you can span dry ravines and high-bank rivers without terraforming. The bridge sits at the start/end cells' terrain height.");

            // ── Game Flow ────────────────────────────────────────────────
            _order = 0;
            R("Game Flow", DoubleClickLoad, "Double-Click to Load",
                "Double-click a save (start screen or in-game Load menu) to load it without the confirmation prompt. Single click still confirms.");
            RF("Game Flow", DoubleClickLoadWindow, "Double-Click Speed (seconds)", 0.15f, 0.6f,
                "Max gap between the two clicks (and the brief hold before a single click shows the prompt).",
                visibleWhen: () => DoubleClickLoad.Value);
            R("Game Flow", PauseOnLoad, "Pause on Load", "Auto-pause once a save finishes loading");
            RF("Game Flow", PauseOnLoadDelay, "Pause on Load Delay (seconds)", 0f, 30f,
                "Wait this many seconds before pausing — gives the scene time to render so the first frame isn't black. Increase for heavily-modded setups.",
                visibleWhen: () => PauseOnLoad.Value);
            R("Game Flow", SkipNewMapIntro, "Skip Start-of-Game Cinematic",
                "Bypass the intro video and go straight to map load.");
            RI("Game Flow", CustomPopulationCap, "Custom Population Cap", 0, 5000,
                "Override the four slider stops (200/500/1000/2000) with any value. 0 = leave vanilla behavior.");
            R("Game Flow", IgnoreUpgradePopulationRequirement, "Ignore Upgrade Population Requirement",
                "Bypass population gates on Town Center tier ups so low-pop saves can still progress.");

            // ── Other Settings (debug / advanced) ────────────────────────
            _order = 0;
            {
                var langOpts = new System.Collections.Generic.List<string> { "Auto" };
                langOpts.AddRange(Localization.KcLoc.LanguageNames);
                SettingsAPI.Register(id, name, "Other Settings", LanguageOverride,
                    new SettingsMeta
                    {
                        Label = "Mod Panel Language",
                        Tooltip = "Language for mod settings text. 'Auto' follows Farthest Frontier's own language setting. Set explicitly if the panel shows the wrong language.",
                        EnumOptions = langOpts.ToArray(),
                        Order = Next(),
                    });
            }
            R("Other Settings", SettingsVerboseLog, "Verbose Settings Log",
                "Log every claim/discovery decision to MelonLoader/Latest.log. Off by default; turn on only when debugging the panel.");
        }

        // We cache the InfoWindow GameObject for reflection access (productionToggle, etc.).
        // The active/inactive question is answered by the input state machine, not by
        // the GameObject — see FrameBuildingWindowActive in OnUpdate.
        //
        // IMPORTANT: search for UIBuildingInfoWindow_New specifically. The legacy
        // UIBuildingInfoWindow class still exists in the assembly but isn't what
        // the game instantiates in v1.1.0+.
        private static UISelectedObjectInfoWindow? _cachedInfoWindow;

        public static UISelectedObjectInfoWindow? GetBuildingInfoWindow()
        {
            if (_cachedInfoWindow == null)
            {
                var windows = Object.FindObjectsOfType<UIBuildingInfoWindow_New>(true);
                if (windows.Length > 0) _cachedInfoWindow = windows[0];
            }
            return _cachedInfoWindow;
        }

        private static UIBuildsiteWindow_New? _cachedBuildSiteWindow;

        public static UIBuildsiteWindow_New? GetBuildSiteWindow()
        {
            if (_cachedBuildSiteWindow == null)
            {
                var windows = Object.FindObjectsOfType<UIBuildsiteWindow_New>(true);
                if (windows.Length > 0) _cachedBuildSiteWindow = windows[0];
            }
            return _cachedBuildSiteWindow;
        }

        private void HandleBuildingHotkeys(Building building, GameManager gm)
        {
            if (Input.GetKeyDown(UpgradeHotkey.Value))
            {
                BuildingActions.TryUpgrade(building, gm);
            }
            else if (Input.GetKeyDown(RelocateHotkey.Value))
            {
                BuildingActions.TryRelocate(building, gm);
            }
            else if (Input.GetKeyDown(ToggleEmployHotkey.Value))
            {
                BuildingActions.TryToggleEmployment(building, gm);
            }
            else if (Input.GetKeyDown(DemolishHotkey.Value) || Input.GetKeyDown(KeyCode.Delete))
            {
                // DemolishHotkey is the configurable one (default T, labeled
                // "Salvage Building"). Delete is hardcoded as a fixed fallback
                // so it always works regardless of rebinding.
                BuildingActions.TryDemolish(building, gm);
            }
            else if (Input.GetKeyDown(CycleBuildingLeftHotkey.Value))
            {
                BuildingActions.TryCycleLeft(building, gm);
            }
            else if (Input.GetKeyDown(CycleBuildingRightHotkey.Value))
            {
                BuildingActions.TryCycleRight(building, gm);
            }
            else if (Input.GetKeyDown(MoveWorkAreaHotkey.Value))
            {
                BuildingActions.TryMoveWorkArea(building, gm);
            }
        }

        // Crop Field UI: fire the native buttons by name. Custom UI (EP Planting
        // Almanac, AddClay/AddSand) is intentionally not bound.
        private void HandleCropfieldHotkeys()
        {
            if (Input.GetKeyDown(CropCopyHotkey.Value)) CropfieldActions.TryClickButton("CopyButton");
            else if (Input.GetKeyDown(CropPasteHotkey.Value)) CropfieldActions.TryClickButton("PasteButton");
            else if (Input.GetKeyDown(CropExpandHotkey.Value)) CropfieldActions.TryExpand();
            else if (Input.GetKeyDown(CropClearHotkey.Value)) CropfieldActions.TryClearSelectedCrop();
            else if (Input.GetKeyDown(CropSalvageHotkey.Value)) CropfieldActions.TryClickButton("SalvageButton");
        }

        // Fruit trees: Cull For Wood (C) + Prioritize (P), routed through the same
        // harvestable-resource window. No Harvest/Delete/Relocate.
        private void HandleFruitTreeHotkeys()
        {
            if (Input.GetKeyDown(CullForWoodHotkey.Value))
                ForageableActions.TryCullForWood();
            else if (Input.GetKeyDown(ForageablePrioritizeHotkey.Value))
                ForageableActions.TryTogglePrioritize();
        }

        // Shared by forageables (canRelocate: true) and tree/stone deposits
        // (canRelocate: false). Relocate is a Tended Wilds forageable feature —
        // trees and stone deposits can't be relocated, so they get only
        // Harvest / Delete / Prioritize.
        private void HandleHarvestableHotkeys(bool canRelocate)
        {
            if (canRelocate && Input.GetKeyDown(RelocateHotkey.Value))
                ForageableActions.TryRelocate();
            else if (Input.GetKeyDown(ForageableHarvestHotkey.Value))
                ForageableActions.TryHarvest();
            else if (Input.GetKeyDown(ForageableDeleteHotkey.Value))
                ForageableActions.TryDelete();
            else if (Input.GetKeyDown(ForageablePrioritizeHotkey.Value))
                ForageableActions.TryTogglePrioritize();
        }

        private void HandleBuildSiteHotkeys(BuildSiteResource buildSite, GameManager gm)
        {
            // DeleteBuildSiteHotkey is configurable (default T). Delete is
            // hardcoded fixed fallback. Same pattern as building Salvage.
            if (Input.GetKeyDown(DeleteBuildSiteHotkey.Value) || Input.GetKeyDown(KeyCode.Delete))
            {
                BuildSiteActions.TryCancel(buildSite, gm);
            }
            else if (Input.GetKeyDown(ConstructionEnabledHotkey.Value))
            {
                BuildSiteActions.TryToggleConstructionEnabled(buildSite, gm);
            }
            else if (Input.GetKeyDown(PrioritizeHotkey.Value))
            {
                BuildSiteActions.TryTogglePriority(buildSite, gm);
            }
            else if (Input.GetKeyDown(DecrementBuildersHotkey.Value))
            {
                BuildSiteActions.TryDecrementBuilders(buildSite, gm);
            }
            else if (Input.GetKeyDown(IncrementBuildersHotkey.Value))
            {
                BuildSiteActions.TryIncrementBuilders(buildSite, gm);
            }
        }

        private static FieldInfo? _selectedObjField;
        private GameObject? GetSelectedGameObject(GameManager gm)
        {
            // InputManager.selectedObject is the current selection
            if (_selectedObjField == null)
            {
                _selectedObjField = typeof(InputManager).GetField("selectedObject",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }
            return _selectedObjField?.GetValue(gm.inputManager) as GameObject;
        }

        private void HandleEscapeKey(GameManager gm)
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;

            // If overlay config is open, close it first
            if (_overlay != null && _overlay.ConfigOpen)
            {
                _overlay.ConfigOpen = false;
                return;
            }

            var wm = gm.uiManager.windowManager;
            wm.CloseMostRecentMenu();
        }

        /// <summary>
        /// Check if a modal dialog is currently visible by looking for active
        /// child objects under the modal manager with Button components.
        /// </summary>
        public static bool IsModalVisible(GameManager gm)
        {
            var modalMgr = gm.uiManager.modalWindowManager;
            if (modalMgr == null) return false;

            // The manager stays active — check if any child with buttons is visible
            for (int i = 0; i < modalMgr.transform.childCount; i++)
            {
                var child = modalMgr.transform.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                {
                    var buttons = child.GetComponentsInChildren<Button>(false);
                    if (buttons.Length >= 2) return true; // Has confirm + cancel buttons
                }
            }
            return false;
        }

        private void HandleModalHotkeys(GameManager gm)
        {
            if (!IsModalVisible(gm)) return;

            var modalMgr = gm.uiManager.modalWindowManager;

            if (Input.GetKeyDown(ConfirmHotkey.Value) || Input.GetKeyDown(KeyCode.Return))
            {
                ClickModalButton(modalMgr, true);
            }
            else if (Input.GetKeyDown(CancelHotkey.Value) || Input.GetKeyDown(KeyCode.Escape))
            {
                ClickModalButton(modalMgr, false);
            }
        }

        private void ClickModalButton(UIModalWindowManager modalMgr, bool confirm)
        {
            // Find the active modal child and its buttons
            for (int i = 0; i < modalMgr.transform.childCount; i++)
            {
                var child = modalMgr.transform.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;

                var buttons = child.GetComponentsInChildren<Button>(false);
                if (buttons.Length < 2) continue;

                // Convention: first button is usually confirm/yes, last is cancel/no
                // But let's try to find by name
                Button? targetBtn = null;
                foreach (var btn in buttons)
                {
                    string name = btn.gameObject.name.ToLower();
                    if (confirm && (name.Contains("confirm") || name.Contains("yes") || name.Contains("ok") || name.Contains("accept")))
                    {
                        targetBtn = btn;
                        break;
                    }
                    if (!confirm && (name.Contains("cancel") || name.Contains("no") || name.Contains("close") || name.Contains("decline")))
                    {
                        targetBtn = btn;
                        break;
                    }
                }

                // Fallback: first button = confirm, last button = cancel
                if (targetBtn == null)
                {
                    targetBtn = confirm ? buttons[0] : buttons[buttons.Length - 1];
                }

                targetBtn.onClick.Invoke();
                return;
            }

            Log.Warning($"[Modal] Could not find active modal with buttons");
        }

        private void HandleOverlayToggle()
        {
            if (!Input.GetKeyDown(ToggleOverlayHotkey.Value)) return;
            // Drive both overlays from the same state. Use the pinned overlay
            // as the source of truth — if it's currently visible, we're
            // hiding both; otherwise we're showing both.
            bool target = _overlay == null ? true : !_overlay.Visible;
            if (_overlay != null) _overlay.Visible = target;
            if (_techQueueOverlay != null) _techQueueOverlay.Visible = target;
            if (_buildQueueOverlay != null) _buildQueueOverlay.Visible = target;
            UI.CompanyOverlayManager.SetVisible(target);
        }
    }
}
