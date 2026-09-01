using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace FFUIOverhaul.Localization
{
    /// <summary>
    /// Keep Clarity's native localization layer — the fleet-wide answer to
    /// community translation mods (a modder shipped a Chinese overlay pack for
    /// the fleet; this makes translations first-class instead).
    ///
    /// HOW IT WORKS
    /// FF uses I2 Localization and ships 15 languages (fonts already render
    /// CJK). We keep our own term table (term → language → text) and resolve at
    /// render time against FF's CURRENT language
    /// (I2.Loc.LocalizationManager.CurrentLanguage — synced with the game's
    /// language setting). A term without an entry for the current language
    /// falls back to the English string the CALLER passes — i.e. exactly the
    /// hardcoded label that would have rendered anyway. Strings auto-follow the
    /// player's game language; no toggle of ours, nothing ever renders blank.
    ///
    /// (v1 note: an earlier design registered terms INTO I2's sources via the
    /// FFModImport pattern. Dropped: write-side language filling forced a
    /// fallback choice at registration time — a Chinese-only pack bled Chinese
    /// into every language. Render-time resolution has exact semantics.)
    ///
    /// WHERE TRANSLATIONS COME FROM
    /// Drop-in packs: plain UTF-8 text files in Mods/KCLocalization/*.txt —
    ///   an early line:          "language: Chinese (Simplified)"
    ///   then one term per line: "KeepClarity/opt/EnableBlightWatch/label=..."
    ///   '#' starts a comment. Translators ship a text file, no DLL, no code.
    /// Fleet mods can also register tables directly via KcLocalizationApi
    /// (reflective soft-dep, same shape as SettingsAPI).
    ///
    /// KEYS FOR SETTINGS STRINGS ARE IMPLICIT — the settings panel resolves
    /// them at render time (see Tr calls in ModDetailPanel/ModListPanel):
    ///   {ModId}/meta/name , {ModId}/meta/desc
    ///   {ModId}/cat/{Category}
    ///   {ModId}/opt/{PrefKey}/label , {ModId}/opt/{PrefKey}/tip
    /// So EVERY fleet mod's settings UI is translatable with zero code changes
    /// in that mod. Ctrl+Alt+F10 exports a template of every registered string.
    /// </summary>
    internal static class KcLoc
    {
        public const string PackDirName = "KCLocalization";

        // term → (languageName → text). The single source of truth.
        private static readonly Dictionary<string, Dictionary<string, string>> _table
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static bool _packsLoaded;

        // Current-language cache — refreshed at most once per second so Tr()
        // stays dirt cheap even when a panel rebuild calls it hundreds of times.
        private static string _lang = "English";
        private static float _nextLangCheck;
        private static bool _loggedLang;

        /// <summary>The 15 language names FF supports (decompile L334354-334413).
        /// Pack files must use these exact names.</summary>
        public static readonly string[] LanguageNames =
        {
            "English", "German", "French", "Italian", "Korean", "Spanish",
            "Chinese (Simplified)", "Chinese (Traditional)", "Russian",
            "Polish", "Swedish", "Czech", "Portuguese", "Japanese", "Ukrainian",
        };

        // ── public surface (used by the settings UI + KcLocalizationApi) ────

        /// <summary>Localize at render time: the pack/table text for the current
        /// game language if present, else <paramref name="fallback"/> (the
        /// caller's English string) unchanged. Always safe to call.</summary>
        public static string Tr(string termKey, string fallback)
        {
            if (string.IsNullOrEmpty(termKey) || _table.Count == 0) return fallback;
            if (!_table.TryGetValue(termKey, out var langs)) return fallback;
            if (langs.TryGetValue(_lang, out var t) && !string.IsNullOrEmpty(t)) return t;
            return fallback;
        }

        /// <summary>Merge terms into the table (term → language → text).</summary>
        public static void AddTerms(Dictionary<string, Dictionary<string, string>> terms)
        {
            if (terms == null) return;
            foreach (var kv in terms)
            {
                if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                if (!_table.TryGetValue(kv.Key, out var langs))
                    _table[kv.Key] = langs = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var lv in kv.Value)
                    if (!string.IsNullOrEmpty(lv.Value)) langs[lv.Key] = lv.Value;
            }
        }

        /// <summary>Driven from Plugin.OnUpdate: loads packs once, keeps the
        /// current-language cache fresh (menu included).</summary>
        public static void Tick()
        {
            if (!_packsLoaded)
            {
                _packsLoaded = true;
                try { LoadPacks(); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[Loc] pack load failed: " + e.Message); }
            }

            if (Time.unscaledTime < _nextLangCheck) return;
            _nextLangCheck = Time.unscaledTime + 1f;
            try
            {
                var lang = ResolveGameLanguage();
                if (!string.IsNullOrEmpty(lang) && lang != _lang)
                {
                    _lang = lang;
                    FFUIOverhaulMod.Log.Msg($"[Loc] game language: {_lang}" +
                        (_table.Count > 0 ? $" ({_table.Count} translated term(s) available)" : ""));
                    // Re-render the settings window (list + detail) so open panels
                    // pick up the new language without needing a reopen.
                    Settings.UI.SettingsCanvas.OnLanguageChanged();
                }
                else if (!_loggedLang && !string.IsNullOrEmpty(lang))
                {
                    _loggedLang = true;
                    FFUIOverhaulMod.Log.Msg($"[Loc] ready — language '{_lang}', {_table.Count} term(s) loaded.");
                }
            }
            catch { /* I2 not up yet — keep default English */ }
        }

        /// <summary>The language the GAME is actually displaying.
        ///
        /// Source of truth is FF's own <c>SettingsManager.currentLanguage</c>, NOT
        /// I2's CurrentLanguage. They diverge: FF syncs I2 at startup with
        /// <c>I2.LocalizationManager.CurrentLanguage = PlayerPrefs.GetString("currentLanguage")</c>,
        /// but PlayerPrefs returns "" when the player has never changed language,
        /// and I2's setter ignores empty input — so I2 keeps whatever
        /// SelectStartupLanguage() chose, which is the DEVICE locale. FF meanwhile
        /// falls back to its own default of "English". Result on a non-English
        /// Windows: English game, mod panel in the system language (reported by a
        /// player 2026-08-31, Chinese). Reading FF's setting first makes KC follow
        /// what the player actually sees. I2 stays as the fallback.</summary>
        private static string ResolveGameLanguage()
        {
            // Explicit player override wins over everything — the escape hatch if
            // detection is ever wrong on a machine we can't reproduce.
            try
            {
                var ov = FFUIOverhaulMod.LanguageOverride?.Value;
                if (!string.IsNullOrEmpty(ov) && ov != "Auto"
                    && Array.IndexOf(LanguageNames, ov) >= 0)
                    return ov;
            }
            catch { }

            try
            {
                var sm = UnitySingletonPersistent<SettingsManager>.Instance;
                if (sm != null)
                {
                    var ff = sm.currentLanguage;
                    if (!string.IsNullOrEmpty(ff) && Array.IndexOf(LanguageNames, ff) >= 0)
                        return ff;
                }
            }
            catch { /* SettingsManager not up yet — fall through */ }

            try
            {
                var i2 = I2.Loc.LocalizationManager.CurrentLanguage;
                if (!string.IsNullOrEmpty(i2)) return i2;
            }
            catch { }
            return "English";
        }

        // ── packs ───────────────────────────────────────────────────────────
        // Two tiers, loaded in override order:
        //   1. EMBEDDED packs (compiled into the DLL from src/Localization/packs/)
        //      — ship with the mod, so every player gets all 15 languages.
        //   2. DISK packs (Mods/KCLocalization/*.txt) — drop-in community
        //      corrections/additions; loaded second so they OVERRIDE built-ins.

        private static string PackDir() => Path.Combine("Mods", PackDirName);

        private static void LoadPacks()
        {
            int files = 0;

            // Tier 1 — embedded (built-in translations).
            try
            {
                var asm = typeof(KcLoc).Assembly;
                foreach (var res in asm.GetManifestResourceNames())
                {
                    if (!res.StartsWith("FFUIOverhaul.pack.", StringComparison.Ordinal)) continue;
                    try
                    {
                        using (var stream = asm.GetManifestResourceStream(res))
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            var lines = new List<string>();
                            string l;
                            while ((l = reader.ReadLine()) != null) lines.Add(l);
                            if (ParsePackLines(lines, "builtin:" + res.Substring("FFUIOverhaul.pack.".Length)))
                                files++;
                        }
                    }
                    catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[Loc] embedded pack '{res}' failed: {e.Message}"); }
                }
            }
            catch (Exception e) { FFUIOverhaulMod.Log.Warning("[Loc] embedded pack scan failed: " + e.Message); }

            // Tier 2 — disk (community overrides win over built-ins).
            var dir = PackDir();
            if (Directory.Exists(dir))
            {
                foreach (var path in Directory.GetFiles(dir, "*.txt"))
                {
                    var name = Path.GetFileName(path);
                    if (name.StartsWith("template_", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        if (ParsePackLines(File.ReadAllLines(path, Encoding.UTF8), name)) files++;
                    }
                    catch (Exception e) { FFUIOverhaulMod.Log.Warning($"[Loc] pack '{name}' failed: {e.Message}"); }
                }
            }

            if (files > 0) FFUIOverhaulMod.Log.Msg($"[Loc] loaded {files} translation pack(s), {_table.Count} term(s) total.");
        }

        /// <summary>Parse one pack's lines and merge into the table. Later calls
        /// override earlier ones for the same term+language (that's the disk-
        /// beats-embedded contract). Returns true if any terms were added.</summary>
        private static bool ParsePackLines(IList<string> rawLines, string displayName)
        {
            string language = null;
            var table = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var raw in rawLines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (language == null && line.StartsWith("language:", StringComparison.OrdinalIgnoreCase))
                {
                    language = line.Substring("language:".Length).Trim();
                    if (Array.IndexOf(LanguageNames, language) < 0)
                    {
                        FFUIOverhaulMod.Log.Warning($"[Loc] pack '{displayName}': unknown language '{language}' — must be one of FF's 15 (e.g. \"Chinese (Simplified)\"). Skipping.");
                        return false;
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0 || string.IsNullOrEmpty(language)) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1);
                if (key.Length == 0 || val.Length == 0) continue;
                if (!table.TryGetValue(key, out var langs))
                    table[key] = langs = new Dictionary<string, string>(StringComparer.Ordinal);
                langs[language] = val;
            }
            if (table.Count == 0) return false;
            AddTerms(table);
            FFUIOverhaulMod.Log.Msg($"[Loc] pack '{displayName}' ({language}): {table.Count} term(s).");
            return true;
        }

        // ── translator kit: export every registered settings string ─────────

        /// <summary>Writes Mods/KCLocalization/template_english.txt containing
        /// every string the settings panel can localize (all mods), in the pack
        /// format. A translator copies it, sets the language: header, and
        /// translates the values. Triggered by Ctrl+Alt+F10. (Files named
        /// template_* are ignored by the pack loader.)</summary>
        public static void ExportTemplate()
        {
            try
            {
                var dir = PackDir();
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "template_english.txt");
                var sb = new StringBuilder();
                sb.AppendLine("# Keep Clarity localization template — every settings string in the fleet.");
                sb.AppendLine("# To translate: copy this file (e.g. chinese_simplified.txt), change the");
                sb.AppendLine("# language: header to one of FF's names, translate the text AFTER each '='.");
                sb.AppendLine("# Delete lines you don't translate (they fall back to English).");
                sb.AppendLine("# NOTE: files named template_* are ignored — your copy must be renamed.");
                sb.AppendLine("# Languages: " + string.Join(", ", LanguageNames));
                sb.AppendLine("language: English");
                sb.AppendLine();

                foreach (var mod in Settings.SettingsRegistry.Mods)
                {
                    var modId = mod.Key;
                    var info = mod.Value;
                    sb.AppendLine("# ── " + (info.DisplayName ?? modId) + " ──");
                    if (!string.IsNullOrEmpty(info.DisplayName)) sb.AppendLine($"{modId}/meta/name={info.DisplayName}");
                    if (!string.IsNullOrEmpty(info.Description)) sb.AppendLine($"{modId}/meta/desc={info.Description}");

                    var seenCats = new HashSet<string>();
                    foreach (var e in Settings.SettingsRegistry.ForMod(modId))
                    {
                        if (!string.IsNullOrEmpty(e.Category) && seenCats.Add(e.Category))
                            sb.AppendLine($"{modId}/cat/{e.Category}={e.Category}");
                        string label = e.Meta.Label ?? e.VanillaDisplayName ?? e.Key;
                        sb.AppendLine($"{modId}/opt/{e.Key}/label={label}");
                        string tip = !string.IsNullOrEmpty(e.Meta.Tooltip) ? e.Meta.Tooltip : e.VanillaDescription;
                        if (!string.IsNullOrEmpty(tip))
                            sb.AppendLine($"{modId}/opt/{e.Key}/tip={tip}");
                    }
                    sb.AppendLine();
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                FFUIOverhaulMod.Log.Msg("[Loc] template exported → " + path);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning("[Loc] template export failed: " + e.Message);
            }
        }
    }
}
