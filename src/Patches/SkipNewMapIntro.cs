using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Skips the start-of-game cinematic that plays after starting a new map
    /// (the one with the "Cinematic001..008" subtitle blurbs). Hitting Esc
    /// already cancels it in vanilla, but the player still hears the first
    /// few seconds of audio + sees the fade. With this enabled we transition
    /// the StartSceneManager state machine straight from cinematic-pending
    /// to LOAD_GAME, bypassing the video entirely.
    ///
    /// Postfix on StartSceneManager.Update: if startState == PLAYING_CINEMATIC
    /// and the pref is on, jump to LOAD_GAME. Reflection on the private state
    /// field + nested StartSceneState enum so the patch silently no-ops on a
    /// future game-side rename rather than breaking the launch flow.
    /// </summary>
    [HarmonyPatch(typeof(StartSceneManager), "Update")]
    public class PatchSkipNewMapIntro
    {
        private static FieldInfo? _stateField;
        private static object? _playingCinematic;
        private static object? _loadGame;
        private static bool _resolveAttempted;

        public static void Postfix(StartSceneManager __instance)
        {
            if (FFUIOverhaulMod.SkipNewMapIntro == null || !FFUIOverhaulMod.SkipNewMapIntro.Value) return;

            try
            {
                if (!_resolveAttempted) Resolve();
                if (_stateField == null || _playingCinematic == null || _loadGame == null) return;

                var current = _stateField.GetValue(__instance);
                if (current?.Equals(_playingCinematic) == true)
                {
                    _stateField.SetValue(__instance, _loadGame);
                    // Vanilla restores Cursor.visible inside the
                    // PRE_DONE_PLAYING_CINEMATIC state we're skipping over.
                    // Without this the player's cursor stays hidden into the
                    // first scenario screen ("Choose your TC location").
                    Cursor.visible = true;
                }
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[SkipIntro] Postfix failed: {e.Message}");
            }
        }

        private static void Resolve()
        {
            _resolveAttempted = true;
            var t = typeof(StartSceneManager);
            _stateField = t.GetField("startState", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_stateField == null) return;

            // StartSceneState is nested inside StartSceneManager (or in the
            // same assembly). Pull the enum type from the field itself, then
            // parse the values we need by name.
            var enumType = _stateField.FieldType;
            try
            {
                _playingCinematic = Enum.Parse(enumType, "PLAYING_CINEMATIC");
                _loadGame = Enum.Parse(enumType, "LOAD_GAME");
            }
            catch
            {
                FFUIOverhaulMod.Log.Warning("[SkipIntro] Could not parse PLAYING_CINEMATIC / LOAD_GAME on StartSceneState — skip will be inert.");
            }
        }
    }
}
