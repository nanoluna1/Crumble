using System;
using UnityEngine;
using MelonLoader;

namespace Main
{
    public static class DebrisSpawner
    {
        public static void Spawn(Transform structure)
        {
            try
            {
                // Make sure every active camera (incl. the live/recording "Rock Camera") renders
                // the debris layer — it may have been toggled on since the last break.
                DebrisLayers.EnsureCamerasSeeDebris();

                var renderer = structure.GetComponentInChildren<MeshRenderer>();
                var filter   = structure.GetComponentInChildren<MeshFilter>();
                if (renderer == null || filter == null || filter.sharedMesh == null)
                {
                    Preferences.Log("No renderer/mesh on structure; skipping debris.");
                    return;
                }

                Transform pivot = filter.transform; // mesh's own transform (matches sharedMesh space)
                Material mat = renderer.sharedMaterial;
                Bounds localBounds = filter.sharedMesh.bounds;

                ChunkData[] chunks = ChunkCache.TryGet(filter.sharedMesh, out var cached)
                    ? cached
                    : ChunkCache.Build(filter.sharedMesh, localBounds);

                foreach (var c in chunks) SpawnOne(c, pivot, mat);
                Preferences.Log($"Spawned {chunks.Length} debris chunks.");
            }
            catch (Exception e)
            {
                Main.Logger.Error($"Spawn failed (gameplay unaffected): {e}");
            }
        }

        private static void SpawnOne(ChunkData c, Transform pivot, Material mat)
        {
            if (DebrisLayers.DebrisLayer < 0) { return; } // layers not set up yet
            var go = new GameObject("CrumbleChunk");
            go.layer = DebrisLayers.DebrisLayer;

            // Place at the structure's pose, offset to the chunk's local position.
            go.transform.position = pivot.TransformPoint(c.LocalOffset);
            go.transform.rotation = pivot.rotation * Quaternion.Euler(
                UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360));
            float sizeMul = Preferences.ChunkSize.Value / 5f; // 5 = 1x (current), 1 = 0.2x, 10 = 2x
            go.transform.localScale = pivot.lossyScale * sizeMul;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = c.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = c.Mesh;
            col.convex = true; // required for a moving Rigidbody collider

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.angularDrag = Preferences.AngularDrag.Value; // angularDrag compiles (angularDamping is ArticulationBody)
            Vector3 ls = pivot.lossyScale;
            float volume = c.Mesh.bounds.size.x * c.Mesh.bounds.size.y * c.Mesh.bounds.size.z
                           * Mathf.Abs(ls.x * ls.y * ls.z) // chunk is simulated at lossyScale
                           * sizeMul * sizeMul * sizeMul;   // scale volume by size multiplier
            rb.mass = Mathf.Max(0.05f, volume * Preferences.MassScale.Value);

            var chunk = go.AddComponent<DebrisChunk>();
            chunk.Init(Preferences.ChunkLifetime.Value);
        }
    }
}
