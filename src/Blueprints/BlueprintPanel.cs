using System;
using UnityEngine;

namespace FFUIOverhaul.Blueprints
{
    /// <summary>
    /// The blueprint library: name and save the current capture, and pick a saved
    /// blueprint to stamp.
    ///
    /// IMGUI rather than uGUI on purpose, for now. A uGUI panel styled like FF's
    /// own windows is the right end state (Divine Hands has that pattern to port),
    /// but this needs a working library first — and IMGUI gets a usable, keyboard-
    /// friendly text field and list in a fraction of the code. Swapping the
    /// presentation later doesn't disturb BlueprintStore or the capture module.
    /// </summary>
    internal static class BlueprintPanel
    {
        // ── uGUI front end ──────────────────────────────────────────────────
        // The panel is uGUI now, styled from FFNativeAssets so it matches the mod
        // manager. The IMGUI implementation below is kept as a runtime fallback:
        // if the canvas fails to build on some machine, the library degrades to a
        // plain window instead of vanishing. _useUgui latches false on the first
        // failure so we don't retry a broken build every frame.
        private static BlueprintPanelUgui? _ugui;
        private static bool _useUgui = true;

        private static BlueprintPanelUgui? Ugui
        {
            get
            {
                if (!_useUgui) return null;
                return _ugui ??= new BlueprintPanelUgui();
            }
        }

        /// <summary>Per-frame upkeep for the uGUI panel. Driven from Plugin.</summary>
        public static void Tick()
        {
            if (!_useUgui || _ugui == null) return;
            try { _ugui.Tick(); }
            catch (Exception e) { Fallback("tick", e); }
        }

        public static void OnMapLoaded()
        {
            // Canvas objects don't survive a map change cleanly; rebuild lazily.
            try { _ugui?.Destroy(); } catch { }
            _ugui = null;
        }

        private static void Fallback(string where, Exception e)
        {
            _useUgui = false;
            _ugui = null;
            FFUIOverhaulMod.Log.Warning(
                $"[Blueprints] uGUI panel failed ({where}: {e.Message}) — falling back to the simple panel.");
        }

        private static bool _open;
        private static Rect _window = new Rect(60f, 90f, 380f, 460f);
        private static Vector2 _scroll;
        private static string _nameField = "";
        private static string _status = "";
        private static float _statusUntil;
        private static string? _confirmDelete;
        private static Blueprint? _suggestedFor;   // clipboard we last named

        private const int WindowId = 0x4D4D01;   // "MM" + 1, unlikely to collide

        internal static bool IsOpen =>
            (_useUgui && _ugui != null) ? _ugui.IsOpen : _open;

        /// <summary>True when the cursor is over the panel. IMGUI windows don't
        /// participate in FF's pointerIsOverUI, so the game treats a click on this
        /// panel as a click on the world — which is why dragging the panel started
        /// a unit-selection box. Anything that reacts to world clicks must consult
        /// this.</summary>
        internal static bool PointerOverPanel
        {
            get
            {
                if (_useUgui && _ugui != null)
                {
                    try { return _ugui.PointerOverPanel; } catch { return false; }
                }
                if (!_open) return false;
                // Input.mousePosition is bottom-left origin; GUI rects are top-left.
                var m = Input.mousePosition;
                return _window.Contains(new Vector2(m.x, Screen.height - m.y));
            }
        }

        /// <summary>The blueprint the player picked to stamp.</summary>
        internal static Blueprint? Selected
        {
            get => (_useUgui && _ugui != null) ? _ugui.Selected : _imguiSelected;
            private set => _imguiSelected = value;
        }
        private static Blueprint? _imguiSelected;

        public static void Toggle()
        {
            var u = Ugui;
            if (u != null)
            {
                try { u.Toggle(); return; }
                catch (Exception e) { Fallback("toggle", e); }
            }

            _open = !_open;
            if (_open)
            {
                BlueprintStore.Invalidate();   // pick up files added while playing
                var clip = BlueprintCapture.Clipboard;
                if (clip != null && _nameField.Length == 0) _nameField = SuggestName(clip);
            }
            _confirmDelete = null;
        }

        public static void Close()
        {
            if (_useUgui && _ugui != null)
            {
                try { _ugui.Close(); return; } catch (Exception e) { Fallback("close", e); }
            }
            _open = false; _confirmDelete = null;
        }

        public static void OnGUI()
        {
            if (_useUgui) return;   // uGUI panel owns the display
            if (!_open) return;
            _window = GUI.Window(WindowId, _window, DrawWindow, "Master Mason — Blueprints");
        }

