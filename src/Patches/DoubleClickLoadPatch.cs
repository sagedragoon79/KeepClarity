using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Double-click a save to load it, skipping the "Load this game?" confirmation.
    /// Works on BOTH the main-menu start screen (<see cref="StartSceneManager"/>)
    /// and the in-game pause → Load menu (<see cref="UIPauseWindow"/>). A single
    /// click still shows the confirmation as normal. Off by default.
    ///
    /// Both menus pop their confirmation the instant a save row is clicked, and
    /// both hide the save list to do it (a modal group on the start screen, a full
    /// tab switch in the pause menu) — so the second click of a rapid double never
    /// reaches the row. We therefore do single-vs-double disambiguation: the FIRST
    /// click is held briefly (no dialog yet); a SECOND click on the same save inside
    /// the window loads directly; otherwise the window lapses and the normal
    /// confirmation shows. The hold only affects a deliberate single click and feels
    /// like any OS file browser.
    ///
    /// Direct load reuses each menu's own load path (start: ConfirmLoadGame; pause:
    /// CESceneManager.LoadFromWithinGame). A save missing required DLC/mods falls
    /// back to the confirmation so its warning is seen rather than fast-loading a
    /// broken save. Any reflection miss leaves vanilla behavior intact.
    /// </summary>
    internal static class DoubleClickLoad
    {
        // Reentrancy guard: the deferred single-click timer re-invokes the original
        // menu method to show the real dialog — the prefixes must let that run.
        internal static bool Bypass;

        // One held click awaiting a possible double (only one menu is ever open).
        private static string? _pendingFile;
        private static float _pendingTime;
        private static Action? _pendingShowDialog;

        internal static bool Enabled => FFUIOverhaulMod.DoubleClickLoad?.Value ?? false;
        internal static float Window => FFUIOverhaulMod.DoubleClickLoadWindow?.Value ?? 0.35f;

        /// <summary>Shared prefix logic. Returns false to swallow the original
        /// (we either loaded directly or are holding the click). <paramref name="directLoad"/>
        /// loads the save now; <paramref name="showDialog"/> re-shows the menu's own
        /// confirmation later (or immediately, on missing content).</summary>
        internal static bool Handle(string file, Action directLoad, Action showDialog)
        {
            float now = Time.unscaledTime;
            if (_pendingFile == file && (now - _pendingTime) <= Window)
            {
                _pendingFile = null;
                _pendingShowDialog = null;
                directLoad();
                return false;
            }
            _pendingFile = file;
            _pendingTime = now;
            _pendingShowDialog = showDialog;
            return false;
        }

        /// <summary>Driven from Plugin.OnUpdate (runs on the start scene AND the
        /// gameplay scene). Flushes a held single click into the normal
        /// confirmation once the double-click window lapses.</summary>
        public static void Tick()
        {
            if (_pendingFile == null || Time.unscaledTime - _pendingTime <= Window) return;
            var show = _pendingShowDialog;
            _pendingFile = null;
            _pendingShowDialog = null;
            show?.Invoke();
        }

        /// <summary>Drop any held click (scene change invalidates the menu ref).</summary>
        public static void ResetState()
        {
            _pendingFile = null;
            _pendingShowDialog = null;
        }

        /// <summary>Re-invoke an original menu method with the reentrancy guard set,
        /// so its prefix runs the vanilla body and the real confirmation appears.</summary>
        internal static void ShowVanilla(MethodInfo method, object instance, string file)
        {
            if (method == null || instance == null) return;
            Bypass = true;
            try { method.Invoke(instance, new object[] { file }); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] deferred dialog failed: " + e.Message); }
            finally { Bypass = false; }
        }

        /// <summary>Mirror FF's own confirmation check: a save needing missing DLC or
        /// mods keeps the dialog (its red warning) instead of fast-loading.</summary>
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

    /// <summary>Main-menu start screen. OnSavedGameSelected is load-only here;
    /// direct load = the private ConfirmLoadGame(string).</summary>
    [HarmonyPatch(typeof(StartSceneManager), "OnSavedGameSelected")]
    internal static class DoubleClickLoad_StartScene
    {
        private static MethodInfo? _onSelected;
        private static MethodInfo? _confirmLoad;
        private static bool _resolved;

        [HarmonyPrefix]
        private static bool Prefix(StartSceneManager __instance, string savedGameFileName)
        {
            if (!DoubleClickLoad.Enabled || DoubleClickLoad.Bypass) return true;
            Resolve();
            if (_onSelected == null || _confirmLoad == null) return true; // inert → vanilla

            var inst = __instance;
            var file = savedGameFileName;
            Action showDialog = () => DoubleClickLoad.ShowVanilla(_onSelected!, inst, file);
            Action directLoad = () =>
            {
                if (DoubleClickLoad.HasMissingContent(file)) { showDialog(); return; }
                try { _confirmLoad!.Invoke(inst, new object[] { file }); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] start-scene load failed: " + e.Message); showDialog(); }
            };
            return DoubleClickLoad.Handle(file, directLoad, showDialog);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _onSelected = AccessTools.Method(typeof(StartSceneManager), "OnSavedGameSelected", new[] { typeof(string) });
            _confirmLoad = AccessTools.Method(typeof(StartSceneManager), "ConfirmLoadGame", new[] { typeof(string) });
            if (_onSelected == null || _confirmLoad == null)
                FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] StartSceneManager methods not found — start-menu double-click inert.");
        }
    }

    /// <summary>In-game pause → Load menu. OnSavedGameSelected serves both the SAVE
    /// (overwrite) and LOAD tabs, so we only intercept when currentTab == LOAD_GAME;
    /// direct load = CESceneManager.LoadFromWithinGame(file).</summary>
    [HarmonyPatch(typeof(UIPauseWindow), "OnSavedGameSelected")]
    internal static class DoubleClickLoad_PauseWindow
    {
        private static MethodInfo? _onSelected;
        private static FieldInfo? _currentTab;
        private static bool _resolved;

        [HarmonyPrefix]
        private static bool Prefix(UIPauseWindow __instance, string savedGameFileNameNoExtension)
        {
            if (!DoubleClickLoad.Enabled || DoubleClickLoad.Bypass) return true;
            Resolve();
            if (_onSelected == null || _currentTab == null) return true; // inert → vanilla

            // Only the Load tab — never hijack a Save/overwrite click.
            var tab = _currentTab.GetValue(__instance);
            if (tab == null || tab.ToString() != "LOAD_GAME") return true;

            var inst = __instance;
            var file = savedGameFileNameNoExtension;
            Action showDialog = () => DoubleClickLoad.ShowVanilla(_onSelected!, inst, file);
            Action directLoad = () =>
            {
                if (DoubleClickLoad.HasMissingContent(file)) { showDialog(); return; }
                try { UnitySingletonPersistent<CESceneManager>.Instance.LoadFromWithinGame(file); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] pause-menu load failed: " + e.Message); showDialog(); }
            };
            return DoubleClickLoad.Handle(file, directLoad, showDialog);
        }

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _onSelected = AccessTools.Method(typeof(UIPauseWindow), "OnSavedGameSelected", new[] { typeof(string) });
            _currentTab = AccessTools.Field(typeof(UIPauseWindow), "currentTab");
            if (_onSelected == null || _currentTab == null)
                FFUIOverhaulMod.Log.Warning("[DoubleClickLoad] UIPauseWindow members not found — pause-menu double-click inert.");
        }
    }
}
