using UnityEngine;

namespace Main
{
    // One debris piece: its mesh (already convex, bake-ready) and where its origin sits
    // relative to the structure's local origin.
    public struct ChunkData
    {
        public Mesh Mesh;
        public Vector3 LocalOffset;

        public ChunkData(Mesh mesh, Vector3 localOffset)
        {
            Mesh = mesh;
            LocalOffset = localOffset;
        }
    }
}
