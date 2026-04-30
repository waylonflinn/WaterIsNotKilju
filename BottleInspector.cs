// BottleInspector.cs — Temporary debug mod to dump FSM info for kilju/water bottle objects.
//
// WHAT IT DOES:
//   On load, searches for all game objects whose name contains "kilju" or "juiceconcentrate"
//   and dumps their FSMs, variables, and states to the MSCLoader console.
//
//   It also logs the current GUIinteraction value each frame for 5 seconds after load,
//   so you can see what the game sets it to when looking at a bottle.
//
// HOW TO USE:
//   1. Add this file to the project alongside WaterIsNotKiljuMod.cs
//   2. Build and install both mods
//   3. Load a save with a plastic bottle (empty, water, or kilju)
//   4. Open the MSCLoader console (~ key) and search for [BottleInspector]
//   5. Screenshot or copy the output — it contains everything needed to fill in the TODOs
//   6. Remove this file once you've gathered the info
//
// WHAT TO LOOK FOR:
//   - The exact game object name (e.g., "kilju(itemx)", "juiceconcentrate4", etc.)
//   - Which FSM has "KiljuAlc" (probably "Use")
//   - Whether "KiljuAlc" is local to that FSM or on a different one
//   - Which state(s) set "GUIinteraction"
//   - Which state(s) play audio (for the sound fix)

using System;
using System.Text;
using UnityEngine;
using MSCLoader;
using HutongGames.PlayMaker;

namespace WaterIsNotKilju
{
    public class BottleInspector : Mod
    {
        public override string ID => "BottleInspector";
        public override string Name => "Bottle Inspector";
        public override string Author => "Waylon";
        public override string Version => "0.2.0";
        public override string Description => "Dumps FSM info for kilju/water bottle objects.";
        public override Game SupportedGames => Game.MySummerCar;

