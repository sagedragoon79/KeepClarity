# Keep Clarity Settings — Integration Guide for Other Mods

## What you get for free

If your mod uses `MelonPreferences`, **it already shows up in Keep Clarity's panel**
with no code changes — auto-discovery enumerates every `MelonPreferences_Category`
on first map load and renders each entry with its existing `display_name` and
`description`.

Default controls:
- `bool` → toggle
- `int` / `float` → text field
- `string` → text field
- `KeyCode` → rebind button
- enum → dropdown

## What rich registration adds

By calling `SettingsAPI.Register(...)` you can add:
- Prettier `Label` (overrides `display_name`)
- Min/Max ranges (turns numeric fields into sliders)
- `EnumOptions` for string fields (turns them into dropdowns)
- `Group` sub-categories within a mod
- `RestartRequired` flag (shows amber warning when changed)
- `VisibleWhen` predicate (e.g. hide a sub-setting when its parent toggle is off)
- Mod-level `AccentColor`, icon, description, version

## Hard-dependency version

If you're shipping a mod pack and want the cleanest code:

1. Add `<Reference Include="KeepClarity">` to your csproj pointing at
   `Mods\KeepClarity.dll`
2. `using FFUIOverhaul.Settings;`
3. Call from `OnInitializeMelon` after creating prefs:

```csharp
SettingsAPI.RegisterMod("MyMod", "My Mod", description: "...",
    accentRgb: new[] { 0.5f, 0.3f, 0.1f, 1f });

SettingsAPI.Register("MyMod", "My Mod", "Combat",
    MyPlugin.AttackSpeed,
    new SettingsMeta { Label = "Attack speed", Min = 0.5f, Max = 3.0f });
```

## Optional-dependency version (recommended)

Drop a `KeepClarityIntegration.cs` file into your mod that resolves the API
reflectively. Mod still works if Keep Clarity is absent.

See `Stalk and Smoke/KeepClarityIntegration.cs` for the canonical template.
The pattern:

```csharp
internal static class KeepClarityIntegration
{
    private static MethodInfo? _registerMod;
    private static MethodInfo? _registerEntry;
    private static Type? _settingsMetaType;
    private static bool _resolved, _present;

    public static void TryRegisterAll()
    {
        if (!ResolveApi()) return;
        // ... call RegisterMod + Register for each pref
    }

    private static bool ResolveApi()
    {
        if (_resolved) return _present;
        _resolved = true;
        var apiType = Type.GetType("FFUIOverhaul.Settings.SettingsAPI, KeepClarity");
        if (apiType == null) return false;
        _settingsMetaType = Type.GetType("FFUIOverhaul.Settings.SettingsMeta, KeepClarity");
        _registerMod = apiType.GetMethod("RegisterMod");
        foreach (var m in apiType.GetMethods())
            if (m.Name == "Register" && m.IsGenericMethodDefinition) _registerEntry = m;
        _present = _settingsMetaType != null && _registerMod != null && _registerEntry != null;
        return _present;
    }
}
```

Call `KeepClarityIntegration.TryRegisterAll()` at the end of `OnInitializeMelon`
after your `MelonPreferences` entries have been created.

## Mod-load ordering

MelonLoader loads DLLs roughly alphabetically. `KeepClarity.dll` loads before
most third-party mods (K is early), so by the time your mod's `OnInitializeMelon`
runs, the `SettingsAPI` type is already loaded and reflectively resolvable.

If your mod somehow loads before Keep Clarity, the reflection lookup returns
null and `TryRegisterAll` becomes a no-op. Auto-discovery still picks up your
prefs at first map load, so worst case you get the auto-discovered version.

## Testing your integration

1. Build your mod, copy DLL to `<game>\Mods\`
2. Launch FF, load any save (auto-discovery runs on map load)
3. Press F10 (default Settings Panel hotkey) — your mod should appear in the
   left rail
4. Check the MelonLoader log for `=== Settings Discovery Dump ===` — every
   registered entry is printed with its mod, category, type, value, and whether
   it was registered explicitly (`[registered]`) or auto-discovered (`[auto]`)

## API reference (stage 1)

```csharp
namespace FFUIOverhaul.Settings
{
    public static class SettingsAPI
    {
        public static void RegisterMod(string modId, string displayName,
            string? description = null, string? version = null,
            string? iconResourcePath = null, float[]? accentRgb = null,
            int order = 0);

        public static void Register<T>(string modId, string modDisplayName,
            string category, MelonPreferences_Entry<T> entry,
            SettingsMeta? meta = null);

        public static void RegisterCategory(string modId, string modDisplayName,
            MelonPreferences_Category category,
            Action<string, SettingsMeta>? configure = null);

        public static void SetValue(string modId, string category, string key, object? value);
        public static object? GetValue(string modId, string category, string key);

        public static event Action<string, string, object?> OnSettingChanged;

        public static void OpenPanel(string? modId = null);
    }

    public class SettingsMeta
    {
        public string? Label;
        public string? Tooltip;
        public object? Min, Max, Step;
        public string[]? EnumOptions;
        public bool RestartRequired;
        public string? Group;
        public int Order;
        public Func<bool>? VisibleWhen;
    }
}
```
