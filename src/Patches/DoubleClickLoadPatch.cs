using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Double-click a save to load it, skipping the "Load this game?" confirmation.
    /// Works on the main-menu start screen (<see cref="StartSceneManager"/>) and the
    /// in-game pause → Load menu (<see cref="UIPauseWindow"/>). A single click still
    /// shows the confirmation. Off by default.
    ///
    /// The double-click is detected from pointer-UP events on each save row, NOT from
    /// two button clicks. On the main-menu grid a rapid second press often drifts a
    /// pixel or two, so Unity's ScrollRect reclassifies it as a drag and the button's
    /// click is silently cancelled — the second OnClick never fires. OnPointerUp fires
    /// regardless of that drag, so pairing two ups catches the double every time.
    /// Using UP rather than DOWN preserves normal button feel: the load commits on
    /// release, so to abort you just hold the second press — with no second up inside
    /// the window the first click's deferred confirmation pops instead of loading. A
    /// click-based fallback in <see cref="Defer"/> still handles clean double-clicks
    /// if the watcher ever fails to attach.
    ///
    /// Both menus pop their confirmation the instant a save is clicked and hide the
    /// list to do it, so the first click is held briefly (no dialog yet); if no second
    /// press arrives within the window the confirmation shows as normal. Direct load
    /// reuses each menu's own path (start: ConfirmLoadGame; pause:
    /// LoadFromWithinGame). Saves missing required DLC/mods keep the confirmation
    /// (its warning) instead of fast-loading. Any reflection miss → vanilla behavior.
    /// </summary>
    internal static class DoubleClickLoad
    {
        internal static bool Enabled => FFUIOverhaulMod.DoubleClickLoad?.Value ?? false;
        internal static float Window => FFUIOverhaulMod.DoubleClickLoadWindow?.Value ?? 0.35f;

        // Reentrancy guard: the deferred timer re-invokes a menu's own method to show
        // the real confirmation — the prefixes must let that pass through.
        internal static bool Bypass;

        // Deferred single click (shows the confirmation once the window lapses).
        private static string? _pendingFile;
        private static float _pendingTime;
        private static Action? _pendingShowDialog;

        // Pointer-up double detection (primary path — immune to drag-cancelled clicks).
        private static string? _lastUpFile;
        private static float _lastUpTime;

        // After a pointer-down load fires, swallow the trailing click so it can't
        // re-arm a deferred confirmation before the scene finishes loading.
        private static string? _suppressFile;
        private static float _suppressUntil;

        // Lazy reflection into both menus.
        private static bool _resolved;
        private static MethodInfo? _startOnSelected, _startConfirmLoad, _pauseOnSelected;
        private static FieldInfo? _pauseCurrentTab;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _startOnSelected  = AccessTools.Method(typeof(StartSceneManager), "OnSavedGameSelected", new[] { typeof(string) });
            _startConfirmLoad = AccessTools.Method(typeof(StartSceneManager), "ConfirmLoadGame", new[] { typeof(string) });
            _pauseOnSelected  = AccessTools.Method(typeof(UIPauseWindow), "OnSavedGameSelected", new[] { typeof(string) });
            _pauseCurrentTab  = AccessTools.Field(typeof(UIPauseWindow), "currentTab");
            if (_startOnSelected == null || _startConfirmLoad == null || _pauseOnSelected == null || _pauseCurrentTab == null)
                FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] a menu method/field was not found — feature partially inert, vanilla confirmation kept.");
        }

        internal static MethodInfo? StartOnSelected  { get { Resolve(); return _startOnSelected; } }
        internal static MethodInfo? StartConfirmLoad { get { Resolve(); return _startConfirmLoad; } }
        internal static MethodInfo? PauseOnSelected  { get { Resolve(); return _pauseOnSelected; } }
        internal static FieldInfo?  PauseCurrentTab  { get { Resolve(); return _pauseCurrentTab; } }

        internal static bool IsSuppressed(string file)
            => _suppressFile == file && Time.unscaledTime <= _suppressUntil;

        /// <summary>Hold the click: defer the confirmation, or (fallback) load on a
        /// clean click-based double if the pointer-down watcher never attached.</summary>
        internal static bool Defer(string file, Action directLoad, Action showDialog)
        {
            float now = Time.unscaledTime;
            if (_pendingFile == file && (now - _pendingTime) <= Window)
            {
                _pendingFile = null; _pendingShowDialog = null;
                directLoad();
                return false;
            }
            _pendingFile = file; _pendingTime = now; _pendingShowDialog = showDialog;
            return false;
        }

        /// <summary>Driven from Plugin.OnUpdate (start scene AND gameplay scene).
        /// Flushes a held single click into the normal confirmation once the window
        /// lapses with no second press.</summary>
        public static void Tick()
        {
            if (_pendingFile == null || Time.unscaledTime - _pendingTime <= Window) return;
            var show = _pendingShowDialog;
            _pendingFile = null; _pendingShowDialog = null;
            show?.Invoke();
        }

        public static void ResetState()
        {
            _pendingFile = null; _pendingShowDialog = null;
            _lastUpFile = null; _suppressFile = null;
        }

        /// <summary>Each pointer-up on a save row. A second up on the same save within
        /// the window is a double-click → load now (up fires through the ScrollRect drag
        /// that cancels the button's click). To abort, just hold the second press: with
        /// no second up inside the window the first click's deferred confirmation pops
        /// (see <see cref="Tick"/>), and the eventual late release no longer pairs.</summary>
        internal static void NotifyPointerUp(string? file)
        {
            if (!Enabled || string.IsNullOrEmpty(file)) return;
            float now = Time.unscaledTime;
            bool second = _lastUpFile == file && (now - _lastUpTime) <= Window;
            if (second)
            {
                _lastUpFile = null;
                // Missing content → don't fast-load; let the normal click path show
                // the confirmation (with its warning). Re-stamp so a third press pairs.
                if (HasMissingContent(file!)) { _lastUpTime = now; return; }
                _pendingFile = null; _pendingShowDialog = null;         // cancel any deferred dialog
                _suppressFile = file; _suppressUntil = now + Mathf.Max(Window + 0.25f, 0.8f);
                LoadNow(file!);
                return;
            }
            _lastUpFile = file; _lastUpTime = now;
        }

        // Route the load to whichever menu is live.
        private static void LoadNow(string file)
        {
            Resolve();
            try
            {
                var pause = UnityEngine.Object.FindObjectOfType<UIPauseWindow>();
                if (pause != null && _pauseCurrentTab != null
                    && _pauseCurrentTab.GetValue(pause)?.ToString() == "LOAD_GAME")
                {
                    UnitySingletonPersistent<CESceneManager>.Instance.LoadFromWithinGame(file);
                    return;
                }
                var start = UnityEngine.Object.FindObjectOfType<StartSceneManager>();
                if (start != null && _startConfirmLoad != null)
                    _startConfirmLoad.Invoke(start, new object[] { file });
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] pointer-down load failed: " + e.Message); }
        }

        internal static void ShowVanilla(MethodInfo? method, object instance, string file)
        {
            if (method == null || instance == null) return;
            Bypass = true;
            try { method.Invoke(instance, new object[] { file }); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] deferred dialog failed: " + e.Message); }
            finally { Bypass = false; }
        }

        internal static bool HasMissingContent(string file)
        {
            try
            {
                var meta = SaveManager.LoadMetaData(file);
                var missingDlc = DlcManagerSingleton.Instance.GetMissingDlcs(meta.requiredDlcs);
                if (missingDlc != null && missingDlc.Count > 0) return true;
                var missingMods = UnitySingletonPersistent<CESteamManager>.Instance.GetMissingMods(meta.requiredMods);
                return missingMods != null && missingMods.Count > 0;
            }
            catch { return true; } // can't tell → err toward showing the dialog
        }
    }

    /// <summary>Main-menu start screen. OnSavedGameSelected is load-only here.</summary>
    [HarmonyPatch(typeof(StartSceneManager), "OnSavedGameSelected")]
    internal static class DoubleClickLoad_StartScene
    {
        [HarmonyPrefix]
        private static bool Prefix(StartSceneManager __instance, string savedGameFileName)
        {
            if (!DoubleClickLoad.Enabled || DoubleClickLoad.Bypass) return true;
            var onSel = DoubleClickLoad.StartOnSelected;
            var confirm = DoubleClickLoad.StartConfirmLoad;
            if (onSel == null || confirm == null) return true;

            var file = savedGameFileName;
            if (DoubleClickLoad.IsSuppressed(file)) return false; // already loaded via pointer-down
            var inst = __instance;
            Action showDialog = () => DoubleClickLoad.ShowVanilla(onSel, inst, file);
            Action directLoad = () =>
            {
                if (DoubleClickLoad.HasMissingContent(file)) { showDialog(); return; }
                try { confirm.Invoke(inst, new object[] { file }); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] start load failed: " + e.Message); showDialog(); }
            };
            return DoubleClickLoad.Defer(file, directLoad, showDialog);
        }
    }

    /// <summary>In-game pause → Load menu. OnSavedGameSelected serves both the SAVE
    /// (overwrite) and LOAD tabs, so we only intercept when currentTab == LOAD_GAME.</summary>
    [HarmonyPatch(typeof(UIPauseWindow), "OnSavedGameSelected")]
    internal static class DoubleClickLoad_PauseWindow
    {
        [HarmonyPrefix]
        private static bool Prefix(UIPauseWindow __instance, string savedGameFileNameNoExtension)
        {
            if (!DoubleClickLoad.Enabled || DoubleClickLoad.Bypass) return true;
            var onSel = DoubleClickLoad.PauseOnSelected;
            var tabF = DoubleClickLoad.PauseCurrentTab;
            if (onSel == null || tabF == null) return true;
            if (tabF.GetValue(__instance)?.ToString() != "LOAD_GAME") return true;

            var file = savedGameFileNameNoExtension;
            if (DoubleClickLoad.IsSuppressed(file)) return false;
            var inst = __instance;
            Action showDialog = () => DoubleClickLoad.ShowVanilla(onSel, inst, file);
            Action directLoad = () =>
            {
                if (DoubleClickLoad.HasMissingContent(file)) { showDialog(); return; }
                try { UnitySingletonPersistent<CESceneManager>.Instance.LoadFromWithinGame(file); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] pause load failed: " + e.Message); showDialog(); }
            };
            return DoubleClickLoad.Defer(file, directLoad, showDialog);
        }
    }

    /// <summary>Attach the pointer-down double-click watcher to every save row (both
    /// menus instantiate UISavedGameWidget).</summary>
    [HarmonyPatch(typeof(UISavedGameWidget), "Init")]
    internal static class DoubleClickLoad_WidgetAttach
    {
        [HarmonyPostfix]
        private static void Postfix(UISavedGameWidget __instance)
        {
            if (__instance.GetComponent<SaveRowDoubleClickWatcher>() == null)
                __instance.gameObject.AddComponent<SaveRowDoubleClickWatcher>().Widget = __instance;
        }
    }

    /// <summary>Reports pointer-ups on a save row to the double-click detector.
    /// OnPointerUp fires on release even when the ScrollRect drag-cancels the button's
    /// click (the bug that drops the second click on the main-menu grid), so pairing
    /// ups is drag-immune. Firing on release (not press) keeps normal button feel —
    /// hold the second press to fall back to the confirmation instead of loading.</summary>
    internal class SaveRowDoubleClickWatcher : MonoBehaviour, IPointerUpHandler
    {
        public UISavedGameWidget? Widget;
        public void OnPointerUp(PointerEventData e)
        {
            if (Widget != null) DoubleClickLoad.NotifyPointerUp(Widget.fileName);
        }
    }
}
