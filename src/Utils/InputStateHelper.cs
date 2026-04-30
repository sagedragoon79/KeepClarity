using System;
using System.Reflection;
using UnityEngine;

namespace FFUIOverhaul.Utils
{
    /// <summary>
    /// Inspects the game's InputManager state machine to determine
    /// what input context is currently active. This lets us conditionally
    /// fire hotkeys only when the right UI context is in focus.
    ///
    /// IMPORTANT: StateMachine.currentState is always the ROOT state (Input_Default).
    /// StackState manages its own linked stack via a `pushedState` field. The
    /// "active" state — the one PushGlobalSignal hands a signal to first — is the
    /// deepest pushedState in the chain. We walk the chain to find it.
    /// </summary>
    public static class InputStateHelper
    {
        private static FieldInfo? _currentStateField;
        private static FieldInfo? _inputStateMachineField;
        private static FieldInfo? _pushedStateField;

        public static Type? GetCurrentInputStateType()
        {
            var gm = UnitySingleton<GameManager>.Instance;
            if (gm == null) return null;

            var inputMgr = gm.inputManager;
            if (inputMgr == null) return null;

            if (_inputStateMachineField == null)
            {
                _inputStateMachineField = typeof(InputManager).GetField("inputStateMachine",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            var stateMachine = _inputStateMachineField?.GetValue(inputMgr) as StateMachine;
            if (stateMachine == null) return null;

            if (_currentStateField == null)
            {
                _currentStateField = typeof(StateMachine).GetField("currentState",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            var top = _currentStateField?.GetValue(stateMachine) as State;
            if (top == null) return null;

            // StackState.pushedState is `protected` — declared on the base class.
            if (_pushedStateField == null)
            {
                _pushedStateField = typeof(StackState).GetField("pushedState",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            }

            // Walk the chain. After this loop, `top` is the deepest StackState
            // that has no pushedState — the actual active context.
            while (top is StackState ss)
            {
                var next = _pushedStateField?.GetValue(ss) as State;
                if (next == null) break;
                top = next;
            }

            return top.GetType();
        }

        /// <summary>
        /// True when the game is in the "normal" input state — no menus open, no modal,
        /// no placement mode. This is when main-game hotkeys should fire.
        /// </summary>
        public static bool IsInDefaultState()
        {
            var t = GetCurrentInputStateType();
            return t != null && t.Name == "Input_Default";
        }

        /// <summary>
        /// True when a Yes/No/Cancel modal dialog is showing.
        /// </summary>
        public static bool IsInModalState()
        {
            var t = GetCurrentInputStateType();
            return t != null && t.Name == "Input_ModalWindow";
        }

        /// <summary>
        /// True when a game object (building, villager, etc.) is selected.
        /// Building info window hotkeys should fire in this state.
        /// </summary>
        public static bool IsSelectingGameObject()
        {
            var t = GetCurrentInputStateType();
            return t != null && t.Name == "Input_SelectGameObject";
        }

        /// <summary>
        /// True if any "UI-focused" state is active (modal, menu, tech tree, etc.)
        /// where game hotkeys shouldn't fire.
        /// </summary>
        public static bool IsUIActive()
        {
            var t = GetCurrentInputStateType();
            if (t == null) return false;
            string n = t.Name;
            return n == "Input_ModalWindow"
                || n == "Input_TechTree"
                || n == "Input_OptionsMenu"
                || n == "Input_TownCenterOptionsMenu"
                || n == "Input_NewGame"
                || n == "Input_GameOver"
                || n == "Input_EnterText"
                || n == "Input_RansomRequest"
                || n == "Input_TutorialMessage";
        }
    }
}
