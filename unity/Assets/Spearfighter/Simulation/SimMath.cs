using System;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// Small math helpers shared by the whole simulation. Kept engine-agnostic
    /// (System.Numerics only) so the sim compiles and runs under plain .NET for
    /// unit tests as well as inside Unity.
    ///
    /// Angle conventions match the validated Phase 0 web prototype:
    ///   yaw   : rotation about world +Y. yaw = 0 faces world -Z.
    ///   pitch : look up/down. pitch &gt; 0 looks up. Clamped to +/-1.45 rad.
    /// </summary>
    public static class SimMath
    {
        public const float MaxPitch = 1.45f;

        public static float Clamp(float v, float lo, float hi)
            => v < lo ? lo : (v > hi ? hi : v);

        public static float Clamp01(float v) => Clamp(v, 0f, 1f);

        public static float ClampPitch(float pitch) => Clamp(pitch, -MaxPitch, MaxPitch);

        /// <summary>Full look direction (includes pitch). Matches prototype forwardDir().</summary>
        public static Vector3 Forward(float yaw, float pitch)
        {
            float cp = MathF.Cos(pitch);
            return new Vector3(-cp * MathF.Sin(yaw), MathF.Sin(pitch), -cp * MathF.Cos(yaw));
        }

        /// <summary>Planar (yaw-only) forward used for movement. y == 0.</summary>
        public static Vector3 PlanarForward(float yaw)
            => new Vector3(-MathF.Sin(yaw), 0f, -MathF.Cos(yaw));

        /// <summary>Planar right vector = cross(planarForward, up).</summary>
        public static Vector3 PlanarRight(float yaw)
            => new Vector3(MathF.Cos(yaw), 0f, -MathF.Sin(yaw));

        public static Vector3 NormalizeSafe(Vector3 v)
        {
            float len = v.Length();
            return len > 1e-6f ? v / len : Vector3.Zero;
        }

        public static Vector2 ClampDisk(Vector2 v, float radius)
        {
            float len = v.Length();
            return len > radius && len > 1e-6f ? v * (radius / len) : v;
        }
    }
}
