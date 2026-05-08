using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine.UI;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// "Remember Choices" QoL — preserves the player's selections in the New
    /// Settlement → Custom Settings panel across game launches, so they don't
    /// have to re-toggle 8 boolean options + 11 multiplier sliders every time.
    ///
    /// On Confirm: snapshot the master toggle + 19 cstm* values into a single
    /// MelonPref string. On panel Awake: if the pref is enabled and a snapshot
    /// exists, write the values back into the UI controls — the controls
    /// already raise OnValueChanged handlers that propagate into
    /// SettingsManager, so we don't need to also touch the static fields.
    ///
    /// The pref toggle defaults off; players opt in. Reflection is used for
    /// every private field/method on UIStartMenu_NewSettlement so a future
    /// game-side rename causes the patch to silently no-op (warning logged)
    /// instead of breaking the New Settlement panel.
    /// </summary>
    internal static class RememberCustomChoices
    {
        // Field names on UIStartMenu_NewSettlement — toggles + sliders. Order
        // matters for nothing, but keep cohesive groups for readability.
        private static readonly string[] ToggleFields =
        {
            "enableCustomSettingsButton", // master enable
            "cstmDisableDisease",
            "cstmDisableHunger",
            "cstmDisableCropDisease",
            "cstmDisableAnimalAttacks",
            "cstmDisableRaiderAttacks",
            "cstmDisableDesirability",
            "cstmDisableSpirituality",
            "cstmEnableFreeBuildings",
        };

        private static readonly string[] SliderFields =
        {
            "cstmCoal",
            "cstmIron",
            "cstmGold",
            "cstmClay",
            "cstmSand",
            "cstmForageables",
            "cstmDeer",
            "cstmWolves",
            "cstmBoars",
            "cstmFish",
            "cstmKpu",
        };

        public static void TakeSnapshot(object panelInstance)
        {
            if (panelInstance == null) return;
            var t = panelInstance.GetType();
            var parts = new List<string>(ToggleFields.Length + SliderFields.Length);

            foreach (var name in ToggleFields)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                var ce = f.GetValue(panelInstance);
                bool on = ReadBool(ce);
                parts.Add(name + "=" + (on ? "1" : "0"));
            }

            foreach (var name in SliderFields)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null) continue;
                if (f.GetValue(panelInstance) is Slider s)
                    parts.Add(name + "=" + ((int)s.value).ToString());
            }

            FFUIOverhaulMod.RememberCustomSettingsSnapshot.Value = string.Join("|", parts);
            MelonPreferences.Save();
        }

        public static void RestoreSnapshot(object panelInstance)
        {
            if (panelInstance == null) return;
            var raw = FFUIOverhaulMod.RememberCustomSettingsSnapshot?.Value ?? "";
            if (string.IsNullOrEmpty(raw)) return;

            var t = panelInstance.GetType();
            var values = new Dictionary<string, string>();
            foreach (var entry in raw.Split('|'))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                values[entry.Substring(0, eq)] = entry.Substring(eq + 1);
            }

            // Toggles: SetToggledWithoutNotify avoids triggering visual chains
            // mid-restore; we then call the matching OnXxxChanged handler so
            // SettingsManager.cstm* picks up the new value (same pattern
            // OnCustomSettingsCancel uses to revert).
            foreach (var name in ToggleFields)
            {
                if (!values.TryGetValue(name, out var v)) continue;
                bool on = v == "1";
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                var ce = f?.GetValue(panelInstance);
                if (ce == null) continue;
                SetToggledWithoutNotify(ce, on);
                InvokeChangeHandler(panelInstance, t, name, ce);
            }

            foreach (var name in SliderFields)
            {
                if (!values.TryGetValue(name, out var v)) continue;
                if (!int.TryParse(v, out int level)) continue;
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                if (f?.GetValue(panelInstance) is Slider s)
                    s.value = level; // fires onValueChanged → updates SettingsManager
            }

            // Master toggle visual + the external "Custom Settings" button
            // swap. FF only flips between customSettingsButton_OFF (gray) and
            // customSettingsButton_ON (orange) inside OnCustomSettingsClose,
            // which our restore path doesn't go through. So we mirror that
            // swap here once we know the master toggle's state.
            SyncMasterButtonVisibility(panelInstance, t, values);
        }

        private static void SyncMasterButtonVisibility(object panel, Type t, Dictionary<string, string> values)
        {
            if (!values.TryGetValue("enableCustomSettingsButton", out var v)) return;
            bool on = v == "1";

            var offToggle = t.GetField("customSettingsButton_OFF", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(panel) as UnityEngine.MonoBehaviour;
            var onToggle = t.GetField("customSettingsButton_ON", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(panel) as UnityEngine.MonoBehaviour;
            if (offToggle != null && onToggle != null)
            {
                offToggle.gameObject.SetActive(!on);
                offToggle.enabled = !on;
                onToggle.gameObject.SetActive(on);
                onToggle.enabled = on;
            }
        }

        // ── Reflection helpers (cached per call site as needed) ────────────

        private static bool ReadBool(object? ceToggle)
        {
            if (ceToggle == null) return false;
            var prop = ceToggle.GetType().GetProperty("isToggledOn");
            if (prop != null) return (bool)(prop.GetValue(ceToggle) ?? false);
            return false;
        }

        private static void SetToggledWithoutNotify(object ceToggle, bool on)
        {
            var m = ceToggle.GetType().GetMethod("SetToggledWithoutNotify",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            m?.Invoke(ceToggle, new object[] { on });
        }

        // For each toggle field "cstmDisableHunger", call the panel's
        // matching "OnDisableHungerChanged" handler so SettingsManager picks
        // up the changed value. Field "enableCustomSettingsButton" maps to
        // OnEnableCustomSettings. Anything else is no-op.
        private static void InvokeChangeHandler(object panel, Type t, string fieldName, object ceToggle)
        {
            string? methodName = fieldName switch
            {
                "enableCustomSettingsButton" => "OnEnableCustomSettings",
                "cstmDisableDisease" => "OnDisableDiseaseChanged",
                "cstmDisableHunger" => "OnDisableHungerChanged",
                "cstmDisableCropDisease" => "OnDisableCropDiseaseChanged",
                "cstmDisableAnimalAttacks" => "OnDisableAnimalAttacks",
                "cstmDisableRaiderAttacks" => "OnDisableRaiderAttacksChanged",
                "cstmDisableDesirability" => "OnDisableDesirabilityChanged",
                "cstmDisableSpirituality" => "OnDisableSpiritualityChanged",
                "cstmEnableFreeBuildings" => "OnEnableFreeBuildingsChanged",
                _ => null,
            };
            if (methodName == null) return;
            var m = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (m == null) return;
            try { m.Invoke(panel, new object[] { ceToggle }); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[RememberChoices] {methodName} failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(UIStartMenu_NewSettlement), "Awake")]
    public class PatchNewSettlementAwakeRestore
    {
        public static void Postfix(UIStartMenu_NewSettlement __instance)
        {
            if (FFUIOverhaulMod.RememberCustomSettings == null || !FFUIOverhaulMod.RememberCustomSettings.Value) return;
            try { RememberCustomChoices.RestoreSnapshot(__instance); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[RememberChoices] Restore failed: {e.Message}"); }
        }
    }

    [HarmonyPatch(typeof(UIStartMenu_NewSettlement), "OnCustomSettingsConfirm")]
    public class PatchOnCustomSettingsConfirmSnapshot
    {
        public static void Postfix(UIStartMenu_NewSettlement __instance)
        {
            if (FFUIOverhaulMod.RememberCustomSettings == null || !FFUIOverhaulMod.RememberCustomSettings.Value) return;
            try { RememberCustomChoices.TakeSnapshot(__instance); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[RememberChoices] Snapshot failed: {e.Message}"); }
        }
    }
}
