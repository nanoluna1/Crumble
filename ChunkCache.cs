using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using Il2CppRUMBLE.MoveSystem;

namespace Main
{
    public static class ChunkCache
    {
        private static readonly Dictionary<string, ChunkData[]> Cache = new Dictionary<string, ChunkData[]>();
        private static readonly Dictionary<int, (Mesh mesh, Bounds bounds)> Seen = new Dictionary<int, (Mesh, Bounds)>();

        private static string MakeKey(Mesh mesh, int count, bool minecraft)
            => $"{mesh.GetInstanceID()}|{count}|{minecraft}";

        public static bool TryGet(Mesh mesh, out ChunkData[] chunks)
        {
            int count = Preferences.ChunksPerBreak.Value;
            bool minecraft = Preferences.ChunkStyleMinecraft.Value;
            return Cache.TryGetValue(MakeKey(mesh, count, minecraft), out chunks);
        }

        // Build + store geometry for one mesh (used by pre-warm and lazy fallback).
        public static ChunkData[] Build(Mesh mesh, Bounds bounds)
        {
            // Remember this source mesh so PrewarmCurrent() can rebuild it on settings changes.
            Seen[mesh.GetInstanceID()] = (mesh, bounds);

            int count = Preferences.ChunksPerBreak.Value;
            bool minecraft = Preferences.ChunkStyleMinecraft.Value;
            string key = MakeKey(mesh, count, minecraft);
            if (Cache.TryGetValue(key, out var existing)) return existing;

            ChunkData[] chunks;
            float randomness = Preferences.SliceRandomness.Value;

            if (minecraft)
            {
                chunks = ShardClusterBuilder.Build(bounds, count, randomness);
                Preferences.Log($"Built {chunks.Length} shards (minecraft) for mesh {mesh.GetInstanceID()}.");
            }
            else
            {
                Mesh sliceable = mesh.isReadable ? mesh : null;
                bool fromReadback = false;
                if (sliceable == null && MeshReadback.TryMakeReadable(mesh, out var rb))
                {
                    sliceable = rb;
                    fromReadback = true;
                }

                if (sliceable != null &&
                    MeshSlicer.TrySlice(sliceable, count, randomness, out chunks))
                {
                    Preferences.Log($"Sliced mesh {mesh.GetInstanceID()} into {chunks.Length} ({(fromReadback ? "GPU readback" : "readable")}).");
                }
                else
                {
                    chunks = ShardClusterBuilder.Build(bounds, count, randomness);
                    Preferences.Log($"Mesh {mesh.GetInstanceID()} not sliceable; built {chunks.Length} shards.");
                }
            }

            Cache[key] = chunks;
            return chunks;
        }

        // Pre-warm every unique structure mesh present in the map, spread across frames.
        public static IEnumerator PreWarm()
        {
            var seen = new HashSet<int>();
            var structures = Object.FindObjectsOfType<Structure>();
            foreach (var s in structures)
            {
                var filter = s.GetComponentInChildren<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                int meshId = filter.sharedMesh.GetInstanceID();
                if (!seen.Add(meshId)) continue;
                Build(filter.sharedMesh, filter.sharedMesh.bounds);
                yield return null; // one mesh per frame — no load hitch
            }
            Preferences.Log($"Pre-warm complete: {seen.Count} structure meshes cached.");
        }

        // Rebuild geometry for every mesh we've seen, at the CURRENT settings, so a Style or
        // Slices-per-structure change is already cached before the next break (no hot-path lag).
        public static System.Collections.IEnumerator PrewarmCurrent()
        {
            // snapshot so Build() writing back into Seen can't disturb iteration
            var snapshot = new System.Collections.Generic.List<(Mesh mesh, Bounds bounds)>(Seen.Values);
            foreach (var entry in snapshot)
            {
                if (entry.mesh == null) continue;
                Build(entry.mesh, entry.bounds);
                yield return null; // spread across frames
            }
            Preferences.Log($"Pre-warmed {snapshot.Count} meshes at current settings.");
        }

        public static void Clear()
        {
            // new Mesh() allocates native memory the GC won't reclaim; destroy cached
            // meshes before dropping references so map transitions don't leak.
            foreach (var arr in Cache.Values)
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i].Mesh != null) UnityEngine.Object.Destroy(arr[i].Mesh);
            Cache.Clear();
            Seen.Clear();
        }
    }
}
