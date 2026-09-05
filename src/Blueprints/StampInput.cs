using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>Owns the stamp hotkey and drives the stamp module.</summary>
    internal static class StampInput
    {
        private static Chord? _chord;

        public static void OnMapLoaded()
        {
            _chord = Chord.Parse(FFUIOverhaulMod.StampHotkey.Value, KeyCode.V);
            FFUIOverhaulMod.Log.Msg($"[Stamp] press {_chord} to stamp the selected blueprint.");
        }

        public static void OnUpdate()
        {
            if (_chord == null) return;
            if (_chord.Down()) BlueprintStamp.Toggle();
            BlueprintStamp.OnUpdate();
        }
    }
}
