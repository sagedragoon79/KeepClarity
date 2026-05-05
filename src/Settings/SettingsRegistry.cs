using System;
using System.Collections.Generic;
using System.Linq;

namespace FFUIOverhaul.Settings
{
    public static class SettingsRegistry
    {
        private static readonly Dictionary<string, ModSettingsInfo> _mods = new Dictionary<string, ModSettingsInfo>();
        private static readonly List<SettingEntryRecord> _entries = new List<SettingEntryRecord>();
        private static readonly Dictionary<string, SettingEntryRecord> _byKey = new Dictionary<string, SettingEntryRecord>();

        // Session-start snapshot: every entry's value as it was when the user
        // first opened the panel. The Reset button restores from this map
        // (revert), not from rec.DefaultValue (factory). Captured lazily on
        // first read so all mods have had time to register.
        private static readonly Dictionary<string, object?> _sessionStartValues = new Dictionary<string, object?>();
        private static bool _sessionStartCaptured;

        public static void CaptureSessionStartValues()
        {
            if (_sessionStartCaptured) return;
            _sessionStartCaptured = true;
            foreach (var rec in _entries)
            {
                try { _sessionStartValues[rec.ModId + "::" + rec.Key] = rec.Get(); } catch { }
            }
        }

        public static bool TryGetSessionStartValue(string modId, string key, out object? value)
        {
            return _sessionStartValues.TryGetValue(modId + "::" + key, out value);
        }

        /// <summary>True if any registered entry already exists for this
        /// modId + key pair (regardless of category). Used by discovery to
        /// avoid duplicating already-registered entries while still surfacing
        /// newly-added prefs that aren't in the mod's KeepClarityIntegration.</summary>
        public static bool HasEntryByModAndKey(string modId, string key)
        {
            foreach (var rec in _entries)
            {
                if (rec.ModId == modId && rec.Key == key) return true;
            }
            return false;
        }

        // MelonPreferences category identifier → owning modId. When a category
        // is claimed by an explicit Register call, auto-discovery skips it
        // (otherwise the panel shows the same prefs twice — once under the
        // category name, once under the registered mod's display name).
        private static readonly Dictionary<string, string> _categoryOwners = new Dictionary<string, string>();

        public static void ClaimCategory(string categoryIdentifier, string modId)
        {
            if (string.IsNullOrEmpty(categoryIdentifier) || string.IsNullOrEmpty(modId)) return;
            bool firstClaim = !_categoryOwners.ContainsKey(categoryIdentifier);
            _categoryOwners[categoryIdentifier] = modId;
            // Only log on first claim — subsequent calls (one per registered
            // entry) would flood the log without adding signal.
            if (firstClaim && IsVerbose())
            {
                try { FFUIOverhaulMod.Log.Msg($"[Settings/claim] '{categoryIdentifier}' → modId='{modId}'"); } catch { }
            }
        }

        private static bool IsVerbose()
        {
            try { return FFUIOverhaulMod.SettingsVerboseLog?.Value ?? false; } catch { return false; }
        }

        public static bool IsCategoryClaimed(string categoryIdentifier, out string? owningModId)
        {
            if (_categoryOwners.TryGetValue(categoryIdentifier, out var id))
            {
                owningModId = id;
                return true;
            }
            owningModId = null;
            return false;
        }

        public static event Action<string /*modId*/, string /*key*/, object?>? SettingChanged;

        public static IReadOnlyDictionary<string, ModSettingsInfo> Mods => _mods;
        public static IReadOnlyList<SettingEntryRecord> Entries => _entries;

        public static void RegisterMod(string modId, ModSettingsInfo info)
        {
            if (string.IsNullOrEmpty(modId)) return;
            _mods[modId] = info;
        }

        public static SettingEntryRecord Add(SettingEntryRecord rec)
        {
            var k = MakeKey(rec.ModId, rec.Category, rec.Key);
            if (_byKey.TryGetValue(k, out var existing))
            {
                if (existing.AutoDiscovered && !rec.AutoDiscovered)
                {
                    var idx = _entries.IndexOf(existing);
                    if (idx >= 0) _entries[idx] = rec;
                    _byKey[k] = rec;
                    return rec;
                }
                return existing;
            }

            _entries.Add(rec);
            _byKey[k] = rec;

            if (!_mods.ContainsKey(rec.ModId))
            {
                _mods[rec.ModId] = new ModSettingsInfo { DisplayName = rec.ModDisplayName };
            }

            return rec;
        }

        public static IEnumerable<SettingEntryRecord> ForMod(string modId)
            => _entries.Where(e => e.ModId == modId);

        public static IEnumerable<IGrouping<string, SettingEntryRecord>> ByCategory(string modId)
            => ForMod(modId).GroupBy(e => e.Category);

        public static void NotifyChanged(SettingEntryRecord rec, object? newValue)
        {
            try { SettingChanged?.Invoke(rec.ModId, rec.Key, newValue); }
            catch (Exception ex) { FFUIOverhaulMod.Log.Warning($"[Settings] SettingChanged handler threw: {ex.Message}"); }
        }

        private static string MakeKey(string modId, string category, string key)
            => modId + "::" + category + "::" + key;
    }
}
