using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>Owns the blueprint panel's hotkey.</summary>
    internal static class PanelInput
    {
        private static Chord? _chord;

        public static void OnMapLoaded()
        {
            _chord = Chord.Parse(FFUIOverhaulMod.PanelHotkey.Value, KeyCode.B);
            FFUIOverhaulMod.Log.Msg($"[Panel] press {_chord} for the blueprint library.");
        }

        public static void OnUpdate()
        {
            if (_chord == null) return;
            if (_chord.Down()) BlueprintPanel.Toggle();
        }
    }
}
