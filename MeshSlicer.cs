// Hull-slice routine via Google Antigravity CLI (Gemini 3.1 Pro High)
using System.Collections.Generic;
using UnityEngine;

namespace Main
{
    public static class MeshSlicer
    {
        // ── Internal types ────────────────────────────────────────────────────

        private struct Edge
        {
            public Vector3 a;
            public Vector3 b;
        }

        private struct Vec3Key
        {
            public float x, y, z;

            public Vec3Key(Vector3 v)
            {
                x = Mathf.Round(v.x * 1000f) / 1000f;
                y = Mathf.Round(v.y * 1000f) / 1000f;
                z = Mathf.Round(v.z * 1000f) / 1000f;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Vec3Key)) return false;
                Vec3Key other = (Vec3Key)obj;
                return x == other.x && y == other.y && z == other.z;
            }

            public override int GetHashCode()
            {
                return x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
            }
        }

        private class SimpleMesh
        {
            public List<Vector3> vertices;
            public List<int>     triangles;
            public Dictionary<Vec3Key, int> vertMap;

            public SimpleMesh()
            {
                vertices  = new List<Vector3>();
                triangles = new List<int>();
                vertMap   = new Dictionary<Vec3Key, int>();
            }

            // Construct from a Unity Mesh (caller guarantees isReadable)
            public SimpleMesh(UnityEngine.Mesh mesh)
            {
                vertices  = new List<Vector3>(mesh.vertices);
                triangles = new List<int>(mesh.triangles);
                vertMap   = new Dictionary<Vec3Key, int>();

                for (int i = 0; i < vertices.Count; i++)
                {
                    Vec3Key key = new Vec3Key(vertices[i]);
                    if (!vertMap.ContainsKey(key))
                        vertMap[key] = i;
                }
            }

            public int AddVertex(Vector3 v)
            {
                Vec3Key key = new Vec3Key(v);
                if (vertMap.TryGetValue(key, out int index))
                    return index;

                index = vertices.Count;
                vertices.Add(v);
                vertMap[key] = index;
                return index;
            }

            public void AddTriangle(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                int i0 = AddVertex(v0);
                int i1 = AddVertex(v1);
                int i2 = AddVertex(v2);

                if (i0 == i1 || i1 == i2 || i2 == i0) return; // degenerate

                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
            }
        }

        // ── Geometry helpers ──────────────────────────────────────────────────

        private static Bounds GetBounds(SimpleMesh m)
        {
            if (m.vertices.Count == 0) return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 min = m.vertices[0];
            Vector3 max = m.vertices[0];
            for (int i = 1; i < m.vertices.Count; i++)
            {
                min = Vector3.Min(min, m.vertices[i]);
                max = Vector3.Max(max, m.vertices[i]);
            }
            return new Bounds((min + max) * 0.5f, max - min);
        }

        private static float GetVolume(SimpleMesh m)
        {
            Bounds b = GetBounds(m);
            return b.size.x * b.size.y * b.size.z;
        }

        private static Vector3 Intersect(Vector3 a, Vector3 b, float da, float db)
        {
            float denom = da - db;
            float t = (denom == 0f) ? 0.5f : (da / denom);
            t = Mathf.Clamp01(t);
            return a + (b - a) * t;
        }

        private static void FillCap(SimpleMesh mesh, List<Edge> edges)
        {
            if (edges.Count == 0) return;

            Vector3 center = Vector3.zero;
            foreach (var e in edges)
            {
                center += e.a;
                center += e.b;
            }
            center /= (edges.Count * 2);

            foreach (var e in edges)
            {
                mesh.AddTriangle(e.a, e.b, center);
            }
        }

        // Split one SimpleMesh by a plane; returns false if either side is empty.
        private static bool Split(SimpleMesh input, Vector3 planePoint, Vector3 planeNormal,
                                   out SimpleMesh posMesh, out SimpleMesh negMesh)
        {
            posMesh = new SimpleMesh();
            negMesh = new SimpleMesh();

            List<Edge> posCapEdges = new List<Edge>();
            List<Edge> negCapEdges = new List<Edge>();

            var verts = input.vertices;
            var tris  = input.triangles;

            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 v0 = verts[tris[i]];
                Vector3 v1 = verts[tris[i + 1]];
                Vector3 v2 = verts[tris[i + 2]];

                float d0 = Vector3.Dot(v0 - planePoint, planeNormal);
                float d1 = Vector3.Dot(v1 - planePoint, planeNormal);
                float d2 = Vector3.Dot(v2 - planePoint, planeNormal);

                bool b0 = d0 >= 0f;
                bool b1 = d1 >= 0f;
                bool b2 = d2 >= 0f;

                if (b0 && b1 && b2)
                {
                    posMesh.AddTriangle(v0, v1, v2);
                }
                else if (!b0 && !b1 && !b2)
                {
                    negMesh.AddTriangle(v0, v1, v2);
                }
                else
                {
                    // Mixed — isolate the lone vertex
                    Vector3 isoV, vA, vB;
                    float   isoD, dA, dB;
                    bool    isoPos;

                    if (b0 != b1 && b0 != b2)
                    {
                        isoV = v0; vA = v1; vB = v2;
                        isoD = d0; dA = d1; dB = d2;
                        isoPos = b0;
                    }
                    else if (b1 != b0 && b1 != b2)
                    {
                        isoV = v1; vA = v2; vB = v0;
                        isoD = d1; dA = d2; dB = d0;
                        isoPos = b1;
                    }
                    else
                    {
                        isoV = v2; vA = v0; vB = v1;
                        isoD = d2; dA = d0; dB = d1;
                        isoPos = b2;
                    }

                    Vector3 i1 = Intersect(isoV, vA, isoD, dA);
                    Vector3 i2 = Intersect(isoV, vB, isoD, dB);

                    if (isoPos)
                    {
                        posMesh.AddTriangle(isoV, i1, i2);
                        negMesh.AddTriangle(vA, vB, i1);
                        negMesh.AddTriangle(vB, i2, i1);

                        posCapEdges.Add(new Edge { a = i2, b = i1 });
                        negCapEdges.Add(new Edge { a = i1, b = i2 });
                    }
                    else
                    {
                        negMesh.AddTriangle(isoV, i1, i2);
                        posMesh.AddTriangle(vA, vB, i1);
                        posMesh.AddTriangle(vB, i2, i1);

                        negCapEdges.Add(new Edge { a = i2, b = i1 });
                        posCapEdges.Add(new Edge { a = i1, b = i2 });
                    }
                }
            }

            if (posMesh.triangles.Count == 0 || negMesh.triangles.Count == 0)
                return false;

            FillCap(posMesh, posCapEdges);
            FillCap(negMesh, negCapEdges);

            return true;
        }

        // Convert a SimpleMesh to a ChunkData: centroid-relative verts + LocalOffset.
        private static ChunkData CreateChunkData(SimpleMesh m)
        {
            // Compute centroid (average of all vertex positions)
            Vector3 centroid = Vector3.zero;
            foreach (var v in m.vertices)
                centroid += v;
            if (m.vertices.Count > 0)
                centroid /= m.vertices.Count;

            // Build vertex array relative to centroid
            Vector3[] verts = new Vector3[m.vertices.Count];
            for (int i = 0; i < m.vertices.Count; i++)
                verts[i] = m.vertices[i] - centroid;

            Mesh mesh = new Mesh();
            mesh.vertices  = verts;
            mesh.triangles = m.triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return new ChunkData(mesh, centroid);
        }

        // ── Public entry point ────────────────────────────────────────────────

        public static bool TrySlice(Mesh mesh, int count, float randomness, out ChunkData[] chunks)
        {
            chunks = null;
            try
            {
                if (mesh == null || count < 2) return false;

                List<SimpleMesh> pieces = new List<SimpleMesh>();
                pieces.Add(new SimpleMesh(mesh));

                int retries    = 0;
                int maxRetries = count * 5;

                Vector3[] axes = new Vector3[]
                {
                    Vector3.up, Vector3.down,
                    Vector3.left, Vector3.right,
                    Vector3.forward, Vector3.back
                };

                while (pieces.Count < count && retries < maxRetries)
                {
                    // Pick the largest piece (by bounding-box volume) to slice next
                    int   biggestIdx = -1;
                    float maxVol     = -1f;

                    for (int i = 0; i < pieces.Count; i++)
                    {
                        float vol = GetVolume(pieces[i]);
                        if (vol > maxVol)
                        {
                            maxVol     = vol;
                            biggestIdx = i;
                        }
                    }

                    if (biggestIdx == -1) break;

                    SimpleMesh target = pieces[biggestIdx];

                    // Build a cut-plane normal: axis-aligned + randomness jitter
                    Vector3 baseNormal = axes[Random.Range(0, axes.Length)];
                    Vector3 randomDir  = Random.onUnitSphere;
                    Vector3 planeNormal = Vector3.Lerp(baseNormal, randomDir, randomness).normalized;

                    if (planeNormal.sqrMagnitude < 0.001f)
                        planeNormal = Vector3.up;

                    // Random point inside the piece's bounds as the plane origin
                    Bounds  bounds     = GetBounds(target);
                    Vector3 planePoint = new Vector3(
                        Random.Range(bounds.min.x, bounds.max.x),
                        Random.Range(bounds.min.y, bounds.max.y),
                        Random.Range(bounds.min.z, bounds.max.z));

                    if (Split(target, planePoint, planeNormal, out SimpleMesh pos, out SimpleMesh neg))
                    {
                        pieces.RemoveAt(biggestIdx);
                        pieces.Add(pos);
                        pieces.Add(neg);
                    }
                    else
                    {
                        retries++;
                    }
                }

                if (pieces.Count < 2) return false;

                chunks = new ChunkData[pieces.Count];
                for (int i = 0; i < pieces.Count; i++)
                    chunks[i] = CreateChunkData(pieces[i]);

                return true;
            }
            catch
            {
                chunks = null;
                return false;
            }
        }
    }
}
