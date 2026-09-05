using System;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// A parsed hotkey chord ("Shift+C", "Ctrl+Alt+B", "F8").
    ///
    /// Exact modifier matching is deliberate: "Shift+C" must not fire on
    /// Ctrl+Shift+C, or chords quietly collide with each other and with the
    /// game's own bindings.
    ///
    /// (StampSpike carries its own copy of this logic — it is temporary M0
    /// scaffolding that gets deleted once real stamping lands, so it isn't worth
    /// refactoring proven code to share this.)
    /// </summary>
    internal class Chord
    {
        public KeyCode Key { get; private set; } = KeyCode.None;
        public bool Ctrl { get; private set; }
        public bool Alt { get; private set; }
        public bool Shift { get; private set; }

        public static Chord Parse(string text, KeyCode fallback)
        {
            var c = new Chord { Key = fallback };
            if (string.IsNullOrEmpty(text)) return c;

            foreach (var raw in text.Split('+'))
            {
                var p = raw.Trim();
                if (p.Length == 0) continue;
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                    p.Equals("Control", StringComparison.OrdinalIgnoreCase)) { c.Ctrl = true; continue; }
                if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) { c.Alt = true; continue; }
                if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) { c.Shift = true; continue; }
                try { c.Key = (KeyCode)Enum.Parse(typeof(KeyCode), p, ignoreCase: true); }
                catch { FFUIOverhaulMod.Log.Warning($"[Chord] unrecognized key '{p}' in '{text}'."); }
            }
            return c;
        }

        private bool ModifiersMatch()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return ctrl == Ctrl && alt == Alt && shift == Shift;
        }

        /// <summary>True on the frame the chord is pressed.</summary>
        public bool Down() => Input.GetKeyDown(Key) && ModifiersMatch();

        /// <summary>Like Down(), but ignores Ctrl. Used for actions that must keep
        /// working while Ctrl is held for the overhead camera.</summary>
        public bool DownIgnoringCtrl()
        {
            if (!Input.GetKeyDown(Key)) return false;
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            return alt == Alt && shift == Shift;
        }

        /// <summary>True while the chord is held — what a drag needs.</summary>
        public bool Held() => Input.GetKey(Key) && ModifiersMatch();

        public override string ToString()
        {
            var s = "";
            if (Ctrl) s += "Ctrl+";
            if (Alt) s += "Alt+";
            if (Shift) s += "Shift+";
            return s + Key;
        }
    }
}
