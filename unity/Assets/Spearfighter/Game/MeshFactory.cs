using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Builds the wedge mesh for a placed ramp. Top-face vertex heights are read
    /// from the SAME Collider.SurfaceHeight the simulation uses for walking and
    /// spear-sticking, so what you see is exactly what you stand on.
    /// </summary>
    public static class MeshFactory
    {
        public static Mesh BuildRamp(System.Numerics.Vector3 min, System.Numerics.Vector3 max, int axis)
        {
            var col = Spearfighter.Simulation.Collider.Ramp(min, max, axis, 0);
            // 4 footprint columns (xz), base at min.Y, top at the slope surface.
            float x0 = min.X, x1 = max.X, z0 = min.Z, z1 = max.Z, yb = min.Y;
            Vector3[] baseV =
            {
                new Vector3(x0, yb, z0), new Vector3(x1, yb, z0),
                new Vector3(x1, yb, z1), new Vector3(x0, yb, z1),
            };
            Vector3[] topV =
            {
                new Vector3(x0, col.SurfaceHeight(x0, z0), z0),
                new Vector3(x1, col.SurfaceHeight(x1, z0), z0),
                new Vector3(x1, col.SurfaceHeight(x1, z1), z1),
                new Vector3(x0, col.SurfaceHeight(x0, z1), z1),
            };

            var verts = new Vector3[8];
            for (int i = 0; i < 4; i++) { verts[i] = baseV[i]; verts[i + 4] = topV[i]; }

            // box topology; the degenerate low edge (base==top) is harmless
            int[] tris =
            {
                4,5,6, 4,6,7,       // top (sloped, walkable)
                0,2,1, 0,3,2,       // bottom
                0,1,5, 0,5,4,       // z0 face
                1,2,6, 1,6,5,       // x1 face
                2,3,7, 2,7,6,       // z1 face
                3,0,4, 3,4,7,       // x0 face
            };

            var mesh = new Mesh { name = "RampWall" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
