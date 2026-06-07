using System.Collections.Generic;
using UnityEngine;

namespace Main
{
    public static class ShardClusterBuilder
    {
        // Build exactly `count` convex box shards filling `bounds` (in the structure's local space).
        // Algorithm: start with one box = bounds; repeatedly split the largest current box along its
        // longest axis at the midpoint (jittered by randomness) until there are exactly count boxes.
        public static ChunkData[] Build(Bounds bounds, int count, float randomness)
        {
            count = Mathf.Max(1, count);

            // Use a list of Bounds; each entry is one sub-box.
            var boxes = new List<Bounds>(count) { bounds };

            while (boxes.Count < count)
            {
                // Find the largest box by volume.
                int largestIdx = 0;
                float largestVol = Volume(boxes[0]);
                for (int i = 1; i < boxes.Count; i++)
                {
                    float v = Volume(boxes[i]);
                    if (v > largestVol) { largestVol = v; largestIdx = i; }
                }

                Bounds src = boxes[largestIdx];
                boxes.RemoveAt(largestIdx);

                // Split along the longest axis.
                Vector3 size = src.size;
                float jitter = 1f + Random.Range(-randomness, randomness) * 0.4f;
                Bounds a, b;
                if (size.x >= size.y && size.x >= size.z)
                {
                    float split = src.min.x + size.x * 0.5f * jitter;
                    split = Mathf.Clamp(split, src.min.x + size.x * 0.1f, src.max.x - size.x * 0.1f);
                    a = BoundsFromMinMax(src.min, new Vector3(split, src.max.y, src.max.z));
                    b = BoundsFromMinMax(new Vector3(split, src.min.y, src.min.z), src.max);
                }
                else if (size.y >= size.x && size.y >= size.z)
                {
                    float split = src.min.y + size.y * 0.5f * jitter;
                    split = Mathf.Clamp(split, src.min.y + size.y * 0.1f, src.max.y - size.y * 0.1f);
                    a = BoundsFromMinMax(src.min, new Vector3(src.max.x, split, src.max.z));
                    b = BoundsFromMinMax(new Vector3(src.min.x, split, src.min.z), src.max);
                }
                else
                {
                    float split = src.min.z + size.z * 0.5f * jitter;
                    split = Mathf.Clamp(split, src.min.z + size.z * 0.1f, src.max.z - size.z * 0.1f);
                    a = BoundsFromMinMax(src.min, new Vector3(src.max.x, src.max.y, split));
                    b = BoundsFromMinMax(new Vector3(src.min.x, src.min.y, split), src.max);
                }

                boxes.Add(a);
                boxes.Add(b);
            }

            // Emit one ChunkData per sub-box: a box mesh sized to that sub-box, offset at its center.
            var result = new ChunkData[boxes.Count];
            for (int i = 0; i < boxes.Count; i++)
            {
                Vector3 half = boxes[i].extents;
                Mesh m = Box(half);
                result[i] = new ChunkData(m, boxes[i].center);
            }
            return result;
        }

        private static float Volume(Bounds b)
        {
            Vector3 s = b.size;
            return s.x * s.y * s.z;
        }

        private static Bounds BoundsFromMinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(min, max);
            return b;
        }

        private static Mesh Box(Vector3 half)
        {
            // Unit cube scaled to +/- half. Uses managed arrays; Il2CppInterop converts on assignment.
            Vector3[] v =
            {
                new Vector3(-half.x,-half.y,-half.z), new Vector3( half.x,-half.y,-half.z),
                new Vector3( half.x, half.y,-half.z), new Vector3(-half.x, half.y,-half.z),
                new Vector3(-half.x,-half.y, half.z), new Vector3( half.x,-half.y, half.z),
                new Vector3( half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z),
            };
            int[] tri =
            {
                0,2,1, 0,3,2,  1,6,5, 1,2,6,  5,7,4, 5,6,7,
                4,3,0, 4,7,3,  3,6,2, 3,7,6,  4,1,5, 4,0,1,
            };
            var mesh = new Mesh();
            mesh.vertices = v;
            mesh.triangles = tri;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
