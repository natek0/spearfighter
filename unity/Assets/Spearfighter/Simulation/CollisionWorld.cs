using System.Collections.Generic;
using System.Numerics;

namespace Spearfighter.Simulation
{
    public enum ColliderKind { Box, Ramp }

    /// <summary>
    /// A single solid in the simulation's own collision world. Value type so the
    /// world is a flat, cache-friendly list with no per-solid allocation.
    ///
    /// Box:  fully solid AABB [Min,Max]. Walkable top face at Max.Y.
    /// Ramp: walkable wedge. Footprint is the AABB [Min,Max] in XZ; the top surface
    ///       rises linearly from Min.Y at the low edge to Max.Y at the high edge,
    ///       along RampAxis. Solid only BELOW that surface, so you walk up it from
    ///       the low end and it blocks like a wall from the high end / from below.
    /// </summary>
    public struct Collider
    {
        public ColliderKind Kind;
        public Vector3 Min;
        public Vector3 Max;
        /// <summary>Direction the ramp rises: 0=+X, 1=+Z, 2=-X, 3=-Z. Ignored for Box.</summary>
        public int RampAxis;
        /// <summary>Stable id used for despawn/cap tracking. 0 = static geometry.</summary>
        public int OwnerBuildId;

        public static Collider Box(Vector3 min, Vector3 max) => new Collider
        {
            Kind = ColliderKind.Box, Min = min, Max = max, RampAxis = 0, OwnerBuildId = 0
        };

        public static Collider Ramp(Vector3 min, Vector3 max, int axis, int buildId) => new Collider
        {
            Kind = ColliderKind.Ramp, Min = min, Max = max, RampAxis = axis, OwnerBuildId = buildId
        };

        public bool InFootprintXZ(float x, float z)
            => x >= Min.X && x <= Max.X && z >= Min.Z && z <= Max.Z;

        /// <summary>Height of the walkable top surface at (x,z). Assumes InFootprintXZ.</summary>
        public float SurfaceHeight(float x, float z)
        {
            if (Kind == ColliderKind.Box) return Max.Y;
            float t;
            switch (RampAxis)
            {
                case 0: t = (x - Min.X) / Denom(Max.X - Min.X); break; // rises +X
                case 2: t = (Max.X - x) / Denom(Max.X - Min.X); break; // rises -X
                case 1: t = (z - Min.Z) / Denom(Max.Z - Min.Z); break; // rises +Z
                default: t = (Max.Z - z) / Denom(Max.Z - Min.Z); break; // rises -Z
            }
            t = SimMath.Clamp01(t);
            return Min.Y + (Max.Y - Min.Y) * t;
        }

        /// <summary>Is the point strictly inside the solid volume? (spear-stick / trajectory blocking)</summary>
        public bool ContainsPoint(Vector3 p)
        {
            if (p.X < Min.X || p.X > Max.X || p.Z < Min.Z || p.Z > Max.Z) return false;
            if (p.Y < Min.Y || p.Y > Max.Y) return false;
            if (Kind == ColliderKind.Box) return true;
            return p.Y <= SurfaceHeight(p.X, p.Z); // ramp: solid below the slope surface
        }

        private static float Denom(float d) => d < 1e-4f ? 1e-4f : d;
    }

    /// <summary>
    /// The simulation's authoritative collision world. Pure math over a list of
    /// Colliders plus an implicit ground plane at y = 0. No engine dependency.
    /// </summary>
    public sealed class CollisionWorld
    {
        public readonly List<Collider> Colliders = new List<Collider>();
        public float GroundHeight = 0f;

        // How far the feet may sit above a surface and still be snapped to it.
        private const float SnapDistance = 0.25f;
        // Small lip the player can step up onto (low edge of a ramp, etc.).
        private const float StepTolerance = 0.35f;

        public void Clear() => Colliders.Clear();
        public void Add(Collider c) => Colliders.Add(c);

        public void RemoveByBuildId(int buildId)
        {
            for (int i = Colliders.Count - 1; i >= 0; i--)
                if (Colliders[i].OwnerBuildId == buildId) Colliders.RemoveAt(i);
        }

        /// <summary>Highest walkable surface at (x,z) at or below feetY+StepTolerance. Never below ground.</summary>
        public float SupportHeight(float x, float z, float feetY)
        {
            float best = GroundHeight;
            for (int i = 0; i < Colliders.Count; i++)
            {
                var c = Colliders[i];
                if (!c.InFootprintXZ(x, z)) continue;
                float top = c.SurfaceHeight(x, z);
                if (top <= feetY + StepTolerance && top > best) best = top;
            }
            return best;
        }

        /// <summary>
        /// Resolve a capsule (feet position + radius + height) against the world.
        /// Mutates feet horizontally (wall push-out) and reports vertical support.
        /// Mirrors the prototype's circle-vs-AABB resolve, extended to ramps.
        /// </summary>
        public void ResolveBody(ref Vector3 feet, float radius, float bodyHeight)
        {
            float headY = feet.Y + bodyHeight;
            for (int i = 0; i < Colliders.Count; i++)
            {
                var c = Colliders[i];

                // Vertical span check: ignore solids entirely above the head or below the feet.
                if (feet.Y > c.Max.Y - StepTolerance || headY < c.Min.Y) continue;

                // For ramps, only block when the feet are below the slope surface here
                // (i.e. trying to pass through it from below / the high wall face).
                if (c.Kind == ColliderKind.Ramp && c.InFootprintXZ(feet.X, feet.Z))
                {
                    float surf = c.SurfaceHeight(feet.X, feet.Z);
                    if (feet.Y >= surf - StepTolerance) continue; // standing on / above the slope: walkable
                }

                float cx = Clamp(feet.X, c.Min.X, c.Max.X);
                float cz = Clamp(feet.Z, c.Min.Z, c.Max.Z);
                float dx = feet.X - cx;
                float dz = feet.Z - cz;
                float d2 = dx * dx + dz * dz;
                if (d2 < radius * radius)
                {
                    float d = d2 > 1e-8f ? MathFSqrt(d2) : 0.0001f;
                    // If the player center is inside the footprint (d2==0), push along +X as a fallback.
                    if (d < 1e-4f) { dx = 1f; dz = 0f; d = 1f; }
                    float push = (radius - d) / d;
                    feet.X += dx * push;
                    feet.Z += dz * push;
                }
            }
        }

        /// <summary>Is the point inside any solid (used for spear-stick and trajectory blocking).</summary>
        public bool PointInSolid(Vector3 p)
        {
            for (int i = 0; i < Colliders.Count; i++)
                if (Colliders[i].ContainsPoint(p)) return true;
            return false;
        }

        /// <summary>
        /// Where a downward-ish aim ray meets the ground plane (y = base). Used to
        /// choose a ramp placement point. Returns false if the ray points upward.
        /// </summary>
        public bool AimGroundPoint(Vector3 origin, Vector3 dir, float baseY, out Vector3 hit)
        {
            hit = origin;
            if (dir.Y >= -1e-4f) return false;
            float t = (baseY - origin.Y) / dir.Y;
            if (t <= 0f) return false;
            hit = origin + dir * t;
            return true;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        private static float MathFSqrt(float v) => System.MathF.Sqrt(v);
    }
}
