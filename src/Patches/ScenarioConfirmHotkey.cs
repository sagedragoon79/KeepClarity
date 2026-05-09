using UnityEngine;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Hotkey support for the "We've finished scouting the surrounding area"
    /// scenario popup that appears at the start of a new game (vanilla class
    /// <see cref="UIStartScenarioWindow"/>). Pressing Space, Enter, or Esc
    /// invokes the popup's confirm button — no need to chase a tiny target
    /// when the cursor is finicky.
    ///
    /// Polling design: we cache the window once we find it (FindObjectOfType
    /// is cheap when nothing matches and quick when it does, but we don't
    /// want it on a hot path). After cache hit, the per-frame work is a
    /// null check + a single <c>activeInHierarchy</c> property read + the
    /// pref check — sub-microsecond. Earlier attempts at this in the RR
    /// session ran heavier work every frame (per the user's notes) and the
    /// game tick visibly stuttered every two seconds; this version only
    /// does meaningful work when the popup is actually on screen.
    /// </summary>
    internal static class ScenarioConfirmHotkey
    {
        private static UIStartScenarioWindow? _window;
        private static int _findAttempts;

        public static void Tick()
        {
            if (FFUIOverhaulMod.EnableScenarioConfirmHotkey == null
                || !FFUIOverhaulMod.EnableScenarioConfirmHotkey.Value) return;

            // Find once, cache. Unity's overloaded == returns true for
            // destroyed-but-not-null, so a stale reference auto-renulls and
            // we re-find on the next try. _findAttempts caps the search rate
            // before the panel is loaded so we don't FindObjectOfType every
            // frame on the main menu.
            if (_window == null)
            {
                if (++_findAttempts > 60)
                {
                    _findAttempts = 0;
                    _window = Object.FindObjectOfType<UIStartScenarioWindow>(includeInactive: true);
                }
                if (_window == null) return;
            }

            // The panel uses a CanvasGroup for its visibility — gameObject can
            // stay active while alpha goes 0. Check both: the GO must be
            // active in hierarchy AND the CanvasGroup must be visible+
            // interactable. Otherwise we'd intercept Space presses meant for
            // other panels (or post-scenario gameplay pause).
            var go = _window.gameObject;
            if (go == null || !go.activeInHierarchy) return;
            var cg = _window.canvasGroup;
            if (cg == null || cg.alpha < 0.5f || !cg.interactable) return;

            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Escape))
            {
                if (_window.confirmButton != null && _window.confirmButton.interactable)
                    _window.confirmButton.onClick.Invoke();
            }
        }
    }
}
