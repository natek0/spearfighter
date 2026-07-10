using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Builds a mesh for a set of voxel cells (a placed build or the ghost preview).
    /// Only faces that border EMPTY space are emitted — a face shared with an
    /// adjacent solid cell is skipped. That removes the internal coincident quads
    /// (which z-fight and waste vertices) so a build renders as a clean shell. A
    /// greedy-mesh + bevel pass for the "enhanced Minecraft" look is later art.
    /// Collision is the same cells, so what you see is what you collide with.
    /// </summary>
    public static class MeshFactory
    {
        private static readonly Vector3[] CubeVerts =
        {
            new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1), // bottom
            new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(0,1,1), // top
        };

        // Six faces: the four corner indices (CCW, outward normal) + the neighbour
        // cell offset. A face is drawn only when that neighbour is empty.
        private struct Face { public int a, b, c, d, nx, ny, nz; }
        private static readonly Face[] Faces =
        {
            new Face { a=4, b=5, c=6, d=7, nx=0,  ny=1,  nz=0  }, // +Y top
            new Face { a=0, b=3, c=2, d=1, nx=0,  ny=-1, nz=0  }, // -Y bottom
            new Face { a=0, b=1, c=5, d=4, nx=0,  ny=0,  nz=-1 }, // -Z
            new Face { a=1, b=2, c=6, d=5, nx=1,  ny=0,  nz=0  }, // +X
            new Face { a=2, b=3, c=7, d=6, nx=0,  ny=0,  nz=1  }, // +Z
            new Face { a=3, b=0, c=4, d=7, nx=-1, ny=0,  nz=0  }, // -X
        };

        public static Mesh BuildVoxels(IReadOnlyList<Cell> cells, float cellSize)
        {
            // membership set for neighbour tests (interior-face culling)
            var solid = new HashSet<long>();
            for (int i = 0; i < cells.Count; i++)
                solid.Add(Key(cells[i].X, cells[i].Y, cells[i].Z));

            var verts = new List<Vector3>(cells.Count * 8);
            var tris = new List<int>(cells.Count * 18);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                var origin = new Vector3(c.X * cellSize, c.Y * cellSize, c.Z * cellSize);
                for (int f = 0; f < Faces.Length; f++)
                {
                    var face = Faces[f];
                    if (solid.Contains(Key(c.X + face.nx, c.Y + face.ny, c.Z + face.nz)))
                        continue; // shared with a solid neighbour — don't draw it
                    int b = verts.Count;
                    verts.Add(origin + CubeVerts[face.a] * cellSize);
                    verts.Add(origin + CubeVerts[face.b] * cellSize);
                    verts.Add(origin + CubeVerts[face.c] * cellSize);
                    verts.Add(origin + CubeVerts[face.d] * cellSize);
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var mesh = new Mesh { name = "VoxelBuild" };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Same packing scheme as VoxelWorld: unique per cell over a large range.
        private static long Key(int x, int y, int z)
            => ((long)(x + 32768) << 34) | ((long)(y + 32768) << 17) | (long)(z + 32768);
    }
}
