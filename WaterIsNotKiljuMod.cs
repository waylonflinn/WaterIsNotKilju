using System;
using System.Collections.Generic;
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
            "Makes plastic bottles correctly show 'Water' instead of 'Kilju' when filled with water, " +
            "and plays the correct drinking sound effect.";
        public override Game SupportedGames => Game.MySummerCar;

        // ─── Configuration ───────────────────────────────────────────────
        // Discovered via Developer Toolkit / MSC runtime investigation.

        // The game object name pattern for kilju/water bottles.
        // MSC items that can have multiple instances use the (itemx) suffix.
        // The BottleInspector can confirm the exact name.
        // TODO: Confirm exact game object name via BottleInspector.
        //       Could be "kilju(itemx)", "juiceconcentrate(itemx)", etc.
        private const string BottleObjectName = "kilju(itemx)";

        // The FSM name on the bottle that contains the KiljuAlc variable
        // and handles item interaction. MSC items typically use "Use".
        // PartInspector uses GetPlayMaker(go, "Use") for all fluid containers.
        // TODO: Confirm via BottleInspector if KiljuAlc is on the "Use" FSM.
        private const string BottleFsmName = "Use";

        // The FSM variable that distinguishes water from kilju in the bottle.
        // KiljuAlc: alcohol content — 0 means water (no alcohol), >0 means kilju.
        // Confirmed: type is FsmFloat, local to the FSM (not global).
        private const string ContentVarName = "KiljuAlc";

        // The value of KiljuAlc when the bottle contains water.
        private const float WaterContentValue = 0f;

        // The global PlayMaker variable used to display the hover text.
        private const string GuiInteractionVar = "GUIinteraction";

        // ─── Sound Fix Configuration ────────────────────────────────────
        // TODO: Fill these in after Developer Toolkit investigation of the
        // bottle's "Use" FSM states (specifically the drinking states).

        // The name of the FSM state that plays the drinking sound.
        private const string DrinkSoundStateName = "TODO_DrinkSoundStateName";

        // The name of the AudioSource component (or child object) that plays
        // the drinking sound on the bottle. Empty string = auto-detect.
        private const string DrinkAudioSourceName = "";

        // How to find the water drinking AudioClip.
        // "SceneObject" = clip on a scene object (e.g. MasterAudio path)
        // "AssetBundle" = load from mod's Assets folder
        // "Custom" = custom loading logic in LoadWaterClip()
        private const string WaterClipSource = "TODO_SceneObject_or_AssetBundle_or_Custom";
        private const string WaterClipPath = "TODO_PathToWaterClipObject";
        private const string WaterClipFilename = "water_drink.wav";

        // ─── Runtime State ───────────────────────────────────────────────

        private FsmString _guiInteraction;
        private AudioClip _waterClip;
        private bool _soundHooksInstalled;

        // ─── Mod Lifecycle ────────────────────────────────────────────────

        public override void ModSetup()
        {
            SetupFunction(Setup.OnLoad, Mod_OnLoad);
            SetupFunction(Setup.Update, Mod_OnUpdate);
        }

        private void Mod_OnLoad()
        {
            // Get the global GUIinteraction variable reference once.
            _guiInteraction = PlayMakerGlobals.Instance.Variables
                .FindFsmString(GuiInteractionVar);

            if (_guiInteraction == null)
            {
                ModConsole.Error("[WaterIsNotKilju] Could not find global variable: " +
                                 GuiInteractionVar);
                return;
            }

            ModConsole.Log("[WaterIsNotKilju] Initialized. Text fix active.");

            // Install sound hooks if configured.
            if (!DrinkSoundStateName.StartsWith("TODO_"))
            {
                LoadWaterClip();
                InstallSoundHooks();
            }
            else
            {
                ModConsole.Log("[WaterIsNotKilju] Sound fix not configured — " +
                               "text-only mode. See README.md for setup.");
            }
        }

        // ─── Hook 1: Text Label Fix (Update-based, PartInspector pattern) ──
        // Instead of hooking FSM states, we check GUIinteraction each frame.
        // If it says "Kilju", we raycast to find the item, check KiljuAlc,
        // and override to "Water" if KiljuAlc == 0.
        // This approach handles unlimited instances and doesn't require
        // knowing which FSM state sets the text.

        private void Mod_OnUpdate()
        {
            if (_guiInteraction == null)
                return;

            // Only intervene when the game is about to display "Kilju"
            if (_guiInteraction.Value != "Kilju")
                return;

            // Raycast to find what the player is looking at
            RaycastHit hit = UnifiedRaycast.GetRaycastHit();

            if (hit.distance <= 1f && hit.collider != null)
            {
                GameObject go = hit.collider.gameObject;

                // Check if this is a kilju/water bottle
                // MSC items use the (itemx) suffix for pickable instances.
                // The collider might be on a child, so check parents too.
                if (!IsBottleObject(go))
                {
                    if (go.transform.parent != null && IsBottleObject(go.transform.parent.gameObject))
                        go = go.transform.parent.gameObject;
                    else
                        return;
                }

                // Get the "Use" FSM and check KiljuAlc
                PlayMakerFSM fsm = go.GetPlayMaker(BottleFsmName);
                if (fsm == null)
                {
                    // Try other FSMs on the object if "Use" isn't found
                    fsm = FindFsmWithVariable(go);
                }

                if (fsm != null)
                {
                    FsmFloat kiljuAlc = fsm.FsmVariables.GetFsmFloat(ContentVarName);
                    if (kiljuAlc != null && kiljuAlc.Value == WaterContentValue)
                    {
                        _guiInteraction.Value = "Water";
                    }
                }
            }
        }

        /// <summary>
        /// Check if a game object is a kilju/water bottle.
        /// Uses the configured BottleObjectName pattern.
        /// </summary>
        private static bool IsBottleObject(GameObject go)
        {
            return go != null && go.name.Contains("kilju");
        }

        /// <summary>
        /// Fallback: search all FSMs on an object for one containing KiljuAlc.
        /// </summary>
        private static PlayMakerFSM FindFsmWithVariable(GameObject go)
        {
            foreach (var fsm in go.GetComponents<PlayMakerFSM>())
            {
                var val = fsm.FsmVariables.GetFsmFloat(ContentVarName);
                if (val != null)
                    return fsm;
            }
            return null;
        }

        // ─── Hook 2: Drink Sound Fix (FSM injection) ──────────────────────
        // Sound fix requires precise timing (swap audio clip before AudioPlay
        // fires), so we still use FSM state injection for this.

        private void LoadWaterClip()
        {
            if (WaterClipSource.StartsWith("TODO_"))
            {
                ModConsole.Warning("[WaterIsNotKilju] WaterClipSource not configured — " +
                                   "sound fix disabled.");
                return;
            }

            switch (WaterClipSource)
            {
                case "SceneObject":
                    var clipObj = GameObject.Find(WaterClipPath);
                    if (clipObj == null)
                    {
                        ModConsole.Error("[WaterIsNotKilju] Could not find water clip object: " +
                                         WaterClipPath);
                        return;
                    }
                    var audioSrc = clipObj.GetComponent<AudioSource>();
                    if (audioSrc == null || audioSrc.clip == null)
                    {
                        ModConsole.Error("[WaterIsNotKilju] No AudioSource/clip on: " + WaterClipPath);
                        return;
                    }
                    _waterClip = audioSrc.clip;
                    ModConsole.Log("[WaterIsNotKilju] Loaded water clip from scene: " + _waterClip.name);
                    break;

                case "AssetBundle":
                    _waterClip = LoadAssets.LoadAudioClip(this, WaterClipFilename, false);
                    if (_waterClip == null)
                    {
                        ModConsole.Error("[WaterIsNotKilju] Failed to load water clip: " +
                                         WaterClipFilename);
                        return;
                    }
                    ModConsole.Log("[WaterIsNotKilju] Loaded water clip from assets: " + _waterClip.name);
                    break;

                case "Custom":
                    ModConsole.Warning("[WaterIsNotKilju] Custom clip loading not implemented. " +
                                       "Update LoadWaterClip() in the mod code.");
                    break;

                default:
                    ModConsole.Error("[WaterIsNotKilju] Unknown WaterClipSource: " + WaterClipSource);
                    break;
            }
        }

        /// <summary>
        /// Install sound hooks on all bottle instances in the scene.
        /// Called once at load time. For dynamically spawned bottles,
        /// we'd need to re-run this (future enhancement).
        /// </summary>
        private void InstallSoundHooks()
        {
            if (_waterClip == null)
            {
                ModConsole.Warning("[WaterIsNotKilju] No water clip loaded — skipping sound hooks.");
                return;
            }

            int hooked = 0;
            foreach (var fsm in UnityEngine.Object.FindObjectsOfType<PlayMakerFSM>())
            {
                if (!IsBottleObject(fsm.gameObject))
                    continue;

                if (fsm.FsmName != BottleFsmName)
                    continue;

                var state = FindState(fsm, DrinkSoundStateName);
                if (state == null)
                {
                    ModConsole.Warning("[WaterIsNotKilju] Could not find state '" +
                                       DrinkSoundStateName + "' on " + fsm.gameObject.name);
                    continue;
                }

                AudioSource bottleAudio = string.IsNullOrEmpty(DrinkAudioSourceName)
                    ? fsm.gameObject.GetComponentInChildren<AudioSource>()
                    : fsm.gameObject.transform.Find(DrinkAudioSourceName)?.GetComponent<AudioSource>();

                if (bottleAudio == null)
                {
                    ModConsole.Warning("[WaterIsNotKilju] No AudioSource on " +
                                       fsm.gameObject.name + " — skipping sound hook.");
                    continue;
                }

                var hookAction = new DrinkSoundFixAction(fsm, ContentVarName,
                    WaterContentValue, bottleAudio, _waterClip);
                AppendAction(state, hookAction);
                hooked++;
            }

            if (hooked > 0)
            {
                _soundHooksInstalled = true;
                ModConsole.Log("[WaterIsNotKilju] Sound hooks installed on " + hooked + " bottle(s).");
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────

        private static FsmState FindState(PlayMakerFSM fsm, string stateName)
        {
            foreach (var s in fsm.FsmStates)
            {
                if (s.Name == stateName)
                    return s;
            }
            return null;
        }

        private static void AppendAction(FsmState state, FsmStateAction action)
        {
            var actions = new List<FsmStateAction>(state.Actions);
            actions.Add(action);
            state.Actions = actions.ToArray();
        }

        // ─── Custom FsmStateAction: Drink Sound Fix ───────────────────────

        /// <summary>
        /// Hook 2: Before the drinking sound plays, checks if the bottle contains
        /// water (KiljuAlc == 0) and swaps the AudioSource clip.
        /// </summary>
        private class DrinkSoundFixAction : FsmStateAction
        {
            private readonly PlayMakerFSM _fsm;
            private readonly string _contentVarName;
            private readonly float _waterValue;
            private readonly AudioSource _audioSource;
            private readonly AudioClip _waterClip;
            private AudioClip _originalClip;

            public DrinkSoundFixAction(PlayMakerFSM fsm, string contentVarName,
                float waterValue, AudioSource audioSource, AudioClip waterClip)
            {
                _fsm = fsm;
                _contentVarName = contentVarName;
                _waterValue = waterValue;
                _audioSource = audioSource;
                _waterClip = waterClip;
            }

            public override void OnEnter()
            {
                if (IsWaterContent())
                {
                    // Save original clip so we can restore it (prevents affecting
                    // other game logic that might reference the same AudioSource)
                    _originalClip = _audioSource.clip;
                    _audioSource.clip = _waterClip;
                }

                Finish();
            }

            private bool IsWaterContent()
            {
                try
                {
                    var content = _fsm.FsmVariables.GetFsmFloat(_contentVarName);
                    return content != null && content.Value == _waterValue;
                }
                catch (Exception ex)
                {
                    ModConsole.Error("[WaterIsNotKilju] Error reading KiljuAlc in sound fix: " +
                                     ex.Message);
                    return false;
                }
            }
        }
    }
}