        private static void DrawWindow(int id)
        {
            var clip = BlueprintCapture.Clipboard;

            // While stamping, the panel must not hold keyboard focus: IMGUI uses
            // Tab to move between controls, which swallowed the rotate key after
            // the first press. Catch Tab here too so a focused field can't eat it.
            if (BlueprintStamp.IsArmed)
            {
                if (Event.current.type == EventType.KeyDown
                    && Event.current.keyCode == BlueprintStamp.RotateKey)
                {
                    BlueprintStamp.Rotate();
                    Event.current.Use();
                }
                GUI.FocusControl(null);
            }

            // Fill the name from the capture as soon as one arrives, even if the
            // panel was already open — previously the suggestion only ran on open,
            // so capturing with the panel up left the field blank.
            if (clip != null && !ReferenceEquals(clip, _suggestedFor))
            {
                _suggestedFor = clip;
                if (string.IsNullOrEmpty(_nameField)) _nameField = SuggestName(clip);
            }

            GUILayout.Space(4f);

            // ── save the current capture ────────────────────────────────────
            GUILayout.Label(clip == null
                ? "No capture yet — arm capture and drag a box to select buildings."
                : $"Captured: {clip.Summary()}");

            GUI.enabled = clip != null;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(40f));
            GUI.SetNextControlName("mm_name");
            _nameField = GUILayout.TextField(_nameField ?? "", 64);
            bool save = GUILayout.Button("Save", GUILayout.Width(60f));
            GUILayout.EndHorizontal();

            // Enter in the name field saves too — the obvious expectation once
            // you've just typed a name.
            bool enterPressed = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && GUI.GetNameOfFocusedControl() == "mm_name";

            if ((save || enterPressed) && clip != null)
            {
                bool overwrite = BlueprintStore.Exists(_nameField);
                if (BlueprintStore.Save(clip, _nameField))
                {
                    SetStatus(overwrite ? $"Overwrote '{_nameField}'." : $"Saved '{_nameField}'.");
                    _nameField = "";   // saved; don't leave it looking unsaved
                    GUI.FocusControl(null);
                }
                else SetStatus("Save failed — see the log.");
                if (enterPressed) Event.current.Use();
            }
            GUI.enabled = true;

            // Copy / Paste mirror the capture and stamp hotkeys. Labelled for what
            // they do to the player's clipboard, not for our internal module names.
            GUILayout.BeginHorizontal();
            bool capturing = BlueprintCapture.IsArmed;
            bool stamping = BlueprintStamp.IsArmed;

            if (GUILayout.Button(capturing ? "Copy… (click map)" : "Copy"))
                BlueprintCapture.ToggleArmed();

            GUI.enabled = Selected != null || clip != null;
            if (GUILayout.Button(stamping ? "Paste… (click map)" : "Paste"))
                BlueprintStamp.Toggle();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Saved blueprints", GUI.skin.box);

            // ── the library ─────────────────────────────────────────────────
            var all = BlueprintStore.All();
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));

            if (all.Count == 0)
            {
                GUILayout.Label("None yet. Capture a layout, name it, and press Save.");
            }
            else
            {
                foreach (var bp in all)
                {
                    bool isSelected = Selected != null && Selected.name == bp.name;

                    GUILayout.BeginHorizontal(GUI.skin.box);
                    GUILayout.BeginVertical();
                    GUILayout.Label((isSelected ? "▶ " : "") + bp.name);
                    GUILayout.Label(bp.Summary(), GUI.skin.label);
                    GUILayout.EndVertical();

                    if (GUILayout.Button(isSelected ? "Selected" : "Select", GUILayout.Width(70f)))
                    {
                        Selected = bp;
                        SetStatus($"'{bp.name}' selected.");
                        _confirmDelete = null;
                    }

                    if (_confirmDelete == bp.name)
                    {
                        if (GUILayout.Button("Sure?", GUILayout.Width(52f)))
                        {
                            if (BlueprintStore.Delete(bp.name))
                            {
                                if (Selected != null && Selected.name == bp.name) Selected = null;
                                SetStatus($"Deleted '{bp.name}'.");
                            }
                            _confirmDelete = null;
                            GUILayout.EndHorizontal();
                            break;   // the list just changed under us
                        }
                    }
                    else if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        _confirmDelete = bp.name;
                    }

                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            // ── footer ──────────────────────────────────────────────────────
            if (_status.Length > 0 && Time.unscaledTime < _statusUntil)
                GUILayout.Label(_status);
            else
                GUILayout.Label(Selected != null
                    ? $"Ready to stamp: {Selected.name}"
                    : "Select a blueprint to stamp.");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open folder"))
            {
                try { Application.OpenURL("file://" + BlueprintStore.Directory()); }
                catch (Exception e) { FFUIOverhaulMod.Log.Warning("[Panel] " + e.Message); }
            }
            if (GUILayout.Button("Refresh")) { BlueprintStore.Invalidate(); SetStatus("Refreshed."); }
            if (GUILayout.Button("Close")) Close();
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static void SetStatus(string s)
        {
            _status = s;
            _statusUntil = Time.unscaledTime + 4f;
        }

        /// <summary>Suggest a name from the capture's contents — the most common
        /// building plus its size beats making the player invent a name.</summary>
        private static string SuggestName(Blueprint bp)
        {
            try
            {
                var counts = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var e in bp.entries)
                {
                    counts.TryGetValue(e.id, out int n);
                    counts[e.id] = n + 1;
                }
                string best = ""; int bestN = 0;
                foreach (var kv in counts)
                    if (kv.Value > bestN) { best = kv.Key; bestN = kv.Value; }
                return bestN > 1 ? $"{best} x{bestN}" : best;
            }
            catch { return ""; }
        }
    }
}
