using System;
using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul
{
    /// <summary>
    /// Hotkey support for forageables / wild bushes. Tended Wilds (and the
    /// older Forageable Transplantation mod) extend the in-game Relocate
    /// button to herbs, mushrooms, greens, roots, nuts, willow and berry
    /// bushes — but the R hotkey only fired for `Building` selections. This
    /// helper closes that gap by routing R presses through
    /// `UIHarvestableResourceWindow.Relocate()` when a forageable is selected.
    ///
    /// Type.GetType("Foo, Assembly-CSharp") only works when Foo has no
    /// namespace AND lives in Assembly-CSharp; FF uses neither guarantee.
    /// We scan loaded assemblies for the simple type name instead — slow on
    /// first call, cached after.
    /// </summary>
    internal static class ForageableActions
    {
        private static Type? _windowType;
        private static MethodInfo? _windowRelocateMethod;
        private static UnityEngine.Object? _cachedWindow;
        private static bool _typeLookupLogged;

        private static Type? FindTypeByName(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                // GetTypes() throws if any referenced assembly fails to load;
                // ReflectionTypeLoadException.Types still has the resolved
                // ones, so use those instead of skipping the whole assembly.
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types; }
                if (types == null) continue;
                foreach (var t in types)
                    if (t != null && t.Name == name) return t;
            }
            return null;
        }

        public static bool IsForageable(GameObject? selectedObj)
        {
            if (selectedObj == null) return false;

            // Walk components on the GameObject and its parents; check by
            // type name so we sidestep namespace/assembly questions entirely.
            // Includes inactive parents because the selectable mesh is
            // sometimes a child of a temporarily-disabled root.
            foreach (var c in selectedObj.GetComponentsInParent<Component>(includeInactive: true))
            {
                if (c == null) continue;
                if (c.GetType().Name == "ForageableResource")
                {
                    if (!_typeLookupLogged)
                    {
                        FFUIOverhaulMod.Log.Msg("[Forageable] ForageableResource detected — R hotkey active");
                        _typeLookupLogged = true;
                    }
                    return true;
                }
            }
            return false;
        }

        private static bool _treeStoneLookupLogged;

        /// <summary>True when a Tree, Stone deposit, or excavated Stone Ruins is
        /// selected. These open the SAME UIHarvestableResourceWindow as
        /// forageables, so Harvest / Delete / Prioritize route through the
        /// identical window methods — they were just never wired into the hotkey
        /// selection check. (No Relocate: none of these can be relocated.)</summary>
        public static bool IsTreeOrStone(GameObject? selectedObj)
        {
            if (selectedObj == null) return false;
            foreach (var c in selectedObj.GetComponentsInParent<Component>(includeInactive: true))
            {
                if (c == null) continue;
                var n = c.GetType().Name;
                if (n == "TreeResource" || n == "StoneResource" || n == "StoneRuinsResource")
                {
                    if (!_treeStoneLookupLogged)
                    {
                        FFUIOverhaulMod.Log.Msg("[Forageable] " + n + " detected — Harvest/Delete/Prioritize hotkeys active");
                        _treeStoneLookupLogged = true;
                    }
                    return true;
                }
            }
            return false;
        }

        public static void TryRelocate()
        {
            try
            {
                if (_windowType == null) _windowType = FindTypeByName("UIHarvestableResourceWindow");
                if (_windowType == null) { FFUIOverhaulMod.Log.Warning("[Forageable] UIHarvestableResourceWindow type not found"); return; }

                // Unity-aware == handles destroyed-but-not-null automatically,
                // so a stale reference from a prior scene re-resolves cleanly.
                if (_cachedWindow == null)
                {
                    var found = UnityEngine.Object.FindObjectOfType(_windowType);
                    if (found is UnityEngine.Object uo) _cachedWindow = uo;
                }
                if (_cachedWindow == null) { FFUIOverhaulMod.Log.Warning("[Forageable] No active UIHarvestableResourceWindow instance"); return; }

                if (_windowRelocateMethod == null)
                {
                    // Don't restrict to Type.EmptyTypes — UIHarvestableResourceWindow.Relocate
                    // turned out to take parameters in this game version, so the
                    // parameterless lookup returned null. Iterate methods and pick
                    // any "Relocate" with parameters we can satisfy: zero-arg or
                    // single-arg-with-default.
                    foreach (var m in _windowType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (m.Name != "Relocate") continue;
                        _windowRelocateMethod = m;
                        FFUIOverhaulMod.Log.Msg($"[Forageable] Found Relocate({m.GetParameters().Length} params)");
                        break;
                    }
                }
                if (_windowRelocateMethod == null) { FFUIOverhaulMod.Log.Warning("[Forageable] Relocate() method not found on window"); return; }

                // Build args matching the parameter count: pass null/default for
                // each. Most likely signature is zero-arg or takes one optional
                // bool/int that the in-game button click also passes as default.
                var pars = _windowRelocateMethod.GetParameters();
                object[] args = new object[pars.Length];
                for (int i = 0; i < pars.Length; i++)
                    args[i] = pars[i].HasDefaultValue ? pars[i].DefaultValue
                        : (pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null);
                _windowRelocateMethod.Invoke(_cachedWindow, args);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[Forageable] Relocate via R failed: {e.Message}");
            }
        }

        /// <summary>Invoke a parameterless private void method on the active
        /// UIHarvestableResourceWindow via reflection. Used by Harvest / Delete
        /// hotkeys, which target methods FF wires to the visible buttons but
        /// keeps private (MarkResourceForHarvest, MarkResourceForClear).
        /// No-op (silent) if the method doesn't exist or the window isn't
        /// currently the active selection's info panel.</summary>
        private static void TryInvokeWindowMethod(string methodName)
        {
            try
            {
                if (_windowType == null) _windowType = FindTypeByName("UIHarvestableResourceWindow");
                if (_windowType == null) return;
                if (_cachedWindow == null)
                {
                    var found = UnityEngine.Object.FindObjectOfType(_windowType);
                    if (found is UnityEngine.Object uo) _cachedWindow = uo;
                }
                if (_cachedWindow == null) return;

                var m = _windowType.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                m?.Invoke(_cachedWindow, null);
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[Forageable] {methodName} failed: {e.Message}");
            }
        }

        private static bool _fruitTreeLookupLogged;

        /// <summary>True when a fruit tree is selected. FruitTreeResource subclasses
        /// TreeResource, so the exact-name tree check above deliberately misses it —
        /// fruit trees get their own hotkeys (Cull For Wood + Prioritize) routed
        /// through the same UIHarvestableResourceWindow (cull = MarkResourceForClear).</summary>
        public static bool IsFruitTree(GameObject? selectedObj)
        {
            if (selectedObj == null) return false;
            foreach (var c in selectedObj.GetComponentsInParent<Component>(includeInactive: true))
            {
                if (c == null) continue;
                if (c.GetType().Name == "FruitTreeResource")
                {
                    if (!_fruitTreeLookupLogged)
                    {
                        FFUIOverhaulMod.Log.Msg("[Forageable] FruitTreeResource detected — Cull/Prioritize hotkeys active");
                        _fruitTreeLookupLogged = true;
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Cull a fruit tree for wood — same as the native "Cull For Wood"
        /// button, which is wired to MarkResourceForClear.</summary>
        public static void TryCullForWood() => TryInvokeWindowMethod("MarkResourceForClear");

        public static void TryHarvest() => TryInvokeWindowMethod("MarkResourceForHarvest");
        public static void TryDelete()  => TryInvokeWindowMethod("MarkResourceForClear");

        /// <summary>Flip the Prioritized toggle on the active forageable
        /// window. FF holds the toggle in a private field named "toggle".</summary>
        public static void TryTogglePrioritize()
        {
            try
            {
                if (_windowType == null) _windowType = FindTypeByName("UIHarvestableResourceWindow");
                if (_windowType == null) return;
                if (_cachedWindow == null)
                {
                    var found = UnityEngine.Object.FindObjectOfType(_windowType);
                    if (found is UnityEngine.Object uo) _cachedWindow = uo;
                }
                if (_cachedWindow == null) return;

                var f = _windowType.GetField("toggle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (f?.GetValue(_cachedWindow) is UnityEngine.UI.Toggle tg)
                    tg.isOn = !tg.isOn;
            }
            catch (Exception e)
            {
                FFUIOverhaulMod.Log.Warning($"[Forageable] Prioritize toggle failed: {e.Message}");
            }
        }
    }
}
