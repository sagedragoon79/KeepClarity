using System;
using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// Lets the player hold Ctrl for the overhead view while capture is armed —
    /// the same affordance FF gives while placing a building, which is exactly
    /// when you want to see the grid square-on.
    ///
    /// HOW: FF already implements this in CameraManager.LateUpdate:
    ///
    ///     if (placementModeActive &amp;&amp; (Input.GetKey(LeftControl) || Input.GetKey(RightControl)))
    ///         → lift the camera and pitch it to 89.999°
    ///
    /// so the whole feature is just making that flag true while we're armed. We
    /// get FF's own framing, zoom-out amount and behaviour for free.
    ///
    /// WHY REFLECT A PRIVATE FIELD rather than raise EnterPlacementModeEvent:
    /// three systems listen to that event (camera, input, UI), and firing it would
    /// pull in input-state and UI side effects that have nothing to do with a
    /// camera angle. Setting the one field the camera reads is narrow and
    /// predictable, and it fails closed — if the field ever moves, top-down simply
    /// doesn't engage and capture still works.
    /// </summary>
    internal static class TopDownCamera
    {
        private static FieldInfo? _placementModeActive;
        private static bool _resolved;
        private static bool _applied;

        /// <summary>Mirror our armed state into the camera's placement flag.</summary>
        public static void SetActive(bool active)
        {
            if (_applied == active) return;

            var cam = Resolve();
            if (cam == null || _placementModeActive == null) return;

            try
            {
                _placementModeActive.SetValue(cam, active);
                _applied = active;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[Camera] top-down toggle failed: " + e.Message);
                _placementModeActive = null;   // stop retrying every frame
            }
        }

        /// <summary>Called on scene change: forget state so we don't try to write
        /// to a CameraManager from the previous map.</summary>
        public static void Reset()
        {
            _applied = false;
            _resolved = false;
            _placementModeActive = null;
        }

        private static CameraManager? Resolve()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            var cam = gm != null ? gm.cameraManager : null;
            if (cam == null) return null;

            if (!_resolved)
            {
                _resolved = true;
                _placementModeActive = typeof(CameraManager).GetField(
                    "placementModeActive", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_placementModeActive == null)
                    FFUIOverhaulMod.Log.Warning(
                        "[Camera] CameraManager.placementModeActive not found — " +
                        "Ctrl top-down won't engage during capture (capture itself is unaffected).");
            }
            return cam;
        }
    }
}
