using System;
using System.Collections;
using System.Reflection;
using MelonLoader;

namespace FFUIOverhaul.Settings
{
    public static class SettingsDiscovery
    {
        public static int RunDiscovery()
        {
            int discovered = 0;

            IList? categories = TryGetCategories();
            if (categories == null)
            {
                FFUIOverhaulMod.Log.Warning("[Settings] Could not enumerate MelonPreferences.Categories");
                return 0;
            }

            foreach (var catObj in categories)
            {
                if (catObj == null) continue;
                var catType = catObj.GetType();
                string catId = (string?)GetMember(catObj, catType, "Identifier") ?? "";
                string catDisplay = (string?)GetMember(catObj, catType, "DisplayName") ?? catId;

                IList? entries = GetMember(catObj, catType, "Entries") as IList;
                if (entries == null) continue;

                // For claimed categories: still process, but only auto-add
                // entries that AREN'T already registered. This catches new
                // prefs added to a mod after its KeepClarityIntegration.cs was
                // last updated — they appear under "Other settings" in the
                // owning mod's section instead of being hidden entirely.
                bool claimed = SettingsRegistry.IsCategoryClaimed(catId, out var owningMod);
                if (IsVerbose())
                    FFUIOverhaulMod.Log.Msg($"[Settings/discover] category='{catId}' display='{catDisplay}' claimed={claimed}{(claimed ? $" by '{owningMod}'" : "")}");

                string modId;
                string modDisplay;
                string sectionName;
                if (claimed)
                {
                    modId = owningMod ?? catId;
                    if (SettingsRegistry.Mods.TryGetValue(modId, out var info))
                        modDisplay = info.DisplayName ?? catDisplay;
                    else
                        modDisplay = catDisplay;
                    // Group new/unregistered prefs separately so they're
                    // visually distinct from the curated rich registrations.
                    sectionName = "Other settings";
                }
                else
                {
                    modId = InferModIdFromCategory(catId);
                    modDisplay = catDisplay;
                    sectionName = catDisplay;
                }

                foreach (var entryObj in entries)
                {
                    if (entryObj == null) continue;
                    // Pull the entry's identifier so we can dedupe against the
                    // existing registry (claimed-category case).
                    var entryType = entryObj.GetType();
                    string entryKey = (string?)GetMember(entryObj, entryType, "Identifier") ?? "";
                    if (string.IsNullOrEmpty(entryKey)) continue;

                    // Skip entries already registered under this modId. Allows
                    // claimed-category mods to coexist with their auto-discovered
                    // unregistered extras.
                    if (SettingsRegistry.HasEntryByModAndKey(modId, entryKey)) continue;

                    if (TryAddEntry(entryObj, modId, modDisplay, sectionName))
                        discovered++;
                }
            }

            return discovered;
        }

        private static bool TryAddEntry(object entryObj, string modId, string modDisplay, string categoryDisplay)
        {
            try
            {
                var t = entryObj.GetType();
                string key = (string?)GetMember(entryObj, t, "Identifier") ?? "";
                if (string.IsNullOrEmpty(key)) return false;

                bool isHidden = false;
                var hiddenObj = GetMember(entryObj, t, "IsHidden");
                if (hiddenObj is bool hb) isHidden = hb;
                if (isHidden) return false;

                string? display = GetMember(entryObj, t, "DisplayName") as string;
                string? desc = GetMember(entryObj, t, "Description") as string;

                Type valueType = typeof(object);
                var getReflected = t.GetMethod("GetReflectedType", BindingFlags.Public | BindingFlags.Instance);
                if (getReflected != null)
                {
                    if (getReflected.Invoke(entryObj, null) is Type rt) valueType = rt;
                }

                var captured = entryObj;
                var boxedProp = t.GetProperty("BoxedValue", BindingFlags.Public | BindingFlags.Instance);
                // MelonPreferences_Entry<T> exposes the typed `DefaultValue`
                // property; there is no `BoxedDefaultValue`. Reflection on the
                // typed property returns a boxed object, which is what we want.
                var defaultProp = t.GetProperty("DefaultValue", BindingFlags.Public | BindingFlags.Instance);

                Func<object?> getter = () => boxedProp?.GetValue(captured, null);
                Action<object?> setter = v =>
                {
                    if (boxedProp != null && boxedProp.CanWrite)
                        boxedProp.SetValue(captured, v, null);
                };
                object? defaultVal = defaultProp?.GetValue(captured, null);

                var rec = new SettingEntryRecord
                {
                    ModId = modId,
                    ModDisplayName = modDisplay,
                    Category = categoryDisplay,
                    Key = key,
                    ValueType = valueType,
                    Get = getter,
                    Set = setter,
                    DefaultValue = defaultVal,
                    VanillaDisplayName = display,
                    VanillaDescription = desc,
                    AutoDiscovered = true,
                };

                SettingsRegistry.Add(rec);
                return true;
            }
            catch (Exception ex)
            {
                FFUIOverhaulMod.Log.Warning($"[Settings] Failed to discover entry: {ex.Message}");
                return false;
            }
        }

        private static IList? TryGetCategories()
        {
            var prefsType = typeof(MelonPreferences);
            var prop = prefsType.GetProperty("Categories", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) return prop.GetValue(null, null) as IList;
            var field = prefsType.GetField("Categories", BindingFlags.Public | BindingFlags.Static);
            if (field != null) return field.GetValue(null) as IList;
            return null;
        }

        private static object? GetMember(object obj, Type t, string name)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null) return p.GetValue(obj, null);
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) return f.GetValue(obj);
            return null;
        }

        private static string InferModIdFromCategory(string categoryIdentifier)
        {
            return string.IsNullOrEmpty(categoryIdentifier) ? "Unknown" : categoryIdentifier;
        }

        private static bool IsVerbose()
        {
            try { return FFUIOverhaulMod.SettingsVerboseLog?.Value ?? false; } catch { return false; }
        }

        public static void DumpToLog()
        {
            if (!IsVerbose()) return;
            FFUIOverhaulMod.Log.Msg("=== Settings Discovery Dump ===");
            foreach (var kv in SettingsRegistry.Mods)
            {
                FFUIOverhaulMod.Log.Msg($"[Mod] {kv.Key}  ({kv.Value.DisplayName})");
                foreach (var grp in SettingsRegistry.ByCategory(kv.Key))
                {
                    FFUIOverhaulMod.Log.Msg($"  [Category] {grp.Key}");
                    foreach (var e in grp)
                    {
                        var val = SafeGet(e);
                        FFUIOverhaulMod.Log.Msg($"    - {e.Key} : {e.ValueType.Name} = {val}  (default={e.DefaultValue}) {(e.AutoDiscovered ? "[auto]" : "[registered]")}");
                    }
                }
            }
            FFUIOverhaulMod.Log.Msg($"=== {SettingsRegistry.Entries.Count} entries across {SettingsRegistry.Mods.Count} mods ===");
        }

        private static string SafeGet(SettingEntryRecord r)
        {
            try { return r.Get()?.ToString() ?? "null"; }
            catch { return "<get-failed>"; }
        }
    }
}
