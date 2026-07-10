using System.Collections.Generic;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// The simulation's authoritative collision world, voxel-based.
    ///
    /// All solid geometry is either a **solid cell** (unit box on the world voxel
    /// grid — this is what builds are made of) or a **static AABB** (coarse arena
    /// boxes). The player is an axis-aligned box moved with a swept, per-axis
    /// resolver plus auto step-up. Walk-up, jump-over and walls all emerge from this
    /// one rule set — no per-shape special cases — and it is deterministic and
    /// trivially server-reconstructable (a build is a bitmask of cells), which is
    /// what the netcode phase needs. No UnityEngine dependency.
    /// </summary>
    public sealed class VoxelWorld
    {
        public float CellSize = 0.5f;
        public float StepHeight = 0.55f;
        public float GroundHeight = 0f;

        private readonly HashSet<long> _solid = new HashSet<long>();
        private readonly Dictionary<int, List<long>> _buildKeys = new Dictionary<int, List<long>>();
        private readonly List<(Vector3 min, Vector3 max)> _statics = new List<(Vector3, Vector3)>();

        private const float Eps = 1e-4f;

        // ---- construction ----

        public void AddStaticBox(Vector3 min, Vector3 max) => _statics.Add((min, max));

        public void AddBuild(int buildId, Cell[] cells)
        {
            var keys = new List<long>(cells.Length);
            foreach (var c in cells)
            {
                long k = Key(c.X, c.Y, c.Z);
                _solid.Add(k);
                keys.Add(k);
            }
            _buildKeys[buildId] = keys;
        }

        public void RemoveBuild(int buildId)
        {
            if (!_buildKeys.TryGetValue(buildId, out var keys)) return;
            foreach (var k in keys) _solid.Remove(k);
            _buildKeys.Remove(buildId);
        }

        public void Clear()
        {
            _solid.Clear();
            _buildKeys.Clear();
            _statics.Clear();
        }

        // ---- queries ----

        public bool IsSolidCell(int x, int y, int z) => _solid.Contains(Key(x, y, z));

        /// <summary>Is a world point inside any solid cell or static box? (spear-stick, trajectory)</summary>
        public bool PointInSolid(Vector3 p)
        {
            if (_solid.Contains(Key(CellOf(p.X), CellOf(p.Y), CellOf(p.Z)))) return true;
            for (int i = 0; i < _statics.Count; i++)
            {
                var s = _statics[i];
                if (p.X >= s.min.X && p.X <= s.max.X && p.Y >= s.min.Y && p.Y <= s.max.Y &&
                    p.Z >= s.min.Z && p.Z <= s.max.Z) return true;
            }
            return false;
        }

        /// <summary>Does an axis-aligned world box overlap any solid cell or static box?
        /// Used to reject build placements that would bury a player.</summary>
        public bool BoxOverlapsSolid(Vector3 min, Vector3 max)
        {
            for (int i = 0; i < _statics.Count; i++)
            {
                var s = _statics[i];
                if (Overlap(min, max, s.min, s.max)) return true;
            }
            int x0 = CellOf(min.X), x1 = CellOf(max.X);
            int y0 = CellOf(min.Y), y1 = CellOf(max.Y);
            int z0 = CellOf(min.Z), z1 = CellOf(max.Z);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        if (_solid.Contains(Key(x, y, z)) && Overlap(min, max, CellMin(x, y, z), CellMax(x, y, z)))
                            return true;
            return false;
        }

        /// <summary>
        /// True if the straight segment a→b passes through any solid cell or static
        /// box. Coarse (~half-cell) point sampling — this is a cheap line-of-sight
        /// test for the bot's reasoning (can I see / can my arc reach the foe?), not
        /// a precise collision cast. The ground plane is NOT treated as blocking;
        /// callers pass eye/torso points that already sit above it.
        /// </summary>
        public bool SegmentBlocked(Vector3 a, Vector3 b)
        {
            Vector3 d = b - a;
            float len = d.Length();
            if (len < 1e-4f) return PointInSolid(a);
            int n = (int)(len / (CellSize * 0.5f)) + 1;
            // skip the endpoints: a may sit inside the shooter, b inside the target.
            for (int i = 1; i < n; i++)
                if (PointInSolid(a + d * ((float)i / n))) return true;
            return false;
        }

        /// <summary>Where a downward-ish aim ray meets the ground plane (y = baseY).</summary>
        public bool AimGroundPoint(Vector3 origin, Vector3 dir, float baseY, out Vector3 hit)
        {
            hit = origin;
            if (dir.Y >= -Eps) return false;
            float t = (baseY - origin.Y) / dir.Y;
            if (t <= 0f) return false;
            hit = origin + dir * t;
            return true;
        }

        // ---- character controller ----

        /// <summary>
        /// Move a player box (feet = bottom-center, half-width r, height h) by disp,
        /// resolving per axis with auto step-up. Reports grounded (stopped moving
        /// down), ceiling (stopped moving up), and how much a step-up raised the feet.
        /// </summary>
        public void MoveBody(ref Vector3 feet, float r, float h, Vector3 disp,
            out bool grounded, out bool ceiling, out float stepGain)
        {
            grounded = false; ceiling = false; stepGain = 0f;

            // --- horizontal (X then Z), with a step-up attempt if blocked ---
            Vector3 flat = feet;
            MoveAxis(ref flat, r, h, 0, disp.X);
            MoveAxis(ref flat, r, h, 2, disp.Z);

            bool blocked = System.MathF.Abs(flat.X - feet.X) < System.MathF.Abs(disp.X) - Eps
                        || System.MathF.Abs(flat.Z - feet.Z) < System.MathF.Abs(disp.Z) - Eps;

            Vector3 chosen = flat;
            if (blocked && StepHeight > 0f)
            {
                Vector3 st = feet;
                float up = MoveAxis(ref st, r, h, 1, StepHeight);   // rise up to StepHeight
                MoveAxis(ref st, r, h, 0, disp.X);                  // retry horizontal higher
                MoveAxis(ref st, r, h, 2, disp.Z);
                MoveAxis(ref st, r, h, 1, -up);                     // settle back down onto the step

                float flatProg = Sq(flat.X - feet.X) + Sq(flat.Z - feet.Z);
                float stepProg = Sq(st.X - feet.X) + Sq(st.Z - feet.Z);
                if (stepProg > flatProg + 1e-6f)
                {
                    chosen = st;
                    stepGain = st.Y - feet.Y;
                }
            }
            feet = chosen;

            // --- vertical (gravity / jump) ---
            float dy = MoveAxis(ref feet, r, h, 1, disp.Y);
            if (disp.Y < 0f && dy > disp.Y + Eps) grounded = true;   // hit floor
            if (disp.Y > 0f && dy < disp.Y - Eps) ceiling = true;    // hit ceiling
        }

        /// <summary>Move the box along one axis (0=X,1=Y,2=Z) by up to delta, clamped by
        /// solids and (for downward Y) the ground plane. Returns the actual movement.</summary>
        private float MoveAxis(ref Vector3 feet, float r, float h, int axis, float delta)
        {
            if (delta == 0f) return 0f;

            Vector3 min = new Vector3(feet.X - r, feet.Y, feet.Z - r);
            Vector3 max = new Vector3(feet.X + r, feet.Y + h, feet.Z + r);

            float allowed = delta;

            // ground plane floor
            if (axis == 1 && delta < 0f)
            {
                float toGround = GroundHeight - feet.Y;
                if (toGround > allowed) allowed = toGround;
            }

            // query region = box swept by delta along this axis
            Vector3 qmin = min, qmax = max;
            if (delta > 0f) qmax = Add(qmax, axis, delta); else qmin = Add(qmin, axis, delta);

            // static AABBs
            for (int i = 0; i < _statics.Count; i++)
                allowed = ClampAxis(min, max, _statics[i].min, _statics[i].max, axis, delta, allowed);

            // solid cells overlapping the query region
            int x0 = CellOf(qmin.X), x1 = CellOf(qmax.X);
            int y0 = CellOf(qmin.Y), y1 = CellOf(qmax.Y);
            int z0 = CellOf(qmin.Z), z1 = CellOf(qmax.Z);
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z <= z1; z++)
                        if (_solid.Contains(Key(x, y, z)))
                            allowed = ClampAxis(min, max, CellMin(x, y, z), CellMax(x, y, z), axis, delta, allowed);

            feet = Add(feet, axis, allowed);
            return allowed;
        }

        /// <summary>Clamp motion of box [bmin,bmax] along axis by solid [smin,smax], if
        /// they overlap in the two perpendicular axes.</summary>
        private static float ClampAxis(Vector3 bmin, Vector3 bmax, Vector3 smin, Vector3 smax,
            int axis, float delta, float allowed)
        {
            int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
            if (!(Get(bmax, a1) > Get(smin, a1) + Eps && Get(bmin, a1) < Get(smax, a1) - Eps)) return allowed;
            if (!(Get(bmax, a2) > Get(smin, a2) + Eps && Get(bmin, a2) < Get(smax, a2) - Eps)) return allowed;

            if (delta > 0f)
            {
                float boxFace = Get(bmax, axis), solidFace = Get(smin, axis);
                if (boxFace <= solidFace + Eps) { float gap = solidFace - boxFace; if (gap < allowed) allowed = gap; }
            }
            else
            {
                float boxFace = Get(bmin, axis), solidFace = Get(smax, axis);
                if (boxFace >= solidFace - Eps) { float gap = solidFace - boxFace; if (gap > allowed) allowed = gap; }
            }
            return allowed;
        }

        // ---- cell / vector helpers ----

        private int CellOf(float w) => (int)System.MathF.Floor(w / CellSize);
        private Vector3 CellMin(int x, int y, int z) => new Vector3(x * CellSize, y * CellSize, z * CellSize);
        private Vector3 CellMax(int x, int y, int z) => new Vector3((x + 1) * CellSize, (y + 1) * CellSize, (z + 1) * CellSize);

        private static bool Overlap(Vector3 amin, Vector3 amax, Vector3 bmin, Vector3 bmax)
            => amin.X < bmax.X && amax.X > bmin.X && amin.Y < bmax.Y && amax.Y > bmin.Y && amin.Z < bmax.Z && amax.Z > bmin.Z;

        private static float Sq(float v) => v * v;

        private static float Get(Vector3 v, int axis) => axis == 0 ? v.X : (axis == 1 ? v.Y : v.Z);
        private static Vector3 Add(Vector3 v, int axis, float d)
            => axis == 0 ? new Vector3(v.X + d, v.Y, v.Z) : (axis == 1 ? new Vector3(v.X, v.Y + d, v.Z) : new Vector3(v.X, v.Y, v.Z + d));

        // 17 bits per axis, offset so a range of about [-32768, 98303] cells packs uniquely.
        private static long Key(int x, int y, int z)
            => ((long)(x + 32768) << 34) | ((long)(y + 32768) << 17) | (long)(z + 32768);
    }
}
