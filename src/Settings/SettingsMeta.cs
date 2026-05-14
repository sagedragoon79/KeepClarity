using System;
using UnityEngine;

namespace FFUIOverhaul.Settings
{
    public class SettingsMeta
    {
        public string? Label;
        public string? Tooltip;
        public object? Min;
        public object? Max;
        public object? Step;
        public string[]? EnumOptions;
        public bool RestartRequired;
        public string? Group;
        public int Order;
        public Func<bool>? VisibleWhen;

        /// <summary>
        /// Left-padding in pixels applied to the row. Use to visually nest
        /// sub-prefs under their master toggle. Recommended values: 0 (no
        /// indent — top-level masters), 20 (single nest), 40 (double nest).
        /// </summary>
        public int Indent;
    }

    public class ModSettingsInfo
    {
        public string DisplayName = "";
        public string? Description;
        public string? Version;
        public string? IconResourcePath;
        public Color? AccentColor;
        public int Order;
    }

    public enum SettingControlKind
    {
        Auto,
        Toggle,
        Slider,
        NumberField,
        TextField,
        Dropdown,
        KeyBind,
        JsonEditor
    }

    public class SettingEntryRecord
    {
        public string ModId = "";
        public string ModDisplayName = "";
        public string Category = "";
        public string Key = "";
        public Type ValueType = typeof(object);
        public Func<object?> Get = () => null;
        public Action<object?> Set = _ => { };
        public object? DefaultValue;
        public string? VanillaDisplayName;
        public string? VanillaDescription;
        public SettingsMeta Meta = new SettingsMeta();
        public SettingControlKind ResolvedControl = SettingControlKind.Auto;
        public bool AutoDiscovered;
    }
}
