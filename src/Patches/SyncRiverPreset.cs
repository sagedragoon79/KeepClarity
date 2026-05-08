using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace FFUIOverhaul.Patches
{
    /// <summary>
    /// Two-way sync between Rivers Restored's <c>RiverPreset</c> setting and
    /// FF's New Settlement <c>mapType</c>. Five biomes map 1:1 (Plains,
    /// AridHighlands, LowlandLakes, AlpineValleys, IdyllicValley); the rest
    /// are no-ops in their respective directions:
    ///   - RR.Custom → no override (FF stays at its default IdyllicValley)
    ///   - FF.Random / Custom / LegacyMap / Wasteland → RR untouched
    ///
    /// All RR access is reflection-based so KC has no hard dependency on
    /// Rivers Restored. If RR isn't installed, the sync silently no-ops.
    /// </summary>
    internal static class SyncRiverPreset
    {
        private static bool _resolveAttempted;
        private static object? _riverPresetEntry; // MelonPreferences_Entry<RiverPresetMode>
        private static PropertyInfo? _entryValueProp;
        private static Type? _riverPresetEnum;
        private static object? _riversEnabledEntry; // MelonPreferences_Entry<bool>
        private static PropertyInfo? _enabledValueProp;

        // Map FF MapType (string) → RR RiverPresetMode (string).
        // Values that don't exist in the other enum aren't listed.
        private static readonly System.Collections.Generic.Dictionary<string, string> _mapTypeToRiverPreset =
            new(StringComparer.Ordinal)
            {
                { "Plains",        "Plains" },
                { "AridHighlands", "AridHighlands" },
                { "LowlandLakes",  "LowlandLakes" },
                { "AlpineValleys", "AlpineValleys" },
                { "IdyllicValley", "IdyllicValley" },
            };

        private static readonly System.Collections.Generic.Dictionary<string, string> _riverPresetToMapType =
            new(StringComparer.Ordinal)
            {
                { "Plains",        "Plains" },
                { "AridHighlands", "AridHighlands" },
                { "LowlandLakes",  "LowlandLakes" },
                { "AlpineValleys", "AlpineValleys" },
                { "IdyllicValley", "IdyllicValley" },
                // "Custom" intentionally absent — leave FF at default
            };

        /// <summary>Resolve RR's preset entry once per session. Cheap if RR isn't loaded — early-returns.</summary>
        private static void Resolve()
        {
            if (_resolveAttempted) return;
            _resolveAttempted = true;

            // RR's class lives in its own assembly; locate by simple type name.
            Type? rrModType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                if (types == null) continue;
                foreach (var t in types)
                {
                    if (t == null) continue;
                    if (t.FullName == "RiversRestored.RiversRestoredMod" || t.Name == "RiversRestoredMod")
                    {
                        rrModType = t;
                        break;
                    }
                }
                if (rrModType != null) break;
            }
            if (rrModType == null) return;

            // The RiverPreset entry is exposed as a static field/property on the mod class.
            var f = rrModType.GetField("RiverPreset",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object? entry = f?.GetValue(null);
            if (entry == null)
            {
                var p = rrModType.GetProperty("RiverPreset",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                entry = p?.GetValue(null);
            }
            if (entry == null) return;

            _riverPresetEntry = entry;
            _entryValueProp = entry.GetType().GetProperty("Value");

            // Find the RiverPresetMode enum type (it's the generic type arg of
            // MelonPreferences_Entry<T>). Easiest: just look at the entry's Value type.
            if (_entryValueProp != null) _riverPresetEnum = _entryValueProp.PropertyType;

            // Master kill-switch — if the player disabled RR via its config
            // file (or in-panel toggle), the preset value is stale and we
            // should NOT force FF's mapType to it.
            var enabledF = rrModType.GetField("RiversEnabled",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object? enabled = enabledF?.GetValue(null);
            if (enabled == null)
            {
                var enabledP = rrModType.GetProperty("RiversEnabled",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                enabled = enabledP?.GetValue(null);
            }
            if (enabled != null)
            {
                _riversEnabledEntry = enabled;
                _enabledValueProp = enabled.GetType().GetProperty("Value");
            }
        }

        private static bool RiversEnabled()
        {
            // Treat "couldn't resolve the toggle" as enabled — fail open. The
            // dropdown sync is still gated by KC's own SyncMapTypeWithRiverPreset
            // pref, so a player can always disable from KC's side.
            if (_riversEnabledEntry == null || _enabledValueProp == null) return true;
            var v = _enabledValueProp.GetValue(_riversEnabledEntry);
            return v is bool b ? b : true;
        }

        /// <summary>Returns the FF MapType string matching RR's current preset, or null if no override should apply.</summary>
        public static string? GetForwardMapType()
        {
            Resolve();
            if (_riverPresetEntry == null || _entryValueProp == null) return null;
            if (!RiversEnabled()) return null; // RR off — leave FF default alone
            var current = _entryValueProp.GetValue(_riverPresetEntry);
            if (current == null) return null;
            string name = current.ToString();
            if (string.IsNullOrEmpty(name)) return null;
            return _riverPresetToMapType.TryGetValue(name, out var mapped) ? mapped : null;
        }

        /// <summary>Pushes a FF MapType selection back into RR's preset, if both sides recognize the value.</summary>
        public static void SetReverseRiverPreset(string mapTypeName)
        {
            Resolve();
            if (_riverPresetEntry == null || _entryValueProp == null || _riverPresetEnum == null) return;
            if (!RiversEnabled()) return; // RR off — don't write to its preset
            if (!_mapTypeToRiverPreset.TryGetValue(mapTypeName, out var rrName)) return;

            try
            {
                var enumValue = Enum.Parse(_riverPresetEnum, rrName);
                _entryValueProp.SetValue(_riverPresetEntry, enumValue);
                MelonPreferences.Save();
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[RR Sync] Failed to set RiverPreset to '{rrName}': {e.Message}");
            }
        }
    }

    /// <summary>
    /// Forward sync: when the New Settlement panel awakes, if RR's preset is
    /// one of the five shared biomes, override FF's default mapType to match.
    /// Re-runs RerollMap so the seed lands within the biome's expected ranges.
    /// </summary>
    [HarmonyPatch(typeof(UIStartMenu_NewSettlement), "Awake")]
    public class PatchSyncMapTypeFromRiverPreset
    {
        private static FieldInfo? _mapTypeField;
        private static MethodInfo? _rerollMap;
        private static MethodInfo? _updateMapTypeDisplay;
        private static Type? _ffMapTypeEnum;

        public static void Postfix(UIStartMenu_NewSettlement __instance)
        {
            if (FFUIOverhaulMod.SyncMapTypeWithRiverPreset == null
                || !FFUIOverhaulMod.SyncMapTypeWithRiverPreset.Value)
                return;

            try
            {
                var ffName = SyncRiverPreset.GetForwardMapType();
                if (ffName == null) return;

                _mapTypeField ??= typeof(UIStartMenu_NewSettlement).GetField(
                    "mapType", BindingFlags.Instance | BindingFlags.NonPublic);
                if (_mapTypeField == null) return;

                _ffMapTypeEnum ??= _mapTypeField.FieldType;
                var newValue = Enum.Parse(_ffMapTypeEnum, ffName);
                _mapTypeField.SetValue(__instance, newValue);

                _rerollMap ??= typeof(UIStartMenu_NewSettlement).GetMethod(
                    "RerollMap", BindingFlags.Instance | BindingFlags.NonPublic);
                _updateMapTypeDisplay ??= typeof(UIStartMenu_NewSettlement).GetMethod(
                    "UpdateMapTypeDisplay", BindingFlags.Instance | BindingFlags.NonPublic);

                _rerollMap?.Invoke(__instance, null);
                _updateMapTypeDisplay?.Invoke(__instance, null);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[RR Sync] Forward sync failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Reverse sync: when the player picks a terrain in the panel, push the
    /// selection back into RR's RiverPreset (only for the 5 shared biomes).
    /// </summary>
    [HarmonyPatch(typeof(UIStartMenu_NewSettlement), "OnMapTypeSelected")]
    public class PatchSyncRiverPresetFromMapType
    {
        public static void Postfix(SettingsManager.MapType _mapType)
        {
            if (FFUIOverhaulMod.SyncMapTypeWithRiverPreset == null
                || !FFUIOverhaulMod.SyncMapTypeWithRiverPreset.Value)
                return;
            try { SyncRiverPreset.SetReverseRiverPreset(_mapType.ToString()); }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[RR Sync] Reverse sync failed: {e.Message}"); }
        }
    }
}
