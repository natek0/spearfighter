using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Builds a mesh for a set of voxel cells (a placed build or the ghost preview).
    /// One cube per solid cell — blocky for now; a greedy-mesh + bevel pass for the
    /// "enhanced Minecraft" look is later art. Collision is the same cells, so what
    /// you see is what you collide with.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Vector3[] CubeVerts =
        {
            new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1), // bottom
            new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(0,1,1), // top
        };
        private static readonly int[] CubeTris =
        {
            4,5,6, 4,6,7,   // top
            0,2,1, 0,3,2,   // bottom
            0,1,5, 0,5,4,   // -z
            1,2,6, 1,6,5,   // +x
            2,3,7, 2,7,6,   // +z
            3,0,4, 3,4,7,   // -x
        };

        public static Mesh BuildVoxels(IReadOnlyList<Cell> cells, float cellSize)
        {
            var verts = new List<Vector3>(cells.Count * 8);
            var tris = new List<int>(cells.Count * 36);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                var origin = new Vector3(c.X * cellSize, c.Y * cellSize, c.Z * cellSize);
                int b = verts.Count;
                for (int v = 0; v < 8; v++) verts.Add(origin + CubeVerts[v] * cellSize);
                for (int t = 0; t < CubeTris.Length; t++) tris.Add(b + CubeTris[t]);
            }
            var mesh = new Mesh { name = "VoxelBuild" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
