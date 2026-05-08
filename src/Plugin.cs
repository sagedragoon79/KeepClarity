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

[assembly: MelonInfo(typeof(FFUIOverhaul.FFUIOverhaulMod), "Keep Clarity", "1.1.0", "sagedragoon79")]
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
        public static MelonPreferences_Entry<float> OverlayUIScale { get; private set; } = null!;

        // Tech research queue
        public static MelonPreferences_Entry<string> TechResearchQueue { get; private set; } = null!;

        // Pause on load
        public static MelonPreferences_Entry<bool> PauseOnLoad { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PauseOnLoadDelay { get; private set; } = null!;

        // Planner button
        public static MelonPreferences_Entry<bool> ShowPlannerButton { get; private set; } = null!;

        // New game menu QoL
        public static MelonPreferences_Entry<bool> KeepMapTypeOnReroll { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> RememberCustomChoices { get; private set; } = null!;
        public static MelonPreferences_Entry<string> RememberCustomChoicesSnapshot { get; private set; } = null!;
        public static MelonPreferences_Entry<bool> SkipNewMapIntro { get; private set; } = null!;

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
                display_name: "Relocate Hotkey",
                description: "Hotkey to relocate selected building");

            ToggleEmployHotkey = _prefs.CreateEntry("ToggleEmployHotkey", KeyCode.E,
                display_name: "Toggle Employment Hotkey",
                description: "Hotkey to toggle employment on selected building");

            DemolishHotkey = _prefs.CreateEntry("DemolishHotkey", KeyCode.Delete,
                display_name: "Demolish Hotkey",
                description: "Hotkey to demolish selected building or cancel a build/deconstruction site (opens confirmation)");

            CycleBuildingLeftHotkey = _prefs.CreateEntry("CycleBuildingLeftHotkey", KeyCode.LeftArrow,
                display_name: "Cycle Building Left",
                description: "Cycle to the previous building of the same type (mirrors the < button at top of the info panel)");

            CycleBuildingRightHotkey = _prefs.CreateEntry("CycleBuildingRightHotkey", KeyCode.RightArrow,
                display_name: "Cycle Building Right",
                description: "Cycle to the next building of the same type (mirrors the > button at top of the info panel)");

            PrioritizeHotkey = _prefs.CreateEntry("PrioritizeHotkey", KeyCode.P,
                display_name: "Prioritize Hotkey",
                description: "Hotkey to toggle the Prioritized checkbox on a build/deconstruction site");

            ConstructionEnabledHotkey = _prefs.CreateEntry("ConstructionEnabledHotkey", KeyCode.O,
                display_name: "Construction Enabled Hotkey",
                description: "Hotkey to toggle the Construction Enabled checkbox on a build/deconstruction site");

            DecrementBuildersHotkey = _prefs.CreateEntry("DecrementBuildersHotkey", KeyCode.Minus,
                display_name: "Decrement Builders Hotkey",
                description: "Hotkey to remove a builder from a build/deconstruction site (-)");

            IncrementBuildersHotkey = _prefs.CreateEntry("IncrementBuildersHotkey", KeyCode.Equals,
                display_name: "Increment Builders Hotkey",
                description: "Hotkey to add a builder to a build/deconstruction site (=)");

            ConfirmHotkey = _prefs.CreateEntry("ConfirmHotkey", KeyCode.Y,
                display_name: "Confirm Hotkey",
                description: "Hotkey to confirm dialogs (Y key)");

            CancelHotkey = _prefs.CreateEntry("CancelHotkey", KeyCode.N,
                display_name: "Cancel Hotkey",
                description: "Hotkey to cancel dialogs (N key)");

            ToggleOverlayHotkey = _prefs.CreateEntry("ToggleOverlayHotkey", KeyCode.F5,
                display_name: "Toggle Overlay",
                description: "Hotkey to show/hide the pinned resource overlay");

            TraderWarningDays = _prefs.CreateEntry("TraderWarningDays", 5,
                display_name: "Trader Warning Days",
                description: "Days before departure to show trader warning notification");

            PinnedResourcesJson = _prefs.CreateEntry("PinnedResources", "",
                display_name: "Pinned Resources",
                description: "JSON list of pinned resource item IDs (managed by overlay UI)");

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
                description: "How opaque the pinned and tech queue panels are. 1.0 = solid, 0.3 = nearly invisible. Applies live.");

            OverlayUIScale = _prefs.CreateEntry("OverlayUIScale", 1.0f,
                display_name: "Overlay UI Scale",
                description: "Pinned/tech queue panel size multiplier. 1.0 = normal, 0.5 = half, 2.0 = double. Applies live.");

            TechResearchQueue = _prefs.CreateEntry("TechResearchQueue", "",
                display_name: "Tech Research Queue",
                description: "Comma-separated list of tech node IDs queued for auto-research");

            PauseOnLoad = _prefs.CreateEntry("PauseOnLoad", false,
                display_name: "Pause on Load",
                description: "Automatically pause the game once it finishes loading a save");

            ShowPlannerButton = _prefs.CreateEntry("ShowPlannerButton", true,
                display_name: "Show Planner Button",
                description: "Show the PLAN icon in the top bar that opens SageDragoon's Farthest Frontier Planner in your browser. Restart required to take effect.");

            KeepMapTypeOnReroll = _prefs.CreateEntry("KeepMapTypeOnReroll", true,
                display_name: "Keep Map Type on Reroll",
                description: "When enabled, the dice button in the New Game menu rerolls the seed within your selected terrain type (Lush Forest, Cold Mountains, etc.) instead of forcing it to Random. Disable to restore vanilla behavior.");

            RememberCustomChoices = _prefs.CreateEntry("RememberCustomChoices", false,
                display_name: "Remember Choices",
                description: "When enabled, your selections in the New Game → Custom Settings panel are saved on Confirm and restored automatically the next time you open that panel.");

            RememberCustomChoicesSnapshot = _prefs.CreateEntry("RememberCustomChoicesSnapshot", "",
                display_name: "(internal) Custom Choices Snapshot",
                description: "Internal — saved values for the Remember Choices feature. Edit only if you know the format.",
                is_hidden: true);

            SkipNewMapIntro = _prefs.CreateEntry("SkipNewMapIntro", false,
                display_name: "Skip Start-of-Game Cinematic",
                description: "Skip the cinematic video that plays after starting a new settlement. Goes straight to map load — no video, no narration.");

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
            else if (selectedForageable && Input.GetKeyDown(RelocateHotkey.Value))
                ForageableActions.TryRelocate();
            HandleEscapeKey(gm);
            HandleOverlayToggle();
            _overlay?.Tick();
            _techQueueOverlay?.Tick();
            TechQueueInput.Tick();
            HandlePauseOnLoad(gm);
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
            // Shift+F10 → dump every active UI element to MelonLoader/Logs/.
            // Used to study FF's native styling so we can write targeted asset
            // lookups for the UGUI panel (Stage 2A).
            if (Input.GetKeyDown(SettingsPanelHotkey.Value)
                && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
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

            // Hotkeys
            void K(string cat, MelonPreferences_Entry<KeyCode> entry, string label, string? tip = null) =>
                SettingsAPI.Register(id, name, cat, entry, new SettingsMeta { Label = label, Tooltip = tip });

            K("Hotkeys — Reports & Panels", ReportsHotkey, "Toggle Reports", "Open/close the 12-month report window");
            K("Hotkeys — Reports & Panels", ToggleOverlayHotkey, "Toggle Pinned Overlay");
            K("Hotkeys — Reports & Panels", SettingsPanelHotkey, "Open Settings Panel", "Opens this very window");

            SettingsAPI.Register(id, name, "Settings Panel", SettingsVerboseLog,
                new SettingsMeta { Label = "Verbose Settings Log",
                    Tooltip = "Log every claim/discovery decision to MelonLoader/Latest.log. Off by default; turn on only when debugging the panel." });

            K("Hotkeys — Building", UpgradeHotkey, "Upgrade");
            K("Hotkeys — Building", RelocateHotkey, "Relocate");
            K("Hotkeys — Building", ToggleEmployHotkey, "Toggle Employment");
            K("Hotkeys — Building", DemolishHotkey, "Demolish");
            K("Hotkeys — Building", CycleBuildingLeftHotkey, "Cycle Building Left");
            K("Hotkeys — Building", CycleBuildingRightHotkey, "Cycle Building Right");

            K("Hotkeys — Build Site", PrioritizeHotkey, "Prioritize");
            K("Hotkeys — Build Site", ConstructionEnabledHotkey, "Construction Enabled");
            K("Hotkeys — Build Site", DecrementBuildersHotkey, "Decrement Builders (-)");
            K("Hotkeys — Build Site", IncrementBuildersHotkey, "Increment Builders (+)");

            K("Hotkeys — Modal Confirm", ConfirmHotkey, "Confirm Dialog");
            K("Hotkeys — Modal Confirm", CancelHotkey, "Cancel Dialog");

            // Numeric / bool / string prefs
            SettingsAPI.Register(id, name, "Notifications", TraderWarningDays,
                new SettingsMeta { Label = "Trader Warning Days", Min = 0, Max = 14,
                    Tooltip = "Days before departure to warn (0 disables)" });

            SettingsAPI.Register(id, name, "Game Flow", PauseOnLoad,
                new SettingsMeta { Label = "Pause on Load",
                    Tooltip = "Auto-pause once a save finishes loading" });

            SettingsAPI.Register(id, name, "Game Flow", PauseOnLoadDelay,
                new SettingsMeta { Label = "Pause on Load Delay (seconds)", Min = 0f, Max = 10f,
                    Tooltip = "Wait this many seconds before pausing — gives the scene time to render so the first frame isn't black",
                    VisibleWhen = () => PauseOnLoad.Value });

            SettingsAPI.Register(id, name, "Pinned Overlay", PinnedCollapsed,
                new SettingsMeta { Label = "Start Collapsed",
                    Tooltip = "Pinned overlay opens collapsed to a tab" });
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
            else if (Input.GetKeyDown(DemolishHotkey.Value) || Input.GetKeyDown(KeyCode.T))
            {
                // T is a fixed alt for Delete — easier on the hand than reaching
                // for Del. If the user remaps DemolishHotkey to T explicitly the
                // duplicate check still works because GetKeyDown only returns
                // true for the frame the key was pressed.
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

        private void HandleBuildSiteHotkeys(BuildSiteResource buildSite, GameManager gm)
        {
            if (Input.GetKeyDown(DemolishHotkey.Value) || Input.GetKeyDown(KeyCode.T))
            {
                BuildSiteActions.TryCancel(buildSite, gm);
            }
            else if (Input.GetKeyDown(PrioritizeHotkey.Value))
            {
                BuildSiteActions.TryTogglePriority(buildSite, gm);
            }
            else if (Input.GetKeyDown(ConstructionEnabledHotkey.Value))
            {
                BuildSiteActions.TryToggleConstructionEnabled(buildSite, gm);
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
            if (Input.GetKeyDown(ToggleOverlayHotkey.Value) && _overlay != null)
            {
                _overlay.Visible = !_overlay.Visible;
            }
        }
    }
}
