using System.Collections.Generic;

namespace FFUIOverhaul.Localization
{
    /// <summary>
    /// Public localization surface for the fleet — same soft-dep shape as
    /// SettingsAPI. Other mods resolve this reflectively:
    ///   Type.GetType("FFUIOverhaul.Localization.KcLocalizationApi, KeepClarity")
    /// and call the static methods below. KC absent → mod uses its own English
    /// strings; KC present → strings follow the game language.
    ///
    /// NOTE: settings-panel strings need NO registration at all — the panel
    /// localizes Label/Tooltip/Category/mod-name implicitly at render time
    /// (keys: {ModId}/opt/{Key}/label etc. — see KcLoc). This API is for a
    /// mod's OTHER user-facing strings (overlay text, toasts, event-log lines).
    ///
    /// ⚠ Method names/signatures are a load-bearing contract once shipped —
    /// same rule as SettingsAPI. Don't rename.
    /// </summary>
    public static class KcLocalizationApi
    {
        public const string Version = "1.0";

        /// <summary>Register a table of terms: termKey → (languageName → text).
        /// Language names must be FF's exact names ("English", "Chinese
        /// (Simplified)", ...). Missing languages fall back to English text.
        /// Namespace your keys with your modId ("EP/...", "WotW/...").</summary>
        public static void RegisterTerms(Dictionary<string, Dictionary<string, string>> terms)
            => KcLoc.AddTerms(terms);

        /// <summary>Localize a registered term in the current game language.
        /// Returns <paramref name="fallback"/> if the term isn't registered or
        /// localization isn't ready yet — always safe to call.</summary>
        public static string Localize(string termKey, string fallback)
            => KcLoc.Tr(termKey, fallback);

        /// <summary>The 15 language names FF supports, exactly as they must
        /// appear in tables and pack files.</summary>
        public static string[] SupportedLanguages()
            => (string[])KcLoc.LanguageNames.Clone();
    }
}
