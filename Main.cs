using System;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Main.Main), "Crumble", "1.0.0", "Nano")]
[assembly: MelonColor(127, 52, 235, 131)]
[assembly: MelonGame("Buckethead Entertainment", "RUMBLE")]
[assembly: MelonLoader.MelonAdditionalDependencies(new string[] { "RockUI" })]

namespace Main
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                Preferences.Init();
                try { ClassInjector.RegisterTypeInIl2Cpp<DebrisChunk>(); }
                catch (Exception ex) { MelonLogger.Warning($"[Crumble] DebrisChunk register: {ex.Message}"); }
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
                MelonLogger.Msg("[Crumble] Initialized and patched Structure.Kill.");
                RumbleModdingAPI.RMAPI.Actions.onMapInitialized += OnMapInitialized; // delegate is Action<string> (map name)
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Crumble] Init failed: {e}");
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
            catch (System.Exception e) { MelonLogger.Error($"[Crumble] map-init failed: {e}"); }
        }

        // ── Input toggle ───────────────────────────────────────────────────
        // Hold Left Trigger + press X (left primary) to open/close the settings panel.
        // Only active in Gym or Park; logs the actual scene name on every rising edge.
        private bool _comboWasDown;

        public override void OnUpdate()
        {
            try
            {
                float trig  = RumbleModdingAPI.RMAPI.Calls.ControllerMap.LeftController.GetTrigger();
                float x     = RumbleModdingAPI.RMAPI.Calls.ControllerMap.LeftController.GetPrimary();
                bool  combo = trig > 0.5f && x > 0.5f;

                if (combo && !_comboWasDown)
                {
                    string scene = RumbleModdingAPI.RMAPI.Calls.Scene.GetSceneName();
                    MelonLogger.Msg($"[Crumble] Menu combo pressed in scene '{scene}'.");

                    if (scene == "Gym" || scene == "Park")
                    {
                        CrumbleUI.Toggle();
                    }
                    else
                    {
                        MelonLogger.Msg($"[Crumble] Settings menu only opens in Gym or Park (current: '{scene}').");
                    }
                }

                _comboWasDown = combo;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Crumble] OnUpdate error: {ex}");
            }
        }
    }
}
