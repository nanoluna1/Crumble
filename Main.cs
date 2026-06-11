using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;
using LogInstance = MelonLoader.MelonLogger.Instance; // alias so logging never references MelonLogger directly

[assembly: MelonInfo(typeof(Main.Main), "Crumble", "1.0.0", "Nano")]
[assembly: MelonColor(127, 52, 235, 131)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonLoader.MelonAdditionalDependencies(new string[] { "RockUI" })]

namespace Main
{
    public class Main : MelonMod
    {
        // Shared Logger instance (auto-prefixes "[Crumble]"). Set first thing in init so every
        // static class can log through Main.Logger instead of any static logger.
        public static LogInstance Logger;

        public override void OnInitializeMelon()
        {
            Logger = LoggerInstance;
            try
            {
                Preferences.Init();
                try { ClassInjector.RegisterTypeInIl2Cpp<DebrisChunk>(); }
                catch (Exception ex) { Logger.Warning($"DebrisChunk register: {ex.Message}"); }
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                Logger.Msg("Initialized and patched Structure.Kill.");
                RumbleModdingAPI.RMAPI.Actions.onMapInitialized += OnMapInitialized; // delegate is Action<string> (map name)
            }
            catch (Exception e)
            {
                Logger.Error($"Init failed: {e}");
            }
        }

        private bool _layersReady;
        private void OnMapInitialized(string mapName)
        {
            try
            {
                // Close any open menu panel — its GameObject will be destroyed by scene
                // unload anyway, but we reset _panel to null explicitly so IsOpen is correct.
                CrumbleUI.Close();

                DebrisLayers.DumpLayers();
                if (!_layersReady) { DebrisLayers.Setup(); _layersReady = true; }
                ChunkCache.Clear(); // meshes differ per map
                MelonCoroutines.Start(ChunkCache.PreWarm());
            }
            catch (System.Exception e) { Logger.Error($"map-init failed: {e}"); }
        }

        // ── Input toggle ───────────────────────────────────────────────────
        // Hold Left Trigger (left hand) + press B (right controller secondary) to open/close
        // the settings panel. Only active in Gym or Park; logs the scene name on every rising edge.
        private bool _comboWasDown;
        private int _camFrame;

        public override void OnUpdate()
        {
            try
            {
                // Continuously (throttled) enforce the debris layer on cameras so the live/rock-cam
                // feed reliably matches RockCamVisibility — the rock cam is (re)created when picked up
                // and the game resets its cullingMask, which wiped one-shot applications.
                if (++_camFrame >= 10) { _camFrame = 0; DebrisLayers.EnsureCamerasSeeDebris(); }

                float trig  = RumbleModdingAPI.RMAPI.Calls.ControllerMap.LeftController.GetTrigger();
                float b     = RumbleModdingAPI.RMAPI.Calls.ControllerMap.RightController.GetSecondary(); // B button
                bool  combo = trig > 0.5f && b > 0.5f;

                if (combo && !_comboWasDown)
                {
                    string scene = RumbleModdingAPI.RMAPI.Calls.Scene.GetSceneName();
                    Logger.Msg($"Menu combo pressed in scene '{scene}'.");

                    if (scene == "Gym" || scene == "Park")
                    {
                        CrumbleUI.Toggle();
                    }
                    else
                    {
                        Logger.Msg($"Settings menu only opens in Gym or Park (current: '{scene}').");
                    }
                }

                _comboWasDown = combo;
            }
            catch (Exception ex)
            {
                Logger.Error($"OnUpdate error: {ex}");
            }
        }
    }
}
