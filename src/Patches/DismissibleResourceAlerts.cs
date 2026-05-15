using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Lets the player dismiss a top-bar resource "!" alert by clicking on it.
    /// Dismissals last until the next in-game month change, at which point
    /// FF's normal alert thresholds re-evaluate and the alert reappears if
    /// the shortage is still real.
    ///
    /// Design:
    ///   - One <see cref="AlertClickGuard"/> MonoBehaviour is attached to each
    ///     UIResourceAlert GameObject the first time vanilla updates it.
    ///   - The guard listens for pointer clicks (it's a tiny "!" badge, the
    ///     intent is obvious enough to make any click count, not just right-
    ///     click). On click it adds the alert's InstanceID to the dismissal
    ///     set and SetActive(false)s the GameObject.
    ///   - Vanilla's UpdateAlert may reactivate the alert on subsequent ticks
    ///     when the count is still below threshold; our Postfix on
    ///     UpdateAlert(int) re-hides it if the InstanceID is in the set.
    ///   - Plugin.OnUpdate calls <see cref="Tick"/> which checks the game's
    ///     current month — when it changes, the dismissal set is cleared.
    /// </summary>
    public static class DismissibleResourceAlerts
    {
        private static readonly HashSet<int> _dismissed = new();
        private static int _lastMonth = int.MinValue;

        public static void NotifyDismissed(int instanceId) => _dismissed.Add(instanceId);
        public static bool IsDismissed(int instanceId) => _dismissed.Contains(instanceId);

        /// <summary>Called from KC.OnUpdate. Cheap — single int compare per frame.</summary>
        public static void Tick()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm?.timeManager == null) return;
            int month = gm.timeManager.currentDate.month;
            if (_lastMonth == int.MinValue) { _lastMonth = month; return; }
            if (month != _lastMonth)
            {
                _lastMonth = month;
                if (_dismissed.Count > 0) _dismissed.Clear();
            }
        }
    }

    /// <summary>
    /// Attached at runtime to each UIResourceAlert GameObject. Pointer clicks
    /// register the alert as dismissed and immediately hide it.
    /// </summary>
    internal class AlertClickGuard : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData e)
        {
            if (FFUIOverhaulMod.DismissibleResourceAlerts == null
                || !FFUIOverhaulMod.DismissibleResourceAlerts.Value) return;

            DismissibleResourceAlerts.NotifyDismissed(gameObject.GetInstanceID());
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Postfix on every UpdateAlert overload. Re-hides the alert if it was
    /// dismissed this month, and ensures the click guard is attached.
    /// </summary>
    [HarmonyPatch(typeof(UIResourceAlert), nameof(UIResourceAlert.UpdateAlert), new[] { typeof(int) })]
    public class PatchUIResourceAlertUpdateInt
    {
        public static void Postfix(UIResourceAlert __instance) => Handle(__instance);

        internal static void Handle(UIResourceAlert alert)
        {
            if (alert == null) return;
            if (FFUIOverhaulMod.DismissibleResourceAlerts == null
                || !FFUIOverhaulMod.DismissibleResourceAlerts.Value) return;

            var go = alert.gameObject;
            if (go == null) return;

            // Idempotent: AddComponent only attaches if one isn't there yet.
            if (go.GetComponent<AlertClickGuard>() == null)
                go.AddComponent<AlertClickGuard>();

            // Ensure the alert's Image catches raycasts — without it, clicks
            // fall through to whatever's behind the icon.
            var img = go.GetComponent<Image>();
            if (img != null && !img.raycastTarget) img.raycastTarget = true;

            if (DismissibleResourceAlerts.IsDismissed(go.GetInstanceID()) && go.activeSelf)
                go.SetActive(false);
        }
    }
}
