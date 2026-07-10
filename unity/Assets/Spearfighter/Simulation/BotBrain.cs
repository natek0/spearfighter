using System;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// The MVP opponent (WS5). CRITICAL PROPERTY: the bot produces a normal
    /// InputCommand — the exact struct a human thumb produces — which the
    /// Simulation consumes identically. So "play vs bots" validates the real
    /// simulation/netcode path, not a throwaway. Swap this for a network peer later
    /// and nothing downstream changes.
    ///
    /// Behaviour (Phase 1 "real opponent"):
    ///  - Spacing + ballistic aim: hold spear range, turn to face, solve the launch
    ///    pitch, charge+throw, jab up close.
    ///  - Fire discipline: only commit a throw when the solved arc actually REACHES
    ///    the foe (clears cover, isn't eaten by a pillar). This lets it lob over the
    ///    cover wall but stops it flinging spears into walls.
    ///  - Dodging: reads spears in flight and side-steps / jumps out of an incoming
    ///    line before impact.
    ///  - Tactical building: when exposed (the foe has a clean line to it) it drops a
    ///    cover build between itself and the foe, on a cooldown.
    ///  - Unstuck routing: naive straight-line approach is kept, but when it stops
    ///    making progress (a wall/pillar/build in the way) it strafes and jumps to get
    ///    around/over. Real navmesh-over-dynamic-geometry is still the deferred WS5 ⚠.
    /// </summary>
    public sealed class BotBrain
    {
        private const float TorsoOffset = 1.1f;   // hurtbox center above feet (matches SimCore)

        private Rng _rng;
        private float _decisionCd;
        private float _chargeTimer;
        private bool _charging;
        private int _strafeDir = 1;
        private float _strafeCd;

        // tactical build FSM
        private bool _buildMode;
        private bool _buildHolding;
        private float _buildHoldTimer;
        private float _buildCd;

        // unstuck detection
        private Vector3 _lastFeet;
        private bool _hasLast;
        private bool _wantedMove;
        private float _stuckTimer;

        // threat memory (drives reactive cover building)
        private float _lastHealth = -1f;
        private float _threatTimer;

        private readonly Vector3[] _arcPts = new Vector3[256];

        public BotBrain(uint seed) { _rng = new Rng(seed); }

        public InputCommand Think(SimCore sim, PlayerState me, PlayerState foe, float dt)
        {
            var cmd = InputCommand.Empty;
            if (me == null || foe == null || !me.Alive || !foe.Alive)
            {
                _charging = false; _buildMode = false; _hasLast = false; _stuckTimer = 0f;
                _lastHealth = -1f; _threatTimer = 0f;
                return cmd;
            }
            var cfg = sim.Config;

            // threat memory: remember taking damage so cover-building is reactive, not
            // a reflex that walls the bot into its own firing lane when nothing threatens it.
            if (_lastHealth >= 0f && me.Health < _lastHealth - 0.01f)
                _threatTimer = cfg.BotThreatMemorySeconds;
            else if (_threatTimer > 0f)
                _threatTimer -= dt;
            _lastHealth = me.Health;

            Vector3 toFoe = new Vector3(foe.Feet.X - me.Feet.X, 0f, foe.Feet.Z - me.Feet.Z);
            float dist = toFoe.Length();
            if (dist < 1e-3f) dist = 1e-3f;

            Vector3 myTorso = TorsoOf(me);
            Vector3 foeTorso = TorsoOf(foe);
            Vector3 foeEye = foe.EyePosition(cfg.EyeHeight);

            // ---- ballistic aim solve toward the foe ----
            float desiredYaw = MathF.Atan2(-toFoe.X, -toFoe.Z); // matches PlanarForward convention
            float power = sim.ChargePower(cfg.BotChargeSeconds);
            float speed = cfg.ThrowSpeedMin + (cfg.ThrowSpeedMax - cfg.ThrowSpeedMin) * power;
            float heightDelta = (foe.Feet.Y + TorsoOffset) - (me.Feet.Y + cfg.EyeHeight);
            if (!Ballistics.SolveLaunchPitch(dist, heightDelta, speed, MathF.Abs(cfg.SpearGravity), out float desiredPitch))
                desiredPitch = 0.5f; // out of range: lob high

            // ---- situational reads ----
            UpdateStuck(cfg, me);
            bool exposed = !sim.World.SegmentBlocked(foeEye, myTorso);       // foe has a clean line to me
            bool arcClear = ArcReachesFoe(sim, me, foe, desiredYaw, desiredPitch, speed);
            bool dodging = TryPlanDodge(sim, me, out Vector2 dodgeMove);

            // ---- decide aim target: building looks down at the ground to place cover ----
            float aimYaw = desiredYaw, aimPitch = desiredPitch;
            _buildCd -= dt;
            if (_charging || dodging)
            {
                _buildMode = false; // never build mid-throw or mid-dodge
            }
            else if (!_buildMode && _buildCd <= 0f && exposed && _threatTimer > 0f
                     && me.BuildEnergy >= cfg.BuildCostPerPlace && dist > cfg.JabRange + 1f)
            {
                _buildMode = true; _buildHolding = false; // under fire and exposed → drop cover
            }
            bool building = _buildMode;
            if (building) aimPitch = cfg.BotBuildAimPitch;

            AimTowards(ref cmd, me, aimYaw, aimPitch, cfg.BotTurnRateRadPerSec * dt);
            bool yawAimed = MathF.Abs(ShortestAngle(aimYaw - me.Yaw)) < 0.15f;

            // ---- attack / build action ----
            if (_charging)
            {
                cmd.AttackHeld = true;
                _chargeTimer -= dt;
                if (_chargeTimer <= 0f)
                {
                    _charging = false;
                    cmd.AttackHeld = false; // release -> throw
                    _decisionCd = cfg.BotReactionSeconds + _rng.Range(0.3f, 1.0f);
                }
            }
            else if (building)
            {
                bool ready = yawAimed && MathF.Abs(aimPitch - me.Pitch) < 0.25f;
                if (!_buildHolding)
                {
                    if (ready) { _buildHolding = true; _buildHoldTimer = 0f; cmd.BuildHeld = true; }
                }
                else
                {
                    _buildHoldTimer += dt;
                    if (_buildHoldTimer < cfg.TickDt * 3f) cmd.BuildHeld = true; // hold preview a few ticks
                    else { cmd.BuildHeld = false; _buildMode = false; _buildCd = cfg.BotBuildCooldownSeconds; } // release = place
                }
            }
            else if (!dodging)
            {
                _decisionCd -= dt;
                if (_decisionCd <= 0f && yawAimed)
                {
                    if (dist <= cfg.JabRange)
                    {
                        cmd.AttackHeld = true; // single tick -> jab
                        _decisionCd = cfg.BotReactionSeconds + _rng.Range(0.2f, 0.6f);
                    }
                    else if (arcClear)
                    {
                        _charging = true;
                        _chargeTimer = cfg.BotChargeSeconds;
                        cmd.AttackHeld = true; // begin charge (only when the shot can land)
                    }
                    // else: no clean shot — hold fire and reposition (movement below)
                }
            }

            // ---- movement ----
            if (dodging)
            {
                cmd.Move = dodgeMove;
                if (me.Grounded) cmd.JumpHeld = true;
            }
            else if (building)
            {
                cmd.Move = Vector2.Zero; // plant to place cleanly
            }
            else
            {
                cmd.Move = PlanSpacing(cfg, dist, dt);
                // want to shoot but the lane is blocked: sidestep to open an angle
                if (!arcClear && dist > cfg.JabRange) cmd.Move = new Vector2(Strafe(dt), 0f);
            }

            // ---- unstuck: made no progress while trying to move → route around / over ----
            if (!dodging && _stuckTimer >= cfg.BotStuckSeconds)
            {
                cmd.Move = new Vector2(Strafe(dt), 0.35f); // sidestep with a little forward bias
                if (me.Grounded) cmd.JumpHeld = true;
            }

            _lastFeet = me.Feet;
            _wantedMove = cmd.Move.LengthSquared() > 1e-4f;
            _hasLast = true;
            return cmd;
        }

        // ---- spacing ----

        private Vector2 PlanSpacing(SimConfig cfg, float dist, float dt)
        {
            if (dist > cfg.BotPreferredRange + cfg.BotRangeTolerance) return new Vector2(0f, 1f);
            if (dist < cfg.BotPreferredRange - cfg.BotRangeTolerance) return new Vector2(0f, -1f);
            return new Vector2(Strafe(dt), 0f);
        }

        private float Strafe(float dt)
        {
            _strafeCd -= dt;
            if (_strafeCd <= 0f) { _strafeDir = -_strafeDir; _strafeCd = _rng.Range(0.8f, 1.8f); }
            return _strafeDir;
        }

        // ---- fire discipline: will this throw actually reach the foe? ----

        /// <summary>
        /// Integrate the would-be spear with the SAME ballistic model the real throw
        /// and the preview arc use, and check the path ends near the foe's torso. If a
        /// wall or pillar stops it short, the shot is "not clear" and the bot holds
        /// fire. An over-the-wall lob still passes because its arc clears the wall.
        /// </summary>
        private bool ArcReachesFoe(SimCore sim, PlayerState me, PlayerState foe,
            float yaw, float pitch, float speed)
        {
            var cfg = sim.Config;
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(yaw, pitch));
            Vector3 origin = me.EyePosition(cfg.EyeHeight);
            int n = Ballistics.PredictPath(origin, dir * speed, cfg.SpearGravity, cfg.TickDt,
                sim.World, sim.World.GroundHeight, _arcPts);
            if (n == 0) return false;
            // Closest approach of the (collision-free) predicted arc to the foe's torso.
            // PredictPath already truncates at the first solid, so a wall/pillar between
            // us cuts the path short and the closest approach stays large ⇒ "not clear".
            Vector3 torso = TorsoOf(foe);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = (_arcPts[i] - torso).Length();
                if (d < best) best = d;
            }
            return best <= cfg.EnemyHurtRadius * 1.4f;
        }

        // ---- dodging: read spears in flight, side-step the incoming line ----

        private bool TryPlanDodge(SimCore sim, PlayerState me, out Vector2 localMove)
        {
            localMove = Vector2.Zero;
            var cfg = sim.Config;
            Vector3 myTorso = TorsoOf(me);

            for (int i = 0; i < sim.Spears.Length; i++)
            {
                var s = sim.Spears[i];
                if (!s.Active || s.Stuck || s.OwnerId == me.Id) continue;

                Vector3 v = s.Velocity;
                float v2 = v.LengthSquared();
                if (v2 < 1e-3f) continue;

                Vector3 rel = myTorso - s.Position;
                float tClose = Vector3.Dot(rel, v) / v2;               // seconds to closest approach
                if (tClose <= 0f || tClose > cfg.BotDodgeTimeToImpact) continue;
                Vector3 closest = s.Position + v * tClose;
                if ((closest - myTorso).Length() > cfg.BotDodgeRadius) continue;

                // evade perpendicular to the spear's horizontal travel, toward the side
                // that opens the most distance from the incoming line.
                Vector3 flatV = new Vector3(v.X, 0f, v.Z);
                Vector3 perp = new Vector3(-flatV.Z, 0f, flatV.X);
                if (Vector3.Dot(perp, rel) < 0f) perp = -perp;
                localMove = WorldToLocalMove(me.Yaw, SimMath.NormalizeSafe(perp));
                return true;
            }
            return false;
        }

        // ---- unstuck bookkeeping ----

        private void UpdateStuck(SimConfig cfg, PlayerState me)
        {
            if (_hasLast && _wantedMove)
            {
                float dx = me.Feet.X - _lastFeet.X, dz = me.Feet.Z - _lastFeet.Z;
                float progressed = MathF.Sqrt(dx * dx + dz * dz);
                float expected = cfg.MoveSpeed * cfg.TickDt;
                if (progressed < expected * cfg.BotStuckSpeedFrac) _stuckTimer += cfg.TickDt;
                else _stuckTimer = 0f;
            }
        }

        // ---- helpers ----

        private static Vector3 TorsoOf(PlayerState p) => new Vector3(p.Feet.X, p.Feet.Y + TorsoOffset, p.Feet.Z);

        /// <summary>Steer yaw/pitch toward a target, clamped to a per-tick max turn.</summary>
        private static void AimTowards(ref InputCommand cmd, PlayerState me, float yaw, float pitch, float maxTurn)
        {
            float yawErr = ShortestAngle(yaw - me.Yaw);
            float pitchErr = pitch - me.Pitch;
            cmd.LookYawDelta = -Clamp(yawErr, -maxTurn, maxTurn);   // sim applies Yaw -= delta
            cmd.LookPitchDelta = -Clamp(pitchErr, -maxTurn, maxTurn);
        }

        /// <summary>Project a world-space planar direction into the bot's local (strafe, forward) move.</summary>
        private static Vector2 WorldToLocalMove(float yaw, Vector3 worldDir)
        {
            Vector3 fwd = SimMath.PlanarForward(yaw);
            Vector3 right = SimMath.PlanarRight(yaw);
            return new Vector2(Vector3.Dot(worldDir, right), Vector3.Dot(worldDir, fwd));
        }

        private static float ShortestAngle(float a)
        {
            while (a > MathF.PI) a -= 2f * MathF.PI;
            while (a < -MathF.PI) a += 2f * MathF.PI;
            return a;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
