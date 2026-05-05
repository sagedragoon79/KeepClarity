using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using FFUIOverhaul.Settings;

namespace FFUIOverhaul.UI
{
    /// <summary>
    /// Stage-1 IMGUI prototype of the Keep Clarity settings panel.
    /// Functional but unstyled — verifies the data layer end-to-end before we
    /// invest in a polished UGUI panel that pulls FF's native sprites.
    /// Toggle with the hotkey defined by Plugin.SettingsPanelHotkey (default F10).
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        private static SettingsWindow? _instance;
        private static bool _open;
        private static string? _selectedModId;
        private static string _searchText = "";
        private static bool _showOnlyChanged;

        private Vector2 _modListScroll;
        private Vector2 _detailScroll;
        private Rect _windowRect = new Rect(120, 80, 880, 560);
        private readonly Dictionary<string, string> _textBuffers = new Dictionary<string, string>();
        private string? _rebindingKey;

        private static GUIStyle? _titleStyle;
        private static GUIStyle? _h2Style;
        private static GUIStyle? _modItemStyle;
        private static GUIStyle? _modItemActiveStyle;
        private static GUIStyle? _hintStyle;
        private static GUIStyle? _windowStyle;
        private static Texture2D? _bgPanel;
        private static Texture2D? _bgRow;
        private static Texture2D? _bgRowActive;

        // Panel background opacity (0.0 = fully transparent, 1.0 = fully opaque).
        // The default IMGUI window skin is partly transparent and lets game
        // visuals bleed through, which makes text hard to read. We override
        // with a solid dark background. Tweak here for now; Stage 2 UGUI will
        // expose this as a runtime preference.
        private const float PANEL_ALPHA = 1.0f;

        public static void EnsureInstance()
        {
            if (_instance != null) return;
            var go = new GameObject("KeepClarity_SettingsWindow");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsWindow>();
        }

        public static void Open(string? modId = null)
        {
            EnsureInstance();
            _open = true;
            if (!string.IsNullOrEmpty(modId)) _selectedModId = modId;
        }

        public static void Close() => _open = false;

        public static void Toggle()
        {
            EnsureInstance();
            _open = !_open;
        }

        public static bool IsOpen => _open;

