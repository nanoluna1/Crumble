// GPU mesh readback — pull geometry off non-readable meshes via GraphicsBuffer.
// mesh.isReadable == false only blocks mesh.vertices/mesh.triangles CPU access;
// the data still lives in GPU buffers we can read back synchronously.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Main
{
    public static class MeshReadback
    {
        public static bool TryMakeReadable(UnityEngine.Mesh src, out UnityEngine.Mesh readable)
        {
            readable = null;
            GraphicsBuffer vbuf = null;
            GraphicsBuffer ibuf = null;
            try
            {
                if (src == null) return false;
                if (!src.HasVertexAttribute(VertexAttribute.Position)) return false;
                if (src.vertexBufferCount < 1) return false;

                // ── Vertices ─────────────────────────────────────────────────
                int stride    = src.GetVertexBufferStride(0);
                int posOffset = src.GetVertexAttributeOffset(VertexAttribute.Position);
                if (stride <= 0) return false;

                vbuf = src.GetVertexBuffer(0);
                if (vbuf == null) return false;

                int vcount = vbuf.count;
                if (vcount == 0) return false;

                byte[] vraw = ReadBytes(vbuf, vcount * stride);
                if (vraw == null) return false;

                Vector3[] positions = new Vector3[vcount];
                for (int i = 0; i < vcount; i++)
                {
                    int o = i * stride + posOffset;
                    positions[i] = new Vector3(
                        BitConverter.ToSingle(vraw, o),
                        BitConverter.ToSingle(vraw, o + 4),
                        BitConverter.ToSingle(vraw, o + 8));
                }

                // ── Indices ──────────────────────────────────────────────────
                ibuf = src.GetIndexBuffer();
                if (ibuf == null) return false;

                int icount = ibuf.count;
                if (icount < 3) return false;

                int[] tris = new int[icount];
                if (src.indexFormat == IndexFormat.UInt16)
                {
                    byte[] iraw = ReadBytes(ibuf, icount * 2);
                    if (iraw == null) return false;
                    for (int i = 0; i < icount; i++)
                    {
                        int idx = BitConverter.ToUInt16(iraw, i * 2);
                        if (idx >= vcount) return false; // corrupt readback
                        tris[i] = idx;
                    }
                }
                else
                {
                    byte[] iraw = ReadBytes(ibuf, icount * 4);
                    if (iraw == null) return false;
                    for (int i = 0; i < icount; i++)
                    {
                        uint idx = BitConverter.ToUInt32(iraw, i * 4);
                        if (idx >= (uint)vcount) return false; // corrupt readback
                        tris[i] = (int)idx;
                    }
                }

                // ── Build the readable mesh ──────────────────────────────────
                var m = new Mesh();
                if (vcount > 65535) m.indexFormat = IndexFormat.UInt32;
                m.vertices  = positions;
                m.triangles = tris;
                m.RecalculateNormals();
                m.RecalculateBounds();

                readable = m;
                return true;
            }
            catch
            {
                readable = null;
                return false;
            }
            finally
            {
                if (vbuf != null) vbuf.Dispose();
                if (ibuf != null) ibuf.Dispose();
            }
        }

        // Read `length` bytes from a GraphicsBuffer into a managed byte[].
        // Uses an Il2CppStructArray for GetData (IL2CPP marshalling), then copies
        // into a managed array so System.BitConverter can parse it.
        private static byte[] ReadBytes(GraphicsBuffer buf, int length)
        {
            if (length <= 0) return null;
            var il2cpp = new Il2CppStructArray<byte>(length);
            buf.GetData(il2cpp.Cast<Il2CppSystem.Array>());
            byte[] managed = new byte[length];
            for (int i = 0; i < length; i++)
                managed[i] = il2cpp[i];
            return managed;
        }
    }
}
