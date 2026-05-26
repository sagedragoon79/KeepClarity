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

[assembly: MelonInfo(typeof(FFUIOverhaul.FFUIOverhaulMod), "Keep Clarity", "1.2.4", "sagedragoon79")]
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
        public static MelonPreferences_Entry<bool> DismissibleResourceAlerts { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> EnableCompanyOverlay { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayPosX { get; private set; } = null!;
        public static MelonPreferences_Entry<float> CompanyOverlayPosY { get; private set; } = null!;
        public static MelonPreferences_Entry<string> CompanyOverlayPositions { get; private set; } = null!;

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

        public void RefreshTechQueueOverlay() => _techQueueOverlay?.RefreshDisplay();

        // Per-frame cached context flags — read by HotkeySuppressPatch to avoid per-call overhead
        public static bool FrameModalVisible { get; private set; }
        public static bool FrameBuildingWindowActive { get; private set; }
        public static bool FrameBuildSiteWindowActive { get; private set; }
        public static bool FrameForageableActive { get; private set; }


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

            PauseOnLoadDelay = _prefs.CreateEntry("PauseOnLoadDelaySeconds", 2.5f,
                display_name: "Pause on Load Delay (seconds)",
                description: "How many seconds to wait after the game finishes loading before pausing. Gives lighting/post-processing time to settle so the player doesn't see a black 'void' frame.");

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

        public override void OnUpdate()
        {
            if (!Application.isPlaying) return;

            // Settings panel hotkey works on the main menu too — it doesn't
            // need a GameManager and the panel is useful pre-load (e.g. flipping
            // a master toggle that requires a restart anyway).
            HandleSettingsPanelHotkey();

            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return;

            // Compute context flags once per frame from the input state machine —
            // it's the source of truth for which input context owns the keyboard.
            // GameObject-based detection is unreliable: panels often hide via
            // CanvasGroup rather than SetActive, leaving activeInHierarchy=true.
            var stateName = InputStateHelper.GetCurrentInputStateType()?.Name;
            FrameModalVisible = stateName == "Input_ModalWindow";

            // Resolve the selected GameObject's component types once per frame —
            // both flags need it, no point in calling reflection twice.
            Building? selectedBuilding = null;
            BuildSiteResource? selectedBuildSite = null;
            bool selectedForageable = false;
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
                        selectedForageable = ForageableActions.IsForageable(selectedObj);
                }
            }
            FrameBuildingWindowActive = selectedBuilding != null;
            FrameBuildSiteWindowActive = selectedBuildSite != null;
            FrameForageableActive = selectedForageable;


            // Order matters: modal handler first (consumes Y/N/Enter/Esc in dialogs)
            if (FrameModalVisible)
            {
                HandleModalHotkeys(gm);
                return; // Modal is the ONLY thing that responds — no other hotkeys
            }

            HandleReportsHotkey(gm);
            if (selectedBuilding != null) HandleBuildingHotkeys(selectedBuilding, gm);
            else if (selectedBuildSite != null) HandleBuildSiteHotkeys(selectedBuildSite, gm);
            else if (selectedForageable) HandleForageableHotkeys();
            HandleEscapeKey(gm);
            HandleOverlayToggle();
            _overlay?.Tick();
            _techQueueOverlay?.Tick();
            TechQueueInput.Tick();
            HandlePauseOnLoad(gm);
            Patches.DismissibleResourceAlerts.Tick();
            UI.CompanyOverlayManager.Tick();
        }

        private void HandlePauseOnLoad(GameManager gm)
        {
            if (_pauseOnLoadDone || !PauseOnLoad.Value) return;
            if (!GameManager.gameReadyToPlay) return; // wait for game to finish loading

            // Tick the delay only once gameReadyToPlay is true. Lighting and
            // post-processing take a bit to settle after that flag flips, so
            // pausing immediately puts the player in a black "void" frame.
            // Use unscaled time so a paused-by-something-else state doesn't
            // freeze the timer.
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

            if (isMap)
            {
                // Construct lazily — both overlays use DontDestroyOnLoad so they
                // survive scene transitions. Re-creating on every Map load would
                // stack canvases (visible as a "double background") and leak.
                if (_overlay == null) _overlay = new PinnedResourceOverlay();
                _overlay.Visible = true;

                if (_techQueueOverlay == null) _techQueueOverlay = new TechQueueMainOverlay();
                _techQueueOverlay.Visible = true;

                _pauseOnLoadDone = false; // re-arm pause-on-load for this Map session
                _pauseOnLoadTimer = 0f;

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
            }
            else
            {
                // Hide overlays on the main menu / any non-gameplay scene.
                if (_overlay != null) _overlay.Visible = false;
                if (_techQueueOverlay != null) _techQueueOverlay.Visible = false;
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

            // Ctrl+Shift+F10 → export every loaded sprite to PNG + an HTML
            // gallery under MelonLoader/KC_SpriteExport_*/. Offline reference so
            // we stop guessing sprite names. Checked before the Shift-only dump.
            if (Input.GetKeyDown(SettingsPanelHotkey.Value) && shiftHeld && ctrlHeldNow)
            {
                Settings.UI.SpriteExport.Run();
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
            K("Hotkeys — Reports & Panels", ToggleOverlayHotkey, "Toggle Overlays", "Show/hide both the pinned overlay and the tech queue overlay");
            K("Hotkeys — Reports & Panels", SettingsPanelHotkey, "Open Settings Panel", "Opens this very window");

            // ── Overlay Settings (pinned + tech queue overlays) ─────────
            _order = 0;
            R("Overlay Settings", PinnedCollapsed, "Pinned Overlay Starts Collapsed",
                "Pinned overlay opens collapsed to a tab");
            R("Overlay Settings", EnableCompanyOverlay, "Company Roster Overlay",
                "Shift+Left-Click a banner (bottom of screen) to open a draggable roster panel for that company.");
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

            // ── Hotkeys — Building ───────────────────────────────────────
            _order = 0;
            K("Hotkeys — Building", UpgradeHotkey, "Upgrade");
            K("Hotkeys — Building", RelocateHotkey, "Relocate");
            K("Hotkeys — Building", ToggleEmployHotkey, "Toggle Building Production");
            K("Hotkeys — Building", DemolishHotkey, "Salvage Building", "Default T. Delete still works as a fixed fallback regardless of rebind.");
            K("Hotkeys — Building", CycleBuildingLeftHotkey, "Cycle Building Left");
            K("Hotkeys — Building", CycleBuildingRightHotkey, "Cycle Building Right");

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
            K("Hotkeys — Forageables", RelocateHotkey, "Relocate", "Shares the same key as Building Relocate — rebinding here rebinds there.");
            K("Hotkeys — Forageables", ForageableHarvestHotkey, "Harvest");
            K("Hotkeys — Forageables", ForageableDeleteHotkey, "Delete");
            K("Hotkeys — Forageables", ForageablePrioritizeHotkey, "Prioritize");

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

            // ── Game Flow ────────────────────────────────────────────────
            _order = 0;
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
        }

        private void HandleForageableHotkeys()
        {
            if (Input.GetKeyDown(RelocateHotkey.Value))
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
        }
    }
}
