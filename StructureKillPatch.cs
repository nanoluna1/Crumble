using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using Il2CppRUMBLE.MoveSystem;

namespace Main
{
    // Prefix (not postfix): structures are POOLED and recycled, so we must read the
    // structure's transform/mesh BEFORE the original Kill runs and the game returns it
    // to the pool. We return true so the game's own death logic runs untouched.
    [HarmonyPatch(typeof(Structure), "Kill",
        new Type[] { typeof(Vector3), typeof(bool), typeof(bool), typeof(bool) })]
    public static class StructureKillPatch
    {
        public static bool Prefix(Structure __instance, Vector3 __0)
        {
            try
            {
                if (!Preferences.Enabled.Value) return true;
                Transform t = __instance.transform;
                Preferences.Log($"Structure.Kill fired at {t.position} (killPoint {__0})");
                DebrisSpawner.Spawn(t);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Crumble] Kill prefix threw (gameplay unaffected): {e}");
            }
            return true; // never block the game's death logic
        }
    }
}
