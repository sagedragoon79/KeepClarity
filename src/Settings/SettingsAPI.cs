using System;
using System.Reflection;
using MelonLoader;

namespace FFUIOverhaul.Settings
{
    /// <summary>
    /// Public API surface for Keep Clarity's settings manager.
    /// Other mods can call this directly (hard dependency on KeepClarity.dll)
    /// or reflectively via Type.GetType("FFUIOverhaul.Settings.SettingsAPI, KeepClarity") (optional dependency).
    /// </summary>
    public static class SettingsAPI
    {
        public const string Version = "1.0";

        public static event Action<string, string, object?>? OnSettingChanged
        {
            add => SettingsRegistry.SettingChanged += value;
            remove => SettingsRegistry.SettingChanged -= value;
        }

        public static void RegisterMod(string modId, string displayName, string? description = null,
            string? version = null, string? iconResourcePath = null, float[]? accentRgb = null, int order = 0)
        {
            var info = new ModSettingsInfo
            {
                DisplayName = displayName,
                Description = description,
                Version = version,
                IconResourcePath = iconResourcePath,
                Order = order,
            };
            if (accentRgb != null && accentRgb.Length >= 3)
            {
                info.AccentColor = new UnityEngine.Color(accentRgb[0], accentRgb[1], accentRgb[2],
                    accentRgb.Length > 3 ? accentRgb[3] : 1f);
            }
            SettingsRegistry.RegisterMod(modId, info);
        }

        public static void Register<T>(string modId, string modDisplayName, string category,
            MelonPreferences_Entry<T> entry, SettingsMeta? meta = null)
        {
            if (entry == null) return;
            ClaimEntryCategory(modId, entry);
            var rec = new SettingEntryRecord
            {
                ModId = modId,
                ModDisplayName = modDisplayName,
                Category = category,
                Key = entry.Identifier,
                ValueType = typeof(T),
                Get = () => entry.Value,
                Set = v => { if (v is T tv) entry.Value = tv; },
                DefaultValue = entry.DefaultValue,
                VanillaDisplayName = entry.DisplayName,
                VanillaDescription = entry.Description,
                Meta = meta ?? new SettingsMeta(),
                AutoDiscovered = false,
            };
            SettingsRegistry.Add(rec);
        }

        public static void RegisterCategory(string modId, string modDisplayName,
            MelonPreferences_Category category, Action<string, SettingsMeta>? configure = null)
        {
            if (category == null) return;
            SettingsRegistry.ClaimCategory(category.Identifier, modId);
            string catDisplay = category.DisplayName ?? category.Identifier;

            var entriesProp = category.GetType().GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance);
            if (entriesProp?.GetValue(category, null) is System.Collections.IList list)
            {
                foreach (var entryObj in list)
                {
                    if (entryObj == null) continue;
                    var t = entryObj.GetType();
                    string key = t.GetProperty("Identifier")?.GetValue(entryObj, null) as string ?? "";
                    if (string.IsNullOrEmpty(key)) continue;

                    var meta = new SettingsMeta();
                    configure?.Invoke(key, meta);

                    Type valueType = typeof(object);
                    var getReflected = t.GetMethod("GetReflectedType", BindingFlags.Public | BindingFlags.Instance);
                    if (getReflected != null && getReflected.Invoke(entryObj, null) is Type rt) valueType = rt;

                    var captured = entryObj;
                    var boxedProp = t.GetProperty("BoxedValue", BindingFlags.Public | BindingFlags.Instance);
                    var defaultProp = t.GetProperty("BoxedDefaultValue", BindingFlags.Public | BindingFlags.Instance);

                    var rec = new SettingEntryRecord
                    {
                        ModId = modId,
                        ModDisplayName = modDisplayName,
                        Category = catDisplay,
                        Key = key,
                        ValueType = valueType,
                        Get = () => boxedProp?.GetValue(captured, null),
                        Set = v => { if (boxedProp != null && boxedProp.CanWrite) boxedProp.SetValue(captured, v, null); },
                        DefaultValue = defaultProp?.GetValue(captured, null),
                        VanillaDisplayName = t.GetProperty("DisplayName")?.GetValue(captured, null) as string,
                        VanillaDescription = t.GetProperty("Description")?.GetValue(captured, null) as string,
                        Meta = meta,
                        AutoDiscovered = false,
                    };
                    SettingsRegistry.Add(rec);
                }
            }
        }

        public static void SetValue(string modId, string category, string key, object? value)
        {
            foreach (var e in SettingsRegistry.Entries)
            {
                if (e.ModId == modId && e.Category == category && e.Key == key)
                {
                    e.Set(value);
                    SettingsRegistry.NotifyChanged(e, value);
                    try { MelonPreferences.Save(); } catch { }
                    return;
                }
            }
        }

        public static object? GetValue(string modId, string category, string key)
        {
            foreach (var e in SettingsRegistry.Entries)
                if (e.ModId == modId && e.Category == category && e.Key == key)
                    return e.Get();
            return null;
        }

        public static void OpenPanel(string? modId = null)
        {
            FFUIOverhaul.UI.SettingsWindow.Open(modId);
        }

        // Find which MelonPreferences category contains this entry, and claim it
        // for the given modId. The first claim wins; subsequent calls are no-ops.
        private static void ClaimEntryCategory(string modId, object entry)
        {
            try
            {
                var prefsType = typeof(MelonPreferences);
                var catsObj = prefsType.GetProperty("Categories", BindingFlags.Public | BindingFlags.Static)
                                ?.GetValue(null, null)
                            ?? prefsType.GetField("Categories", BindingFlags.Public | BindingFlags.Static)
                                ?.GetValue(null);
                if (!(catsObj is System.Collections.IEnumerable cats))
                {
                    FFUIOverhaulMod.Log.Warning($"[Settings/claim-fail] modId='{modId}' — could not enumerate MelonPreferences.Categories (got {catsObj?.GetType().Name ?? "null"})");
                    return;
                }

                foreach (var cat in cats)
                {
                    if (cat == null) continue;
                    var catType = cat.GetType();
                    var entriesObj = catType.GetProperty("Entries", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cat, null)
                                   ?? catType.GetField("Entries", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cat);
                    if (!(entriesObj is System.Collections.IList list))
                    {
                        FFUIOverhaulMod.Log.Warning($"[Settings/claim-debug] cat type {catType.Name} — Entries member not IList (got {entriesObj?.GetType().Name ?? "null"})");
                        continue;
                    }

                    foreach (var e in list)
                    {
                        if (ReferenceEquals(e, entry))
                        {
                            string id = catType.GetProperty("Identifier", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cat, null) as string
                                     ?? catType.GetField("Identifier", BindingFlags.Public | BindingFlags.Instance)?.GetValue(cat) as string
                                     ?? "";
                            if (!string.IsNullOrEmpty(id))
                                SettingsRegistry.ClaimCategory(id, modId);
                            else
                                FFUIOverhaulMod.Log.Warning($"[Settings/claim-fail] modId='{modId}' — found entry but category Identifier is empty");
                            return;
                        }
                    }
                }

                FFUIOverhaulMod.Log.Warning($"[Settings/claim-fail] modId='{modId}' — entry of type {entry?.GetType().FullName ?? "null"} not found in any MelonPreferences category");
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"[Settings/claim-fail] modId='{modId}' — exception: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
