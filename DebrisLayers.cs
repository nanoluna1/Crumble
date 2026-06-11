using UnityEngine;

namespace Main
{
    public static class DebrisLayers
    {
        public static int DebrisLayer = -1;

        // The ONLY layers debris should collide with: the arena surfaces it rests on.
        // Everything else (players, hands, Move stones, interaction, triggers) is ignored,
        // so chunks are pure inert debris that lands and piles but never touches gameplay.
        // Names confirmed from RUMBLE's live layer dump.
        private static readonly string[] CollideNames =
        {
            "Floor", "CombatFloor", "Environment", "PedestalFloor"
        };

        public static void DumpLayers()
        {
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    Preferences.Log($"Layer {i} = '{name}'");
            }
        }

        public static void Setup()
        {
            DebrisLayer = FindUnusedLayer();
            if (DebrisLayer < 0) { DebrisLayer = 26; } // last-resort fallback

            // RUMBLE uses a custom collision matrix, so we can't trust Unity's "everything
            // collides" default for an unused layer (that's why chunks fell through the floor).
            // Be deterministic: ignore EVERY layer first, then re-enable only the surfaces
            // debris should land on + itself (so chunks pile).
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(DebrisLayer, i, true);

            foreach (var n in CollideNames)
            {
                int l = LayerMask.NameToLayer(n);
                if (l >= 0) Physics.IgnoreLayerCollision(DebrisLayer, l, false);
            }
            Physics.IgnoreLayerCollision(DebrisLayer, DebrisLayer, false); // chunks pile on each other

            EnsureCamerasSeeDebris();
            Preferences.Log($"Debris layer = {DebrisLayer}");
        }

        private static bool _loggedCams;

        // The player's headset always renders debris. Every OTHER camera (RecordingCamera,
        // the live/rock-cam feed, mirrors, etc.) shows debris only when RockCamVisibility is on.
        public static void EnsureCamerasSeeDebris()
        {
            if (DebrisLayer < 0) return;
            int mask = 1 << DebrisLayer;
            bool show = Preferences.RockCamVisibility.Value;

            var cams = Camera.allCameras;
            if (cams == null) return;
            foreach (var cam in cams)
            {
                if (cam == null) continue;
                string n = cam.name ?? string.Empty;
                bool isHeadset = n == "Headset" || n.ToLowerInvariant().Contains("headset");

                if (!_loggedCams)
                    Preferences.Log($"Camera '{cam.name}' layer={cam.gameObject.layer} headset={isHeadset} show={(isHeadset || show)}");

                if (isHeadset || show) cam.cullingMask |= mask;   // visible
                else                   cam.cullingMask &= ~mask;  // hidden in non-headset cams
            }
            _loggedCams = true;
        }

        private static int FindUnusedLayer()
        {
            // 8..31 are user layers; pick the highest empty one to avoid clashing with game layers.
            for (int i = 31; i >= 8; i--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i))) return i;
            return -1;
        }
    }
}