        private void Update()
        {
            if (_open && Input.GetKeyDown(KeyCode.Escape) && _rebindingKey == null)
            {
                Close();
                try { MelonPreferences.Save(); } catch { }
            }
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();

            _windowRect = GUILayout.Window(91733, _windowRect, DrawWindow, "Keep Clarity — Mod Settings", _windowStyle);
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginHorizontal();

            // === Header bar ===
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label("Mods", _titleStyle);
            _searchText = GUILayout.TextField(_searchText ?? "");
            _showOnlyChanged = GUILayout.Toggle(_showOnlyChanged, "Only changed");
            GUILayout.Space(4);

            _modListScroll = GUILayout.BeginScrollView(_modListScroll, GUILayout.ExpandHeight(true));
            foreach (var kv in SettingsRegistry.Mods.OrderBy(m => m.Value.Order).ThenBy(m => m.Value.DisplayName))
            {
                var entries = SettingsRegistry.ForMod(kv.Key).ToList();
                if (entries.Count == 0) continue;
                if (!MatchesFilter(kv.Key, kv.Value, entries)) continue;

                bool active = kv.Key == _selectedModId;
                if (GUILayout.Button($"{kv.Value.DisplayName}\n  {entries.Count} settings",
                    active ? _modItemActiveStyle : _modItemStyle, GUILayout.Height(44)))
                {
                    _selectedModId = kv.Key;
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // === Detail pane ===
            GUILayout.BeginVertical();
            if (_selectedModId == null || !SettingsRegistry.Mods.ContainsKey(_selectedModId))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select a mod from the left to configure.", _hintStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                DrawDetail(_selectedModId);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save & Close", GUILayout.Width(120)))
            {
                try { MelonPreferences.Save(); } catch { }
                Close();
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        private void DrawDetail(string modId)
        {
            var info = SettingsRegistry.Mods[modId];
            GUILayout.Label(info.DisplayName, _titleStyle);
            if (!string.IsNullOrEmpty(info.Description)) GUILayout.Label(info.Description, _hintStyle);
            GUILayout.Space(6);

            _detailScroll = GUILayout.BeginScrollView(_detailScroll);

            foreach (var grp in SettingsRegistry.ByCategory(modId))
            {
                GUILayout.Label(grp.Key, _h2Style);
                foreach (var e in grp.OrderBy(x => x.Meta.Order).ThenBy(x => x.Key))
                {
                    if (_showOnlyChanged && !IsChanged(e)) continue;
                    if (e.Meta.VisibleWhen != null)
                    {
                        bool ok = false;
                        try { ok = e.Meta.VisibleWhen(); } catch { ok = true; }
                        if (!ok) continue;
                    }
                    DrawEntry(e);
                }
                GUILayout.Space(8);
            }

            GUILayout.EndScrollView();
        }

        private void DrawEntry(SettingEntryRecord e)
        {
            GUILayout.BeginHorizontal();
            string label = e.Meta.Label ?? e.VanillaDisplayName ?? e.Key;
            GUI.color = IsChanged(e) ? new Color(1f, 0.95f, 0.7f) : Color.white;
            GUILayout.Label(new GUIContent(label, e.Meta.Tooltip ?? e.VanillaDescription ?? ""), GUILayout.Width(260));
            GUI.color = Color.white;

            object? current = SafeGet(e);
            object? newValue = current;

            try
            {
                if (e.ValueType == typeof(bool))
                {
                    bool b = current is bool bb && bb;
                    bool nb = GUILayout.Toggle(b, b ? " On" : " Off", GUILayout.Width(80));
                    if (nb != b) newValue = nb;
                }
                else if (e.ValueType == typeof(int))
                {
                    int iv = current is int ii ? ii : 0;
                    if (e.Meta.Min is IConvertible && e.Meta.Max is IConvertible)
                    {
                        int min = Convert.ToInt32(e.Meta.Min);
                        int max = Convert.ToInt32(e.Meta.Max);
                        int slid = Mathf.RoundToInt(GUILayout.HorizontalSlider(iv, min, max, GUILayout.Width(280)));
                        GUILayout.Label(slid.ToString(), GUILayout.Width(60));
                        if (slid != iv) newValue = slid;
                    }
                    else
                    {
                        string buf = TextBuffer(e, iv.ToString());
                        string nb2 = GUILayout.TextField(buf, GUILayout.Width(120));
                        SetTextBuffer(e, nb2);
                        if (int.TryParse(nb2, out int parsed) && parsed != iv) newValue = parsed;
                    }
                }
                else if (e.ValueType == typeof(float))
                {
                    float fv = current is float ff ? ff : (current is double dd ? (float)dd : 0f);
                    if (e.Meta.Min is IConvertible && e.Meta.Max is IConvertible)
                    {
                        float min = Convert.ToSingle(e.Meta.Min);
                        float max = Convert.ToSingle(e.Meta.Max);
                        float slid = GUILayout.HorizontalSlider(fv, min, max, GUILayout.Width(280));
                        GUILayout.Label(slid.ToString("0.##"), GUILayout.Width(60));
                        if (Math.Abs(slid - fv) > 0.0001f) newValue = slid;
                    }
                    else
                    {
                        string buf = TextBuffer(e, fv.ToString("0.###"));
                        string nb2 = GUILayout.TextField(buf, GUILayout.Width(120));
                        SetTextBuffer(e, nb2);
                        if (float.TryParse(nb2, out float parsed) && Math.Abs(parsed - fv) > 0.0001f) newValue = parsed;
                    }
                }
                else if (e.ValueType == typeof(string))
                {
                    string sv = current as string ?? "";
                    if (e.Meta.EnumOptions != null && e.Meta.EnumOptions.Length > 0)
                    {
                        int sel = Array.IndexOf(e.Meta.EnumOptions, sv);
                        if (sel < 0) sel = 0;
                        int newSel = DrawDropdown(sel, e.Meta.EnumOptions);
                        if (newSel != sel) newValue = e.Meta.EnumOptions[newSel];
                    }
                    else
                    {
                        string nb2 = GUILayout.TextField(sv, GUILayout.Width(280));
                        if (nb2 != sv) newValue = nb2;
                    }
                }
                else if (e.ValueType == typeof(KeyCode))
                {
                    KeyCode kc = current is KeyCode kk ? kk : KeyCode.None;
                    string keyId = e.ModId + "::" + e.Category + "::" + e.Key;
                    if (_rebindingKey == keyId)
                    {
                        GUILayout.Label("Press a key...", GUILayout.Width(160));
                        foreach (KeyCode kcandidate in Enum.GetValues(typeof(KeyCode)))
                        {
                            if (kcandidate == KeyCode.Escape) continue;
                            if (Input.GetKeyDown(kcandidate))
                            {
                                newValue = kcandidate;
                                _rebindingKey = null;
                                break;
                            }
                        }
                        if (Input.GetKeyDown(KeyCode.Escape)) _rebindingKey = null;
                    }
                    else
                    {
                        if (GUILayout.Button(kc.ToString(), GUILayout.Width(160)))
                            _rebindingKey = keyId;
                    }
                }
                else if (e.ValueType.IsEnum)
                {
                    var names = Enum.GetNames(e.ValueType);
                    string cur = current?.ToString() ?? names[0];
                    int sel = Array.IndexOf(names, cur);
                    if (sel < 0) sel = 0;
                    int newSel = DrawDropdown(sel, names);
                    if (newSel != sel) newValue = Enum.Parse(e.ValueType, names[newSel]);
                }
                else
                {
                    GUILayout.Label(current?.ToString() ?? "null", GUILayout.Width(280));
                }
            }
            catch (Exception ex)
            {
                GUILayout.Label("err: " + ex.Message, GUILayout.Width(280));
            }

            GUILayout.FlexibleSpace();
            if (IsChanged(e))
            {
                if (GUILayout.Button("↺", GUILayout.Width(28)))
                {
                    newValue = e.DefaultValue;
                }
            }
            GUILayout.EndHorizontal();

            if (e.Meta.RestartRequired && IsChanged(e))
            {
                GUILayout.Label("  ⚠ requires restart to take effect", _hintStyle);
            }

            if (!Equals(newValue, current))
            {
                e.Set(newValue);
                SettingsRegistry.NotifyChanged(e, newValue);
                ClearTextBuffer(e);
            }
        }

        private static int DrawDropdown(int selected, string[] options)
        {
            // GUILayout has no built-in dropdown; cycle button is good enough for stage-1.
            string label = options[Mathf.Clamp(selected, 0, options.Length - 1)];
            if (GUILayout.Button("◄", GUILayout.Width(24))) selected = (selected - 1 + options.Length) % options.Length;
            GUILayout.Label(label, GUILayout.Width(180));
            if (GUILayout.Button("►", GUILayout.Width(24))) selected = (selected + 1) % options.Length;
            return selected;
        }

        private static bool IsChanged(SettingEntryRecord e)
        {
            try { return !Equals(e.Get(), e.DefaultValue); } catch { return false; }
        }

        private static object? SafeGet(SettingEntryRecord e)
        {
            try { return e.Get(); } catch { return null; }
        }

        private string TextBuffer(SettingEntryRecord e, string fallback)
        {
            string k = e.ModId + "::" + e.Category + "::" + e.Key;
            return _textBuffers.TryGetValue(k, out var v) ? v : fallback;
        }

        private void SetTextBuffer(SettingEntryRecord e, string v)
        {
            string k = e.ModId + "::" + e.Category + "::" + e.Key;
            _textBuffers[k] = v;
        }

        private void ClearTextBuffer(SettingEntryRecord e)
        {
            string k = e.ModId + "::" + e.Category + "::" + e.Key;
            _textBuffers.Remove(k);
        }

        private static bool MatchesFilter(string modId, ModSettingsInfo info, List<SettingEntryRecord> entries)
        {
            if (!string.IsNullOrEmpty(_searchText))
            {
                string s = _searchText.ToLowerInvariant();
                if (info.DisplayName.ToLowerInvariant().Contains(s)) return true;
                foreach (var e in entries)
                {
                    if (e.Key.ToLowerInvariant().Contains(s)) return true;
                    if (!string.IsNullOrEmpty(e.Meta.Label) && e.Meta.Label!.ToLowerInvariant().Contains(s)) return true;
                    if (!string.IsNullOrEmpty(e.VanillaDisplayName) && e.VanillaDisplayName!.ToLowerInvariant().Contains(s)) return true;
                }
                return false;
            }
            if (_showOnlyChanged)
            {
                foreach (var e in entries) if (IsChanged(e)) return true;
                return false;
            }
            return true;
        }

        private static void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _bgPanel = MakeTex(new Color(0.09f, 0.08f, 0.07f, PANEL_ALPHA));
            _bgRow = MakeTex(new Color(0.15f, 0.13f, 0.11f, 1f));
            _bgRowActive = MakeTex(new Color(0.36f, 0.27f, 0.13f, 1f));

            _titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _h2Style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            _hintStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, wordWrap = true };
            _modItemStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };
            _modItemActiveStyle = new GUIStyle(_modItemStyle) { fontStyle = FontStyle.Bold };
            _modItemActiveStyle.normal.background = _bgRowActive;

            // Solid window background — replaces the default Unity IMGUI skin
            // which lets the game scene bleed through and makes text unreadable.
            _windowStyle = new GUIStyle(GUI.skin.window)
            {
                padding = new RectOffset(12, 12, 28, 12),
            };
            _windowStyle.normal.background = _bgPanel;
            _windowStyle.onNormal.background = _bgPanel;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.onNormal.textColor = Color.white;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
