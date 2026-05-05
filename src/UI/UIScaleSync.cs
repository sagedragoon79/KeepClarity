using UnityEngine;
using UnityEngine.UI;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Drives an overlay canvas's CanvasScaler from our own MelonPref
    /// (<see cref="FFUIOverhaulMod.OverlayUIScale"/>). Replaces the earlier
    /// "mirror the game's scaler" approach, which tied us to FF's UI Scale
    /// slider. Players asked for an independent slider so the overlays can be
    /// shrunk or grown without changing the rest of the HUD.
    ///
    /// Cheap call: a tolerance check skips the assignment most ticks (touching
    /// CanvasScaler.scaleFactor dirties the canvas and forces a layout rebuild).
    /// </summary>
    public static class UIScaleSync
    {
        public static void Sync(CanvasScaler? targetScaler)
        {
            if (targetScaler == null) return;
            float wanted = FFUIOverhaulMod.OverlayUIScale?.Value ?? 1.0f;
            wanted = Mathf.Clamp(wanted, 0.3f, 3f);

            // ConstantPixelSize mode is the simplest scale model — scaleFactor
            // is just a flat multiplier on every pixel measurement in the
            // canvas. Force the mode in case someone left it on something else.
            if (targetScaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize)
                targetScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            if (Mathf.Abs(targetScaler.scaleFactor - wanted) < 0.005f) return;
            targetScaler.scaleFactor = wanted;
        }
    }
}