        private FsmString _guiInteraction;
        private float _logTimer;
        private const float LogDuration = 5f; // Log GUIinteraction for 5 seconds

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, OnLoad);
            SetupFunction(Setup.Update, OnUpdate);
        }

        private void OnLoad()
        {
            ModConsole.Log("=== [BottleInspector] Starting dump ===");

            // Get the global GUIinteraction reference
            _guiInteraction = PlayMakerGlobals.Instance.Variables.FindFsmString("GUIinteraction");
            if (_guiInteraction != null)
                ModConsole.Log("[BottleInspector] GUIinteraction global variable found.");
            else
                ModConsole.Warning("[BottleInspector] GUIinteraction global variable NOT found!");

            // Search for bottle objects using multiple name patterns
            int found = 0;
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                // Match various possible naming patterns
                string nameLower = go.name.ToLower();
                if (nameLower.Contains("kilju") ||
                    nameLower.Contains("juiceconcentrate") ||
                    nameLower.Contains("bottleplastic") ||
                    nameLower.Contains("bottle plastic"))
                {
                    DumpBottleObject(go);
                    found++;
                }
            }

            if (found == 0)
            {
                ModConsole.Warning("[BottleInspector] No bottle objects found! " +
                    "Make sure you have a plastic bottle in your save.");
                ModConsole.Log("[BottleInspector] Dumping ALL game objects with tag 'ITEM'...");
                foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (go.CompareTag("ITEM"))
                    {
                        ModConsole.Log($"  ITEM: {go.name}");
                    }
                }
            }

            ModConsole.Log($"=== [BottleInspector] Dump complete. Found {found} bottle object(s). ===");
            ModConsole.Log("[BottleInspector] Will now log GUIinteraction changes for 5 seconds...");
            ModConsole.Log("[BottleInspector] Look at a water bottle in-game to see what text appears!");
        }

        private void OnUpdate()
        {
            // Log GUIinteraction value for the first few seconds after load
            // This helps identify what text the game sets for water vs kilju
            if (_logTimer < LogDuration && _guiInteraction != null)
            {
                _logTimer += Time.deltaTime;
                // Only log when the value changes
                string currentVal = _guiInteraction.Value;
                if (!string.IsNullOrEmpty(currentVal) && currentVal != _lastGuiValue)
                {
                    ModConsole.Log($"[BottleInspector] GUIinteraction changed to: \"{currentVal}\"");
                    _lastGuiValue = currentVal;
                }
            }
        }

        private string _lastGuiValue = "";

        private void DumpBottleObject(GameObject go)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n[BottleInspector] ========== Object: {go.name} ==========");
            sb.AppendLine($"  Path: {GetPath(go.transform)}");
            sb.AppendLine($"  Active: {go.activeSelf}");
            sb.AppendLine($"  Tag: {go.tag}");
            sb.AppendLine($"  Parent: {(go.transform.parent != null ? go.transform.parent.name : "none")}");

            // List all components
            sb.AppendLine("  Components:");
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null)
                    sb.AppendLine($"    {comp.GetType().Name}");
            }

            var fsms = go.GetComponents<PlayMakerFSM>();
            sb.AppendLine($"  FSMs ({fsms.Length}):");

            foreach (var fsm in fsms)
            {
                sb.AppendLine($"    --- FSM: {fsm.FsmName} ---");

                // Dump all variables
                sb.AppendLine("    Variables:");
                foreach (var v in fsm.FsmVariables.FloatVariables)
                    sb.AppendLine($"      [Float] {v.Name} = {v.Value}");
                foreach (var v in fsm.FsmVariables.IntVariables)
                    sb.AppendLine($"      [Int] {v.Name} = {v.Value}");
                foreach (var v in fsm.FsmVariables.StringVariables)
                    sb.AppendLine($"      [String] {v.Name} = \"{v.Value}\"");
                foreach (var v in fsm.FsmVariables.BoolVariables)
                    sb.AppendLine($"      [Bool] {v.Name} = {v.Value}");
                foreach (var v in fsm.FsmVariables.Vector3Variables)
                    sb.AppendLine($"      [Vector3] {v.Name} = {v.Value}");
                foreach (var v in fsm.FsmVariables.GameObjectVariables)
                    sb.AppendLine($"      [GameObject] {v.Name} = {(v.Value != null ? v.Value.name : "null")}");
                foreach (var v in fsm.FsmVariables.ObjectVariables)
                    sb.AppendLine($"      [Object] {v.Name} = {(v.Value != null ? v.Value.ToString() : "null")}");

                // Dump states and actions
                sb.AppendLine($"    States ({fsm.FsmStates.Length}):");
                foreach (var state in fsm.FsmStates)
                {
                    sb.AppendLine($"      State: {state.Name} ({state.Actions.Length} actions)");
                    foreach (var action in state.Actions)
                    {
                        string typeName = action.GetType().Name;
                        string detail = "";

                        // Add useful details for common action types
                        if (action is HutongGames.PlayMaker.Actions.SetFsmString sfs)
                            detail = $" → SetFsmString: variable=\"{sfs.variableName}\" value=\"{sfs.setValue}\"";
                        else if (action is HutongGames.PlayMaker.Actions.FsmStringAction fsa)
                            detail = $" → FsmStringAction";
                        else if (action is HutongGames.PlayMaker.Actions.AudioPlay ap)
                            detail = $" → AudioPlay";
                        else if (action is HutongGames.PlayMaker.Actions.AudioPlayRandom apr)
                            detail = $" → AudioPlayRandom";
                        else if (action is HutongGames.PlayMaker.Actions.SetFsmFloat sff)
                            detail = $" → SetFsmFloat: variable=\"{sff.variableName}\"";

                        sb.AppendLine($"        {typeName}{detail}");
                    }
                }
            }

            // Also check children for FSMs
            foreach (Transform child in go.transform)
            {
                var childFsms = child.GetComponents<PlayMakerFSM>();
                if (childFsms.Length > 0)
                {
                    sb.AppendLine($"  Child '{child.name}' has {childFsms.Length} FSM(s):");
                    foreach (var fsm in childFsms)
                    {
                        sb.AppendLine($"    FSM: {fsm.FsmName}");
                        foreach (var v in fsm.FsmVariables.FloatVariables)
                            if (v.Name == "KiljuAlc")
                                sb.AppendLine($"      *** [Float] KiljuAlc = {v.Value} (FOUND ON CHILD!) ***");
                    }
                }
            }

            ModConsole.Log(sb.ToString());
        }

        private static string GetPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            while (t != null)
            {
                parts.Insert(0, t.name);
                t = t.parent;
            }
            return string.Join("/", parts);
        }
    }
}