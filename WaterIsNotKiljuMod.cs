using System;
using UnityEngine;
using MSCLoader;
using HutongGames.PlayMaker;

namespace WaterIsNotKilju
{
    public class WaterIsNotKiljuMod : Mod
    {
        public override string ID => "WaterIsNotKilju";
        public override string Name => "Water Is Not Kilju";
        public override string Author => "Waylon";
        public override string Version => "1.0.0";
        public override string Description =>
            "Makes plastic bottles correctly show 'Water' instead of 'Kilju' when filled with water.";
        public override Game SupportedGames => Game.MySummerCar;

        // ─── Configuration ───────────────────────────────────────────────
        // These values MUST be updated after MSCToolset investigation.
        // See README.md sections 2 and 3 for how to discover them.

        // The exact name of the plastic bottle game object in the scene hierarchy.
        // TODO: Replace with actual object path found via MSCToolset.
        private const string BottleObjectName = "TODO_BottleObjectName";

        // The name of the FSM on the bottle that handles interaction/display.
        // Bottles may have multiple FSMs — we need the one that sets GUIinteraction.
        // TODO: Replace with actual FSM name found via MSCToolset.
        private const string BottleFsmName = "TODO_FsmName";

        // The name of the FSM state that sets the hover/interaction text.
        // TODO: Replace with actual state name found via MSCToolset.
        private const string SetTextStateName = "TODO_StateName";

        // The FSM variable that distinguishes water from kilju in the bottle.
        // Could be an FsmString, FsmInt, FsmBool, or FsmEnum.
        // TODO: Replace with actual variable name and type found via MSCToolset.
        private const string ContentVarName = "TODO_ContentVariableName";

        // The value of ContentVar when the bottle contains water.
        // Could be a string like "water", an int like 1, a bool, etc.
        // TODO: Replace with actual water value found via MSCToolset.
        private const string WaterContentValue = "TODO_WaterValue";

        // The global PlayMaker variable used to display the hover text.
        private const string GuiInteractionVar = "GUIinteraction";

        // ─── Mod Lifecycle ────────────────────────────────────────────────

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
        }

        private void Mod_OnLoad()
        {
            // Validate that the TODOs have been filled in.
            if (BottleObjectName.StartsWith("TODO_"))
            {
                ModConsole.Error("[WaterIsNotKilju] BottleObjectName not configured! " +
                                 "Run MSCToolset investigation first. See README.md.");
                return;
            }

            HookBottleFsm();
        }

        // ─── FSM Hooking ─────────────────────────────────────────────────

        private void HookBottleFsm()
        {
            // Find the bottle game object.
            // Some items are nested — you may need GameObject.Find with a path
            // like "ITEMS/BottlePlastic" or search by tag/layer.
            GameObject bottle = GameObject.Find(BottleObjectName);
            if (bottle == null)
            {
                ModConsole.Error($"[WaterIsNotKilju] Could not find game object: {BottleObjectName}");
                return;
            }

            // Get the specific FSM component.
            PlayMakerFSM fsm = null;
            foreach (var comp in bottle.GetComponents<PlayMakerFSM>())
            {
                if (comp.FsmName == BottleFsmName)
                {
                    fsm = comp;
                    break;
                }
            }

            if (fsm == null)
            {
                ModConsole.Error($"[WaterIsNotKilju] Could not find FSM '{BottleFsmName}' on {BottleObjectName}");
                return;
            }

            // Store a reference so the hook closure can read the content variable.
            var fsmRef = fsm;

            // Inject our hook into the state that sets the interaction text.
            // When the game sets "Kilju" but the bottle actually contains water,
            // we overwrite the GUIinteraction value to "Water".
            FsmState state = null;
            foreach (var s in fsmRef.FsmStates)
            {
                if (s.Name == SetTextStateName)
                {
                    state = s;
                    break;
                }
            }

            if (state == null)
            {
                ModConsole.Error($"[WaterIsNotKilju] Could not find state '{SetTextStateName}' " +
                                 $"in FSM '{BottleFsmName}'");
                return;
            }

            // Insert our correction action at the end of the state's actions.
            // This runs after the game's original action sets "Kilju", so we
            // can check and override it.
            var hookAction = new ContentCheckAction(fsmRef, ContentVarName, WaterContentValue);
            var actions = new System.Collections.Generic.List<FsmStateAction>(state.Actions);
            actions.Add(hookAction);
            state.Actions = actions.ToArray();

            ModConsole.Log("[WaterIsNotKilju] Successfully hooked bottle FSM!");
        }

        // ─── Custom FsmStateAction ────────────────────────────────────────

        /// <summary>
        /// Runs after the game's original state actions. If the bottle's content
        /// variable indicates water, overwrites GUIinteraction from "Kilju" to "Water".
        ///
        /// This is a separate class so it integrates cleanly into the PlayMaker
        /// state machine — it runs as part of the FSM's normal execution.
        /// </summary>
        private class ContentCheckAction : FsmStateAction
        {
            private readonly PlayMakerFSM _fsm;
            private readonly string _contentVarName;
            private readonly string _waterValue;

            public ContentCheckAction(PlayMakerFSM fsm, string contentVarName, string waterValue)
            {
                _fsm = fsm;
                _contentVarName = contentVarName;
                _waterValue = waterValue;
            }

            public override void OnEnter()
            {
                var guiText = PlayMakerGlobals.Instance.Variables
                    .FindFsmString(GuiInteractionVar);

                if (guiText == null)
                {
                    Finish();
                    return;
                }

                // Only intervene if the game just set the text to "Kilju"
                if (guiText.Value != "Kilju")
                {
                    Finish();
                    return;
                }

                // ── Check the content variable ──────────────────────────────
                // TODO: Update this check based on the actual variable type
                // discovered via MSCToolset. Examples:
                //
                // If FsmString:
                //   var content = _fsm.FsmVariables.GetFsmString(_contentVarName);
                //   if (content.Value == _waterValue) guiText.Value = "Water";
                //
                // If FsmInt:
                //   var content = _fsm.FsmVariables.GetFsmInt(_contentVarName);
                //   if (content.Value.ToString() == _waterValue) guiText.Value = "Water";
                //
                // If FsmBool:
                //   var content = _fsm.FsmVariables.GetFsmBool(_contentVarName);
                //   if (content.Value) guiText.Value = "Water";
                //
                // If the variable is on a DIFFERENT FSM on the same object:
                //   var otherFsm = _fsm.gameObject.GetComponents<PlayMakerFSM>()
                //       .First(f => f.FsmName == "OtherFsmName");
                //   var content = otherFsm.FsmVariables.GetFsmString(_contentVarName);
                //
                // If the variable is a GLOBAL (not per-FSM):
                //   var content = PlayMakerGlobals.Instance.Variables
                //       .FindFsmString(_contentVarName);

                // Default: try as FsmString. Update this after investigation.
                try
                {
                    var content = _fsm.FsmVariables.GetFsmString(_contentVarName);
                    if (content != null && content.Value == _waterValue)
                    {
                        guiText.Value = "Water";
                    }
                }
                catch (Exception ex)
                {
                    ModConsole.Error($"[WaterIsNotKilju] Error reading content variable: {ex.Message}");
                }

                Finish();
            }
        }
    }
}
