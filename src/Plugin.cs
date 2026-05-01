using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using FFUIOverhaul.UI;
using FFUIOverhaul.Utils;
using FFUIOverhaul.TechTree;

[assembly: MelonInfo(typeof(FFUIOverhaul.FFUIOverhaulMod), "Keep Clarity", "1.0.0", "sagedragoon79")]
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

        // Tech research queue
        public static MelonPreferences_Entry<string> TechResearchQueue { get; private set; } = null!;

        // Pause on load
        public static MelonPreferences_Entry<bool> PauseOnLoad { get; private set; } = null!;
        public static MelonPreferences_Entry<float> PauseOnLoadDelay { get; private set; } = null!;

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

            TechResearchQueue = _prefs.CreateEntry("TechResearchQueue", "",
                display_name: "Tech Research Queue",
                description: "Comma-separated list of tech node IDs queued for auto-research");

            PauseOnLoad = _prefs.CreateEntry("PauseOnLoad", false,
                display_name: "Pause on Load",
                description: "Automatically pause the game once it finishes loading a save");

            PauseOnLoadDelay = _prefs.CreateEntry("PauseOnLoadDelaySeconds", 2.5f,
                display_name: "Pause on Load Delay (seconds)",
                description: "How many seconds to wait after the game finishes loading before pausing. Gives lighting/post-processing time to settle so the player doesn't see a black 'void' frame.");

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
            if (stateName == "Input_SelectGameObject")
            {
                var selectedObj = GetSelectedGameObject(gm);
                if (selectedObj != null)
                {
                    selectedBuilding = selectedObj.GetComponent<Building>()
                        ?? selectedObj.GetComponentInParent<Building>();
                    selectedBuildSite = selectedObj.GetComponent<BuildSiteResource>()
                        ?? selectedObj.GetComponentInParent<BuildSiteResource>();
                }
            }
            FrameBuildingWindowActive = selectedBuilding != null;
            FrameBuildSiteWindowActive = selectedBuildSite != null;


            // Order matters: modal handler first (consumes Y/N/Enter/Esc in dialogs)
            if (FrameModalVisible)
            {
                HandleModalHotkeys(gm);
                return; // Modal is the ONLY thing that responds — no other hotkeys
            }

            HandleReportsHotkey(gm);
            if (selectedBuilding != null) HandleBuildingHotkeys(selectedBuilding, gm);
            else if (selectedBuildSite != null) HandleBuildSiteHotkeys(selectedBuildSite, gm);
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
            bool isMap = sceneName == "Map";

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
            else if (Input.GetKeyDown(DemolishHotkey.Value))
            {
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
            if (Input.GetKeyDown(DemolishHotkey.Value))
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
