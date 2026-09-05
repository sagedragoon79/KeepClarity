using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Owns the capture chord and feeds its held-state to
    /// <see cref="BlueprintCapture"/>. Kept separate so the capture module stays
    /// about geometry and building selection rather than input plumbing.
    ///
    /// Like the spike, this announces itself on every map load — a feature that
    /// fails silently is indistinguishable from a dead hotkey, which cost several
    /// test rounds during M0.
    /// </summary>
    internal static class CaptureInput
    {
        private static Chord? _chord;

        public static void OnMapLoaded()
        {
            _chord = Chord.Parse(FFUIOverhaulMod.CaptureHotkey.Value, KeyCode.C);
            FFUIOverhaulMod.Log.Msg($"[Capture] ready: press {_chord} to arm capture mode, then click-drag on the ground.");
        }

        public static void OnUpdate()
        {
            if (_chord == null) return;
            // Press to arm/disarm; the drag itself is mouse-driven, matching how
            // FF's own crop-field and graveyard placement works.
            if (_chord.Down()) BlueprintCapture.ToggleArmed();
            BlueprintCapture.OnUpdate();
        }
    }
}
