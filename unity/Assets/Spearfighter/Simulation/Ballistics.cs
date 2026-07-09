using System;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// Shared ballistic math for the arced spear. Two consumers use the SAME code:
    /// the trajectory-preview arc (WS3 aiming aid) and the bot's aim solve (WS5).
    /// Keeping them identical is what makes the preview honest and the bot fair.
    /// </summary>
    public static class Ballistics
    {
        /// <summary>
        /// Integrate a projectile forward and fill <paramref name="points"/> with the
        /// path, stopping at the ground plane or the first solid it enters. Returns the
        /// number of points written. This is the exact model used to render the dotted
        /// preview, so preview == real flight (same gravity, same collision test).
        /// </summary>
        public static int PredictPath(
            Vector3 origin, Vector3 velocity, float gravity, float stepDt,
            VoxelWorld world, float groundY, Vector3[] points)
        {
            Vector3 p = origin;
            Vector3 v = velocity;
            int n = 0;
            for (int i = 0; i < points.Length; i++)
            {
                points[n++] = p;
                v.Y += gravity * stepDt;
                p += v * stepDt;
                if (p.Y <= groundY + 0.05f) break;
                if (world != null && world.PointInSolid(p)) break;
            }
            return n;
        }

        /// <summary>
        /// Solve the launch pitch (radians) to hit a target at the given horizontal
        /// distance and height difference with a fixed launch speed under gravity g.
        /// Chooses the lower (flatter, faster-arriving) of the two ballistic arcs.
        /// Returns false if the target is out of range for that speed.
        /// g is passed as a POSITIVE magnitude.
        /// </summary>
        public static bool SolveLaunchPitch(
            float horizontalDist, float heightDelta, float speed, float g, out float pitch)
        {
            pitch = 0f;
            if (speed <= 1e-3f || horizontalDist <= 1e-3f) return false;

            float s2 = speed * speed;
            float x = horizontalDist;
            // discriminant of the standard projectile-to-target equation
            float disc = s2 * s2 - g * (g * x * x + 2f * heightDelta * s2);
            if (disc < 0f) return false; // unreachable at this speed

            float root = MathF.Sqrt(disc);
            // Two solutions; take the lower arc (smaller tan => flatter).
            float tanLow = (s2 - root) / (g * x);
            pitch = MathF.Atan(tanLow);
            return true;
        }
    }
}
