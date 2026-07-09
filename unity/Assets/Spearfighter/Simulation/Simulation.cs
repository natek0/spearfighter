using System.Collections.Generic;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// The fixed-tick, input-driven, engine-agnostic game simulation. This is the
    /// single most important architectural object in the project (WS0): movement,
    /// combat, projectiles and building all live here, decoupled from rendering.
    /// A human, a bot, or (later) the network all drive it through the SAME
    /// InputCommand[] contract, so server-authoritative PvP is addable without a
    /// rewrite.
    ///
    /// Nothing in this assembly references UnityEngine, wall-clock time, or
    /// UnityEngine.Random — determinism flows through the seeded Rng and the fixed
    /// TickDt. Advance with Tick(commands): commands[i] drives Players[i].
    /// </summary>
    public sealed class SimCore
    {
        public readonly SimConfig Config;
        public readonly CollisionWorld World = new CollisionWorld();
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<BuildState> Builds = new List<BuildState>();
        public SpearState[] Spears;

        public int TickCount { get; private set; }

        private readonly List<SimEvent> _events = new List<SimEvent>();
        public IReadOnlyList<SimEvent> Events => _events;

        private readonly List<Vector3> _spawnPoints = new List<Vector3>();
        private InputCommand[] _prev = System.Array.Empty<InputCommand>();
        private Rng _rng;
        private int _nextBuildId = 1;

        private const float TorsoOffset = 1.1f;   // hurtbox center above feet (matches prototype)
        private const float RespawnDelay = 2.0f;

        public SimCore(SimConfig config, uint seed = 12345)
        {
            Config = config ?? SimConfig.Default();
            _rng = new Rng(seed);
            Spears = new SpearState[Config.MaxSpears];
        }

        // ---- setup ----

        public PlayerState AddPlayer(Vector3 spawnFeet, float yaw = 0f)
        {
            var p = new PlayerState
            {
                Id = Players.Count,
                Feet = spawnFeet,
                Yaw = yaw,
                Health = Config.MaxHealth,
                BuildEnergy = Config.BuildMaxEnergy,
            };
            Players.Add(p);
            _spawnPoints.Add(spawnFeet);
            _prev = new InputCommand[Players.Count];
            return p;
        }

        public void AddStaticBox(Vector3 min, Vector3 max) => World.Add(Collider.Box(min, max));

        // ---- main step ----

        public void Tick(IReadOnlyList<InputCommand> commands)
        {
            _events.Clear();
            float dt = Config.TickDt;

            for (int i = 0; i < Players.Count; i++)
            {
                var cmd = i < commands.Count ? commands[i] : InputCommand.Empty;
                StepPlayer(Players[i], cmd, i < _prev.Length ? _prev[i] : InputCommand.Empty, dt);
                if (i < _prev.Length) _prev[i] = cmd;
            }

            StepSpears(dt);

            TickCount++;
        }

        private void StepPlayer(PlayerState p, InputCommand cmd, InputCommand prev, float dt)
        {
            if (!p.Alive)
            {
                // round-based respawn (WS3 P1)
                p.ChargeHeldTime -= dt; // reuse as respawn countdown while dead
                if (p.ChargeHeldTime <= 0f) Respawn(p);
                return;
            }

            // ----- look (reduced sensitivity once a charge is committed) -----
            bool committed = p.Phase == AttackPhase.Charging && p.ChargeHeldTime > Config.TapMaxSeconds;
            float sens = committed ? Config.AimSensMultiplier : 1f;
            p.Yaw -= cmd.LookYawDelta * sens;
            p.Pitch = SimMath.ClampPitch(p.Pitch - cmd.LookPitchDelta * sens);

            // ----- planar movement -----
            Vector3 fwd = SimMath.PlanarForward(p.Yaw);
            Vector3 right = SimMath.PlanarRight(p.Yaw);
            Vector3 move = fwd * cmd.Move.Y + right * cmd.Move.X;
            if (move.LengthSquared() > 1e-6f)
            {
                move = SimMath.NormalizeSafe(move) * (Config.MoveSpeed * dt);
                p.Feet.X += move.X;
                p.Feet.Z += move.Z;
            }

            // resolve against walls after horizontal move
            World.ResolveBody(ref p.Feet, Config.PlayerRadius, Config.EyeHeight);

            // ----- jump + gravity -----
            if (cmd.JumpHeld && !prev.JumpHeld && p.Grounded)
            {
                p.VelocityY = Config.JumpSpeed;
                p.Grounded = false;
            }
            p.VelocityY += Config.Gravity * dt;
            p.Feet.Y += p.VelocityY * dt;

            // ----- ground / ramp support -----
            float support = World.SupportHeight(p.Feet.X, p.Feet.Z, p.Feet.Y);
            if (p.Feet.Y <= support + 0.25f && p.VelocityY <= 0f)
            {
                p.Feet.Y = support;
                p.VelocityY = 0f;
                p.Grounded = true;
            }
            else
            {
                p.Grounded = false;
            }

            // ----- attack charge FSM (tap = jab, hold = charge & throw) -----
            StepAttack(p, cmd, prev, dt);

            // ----- building (Phase 1) -----
            StepBuilding(p, cmd, prev, dt);
        }

        private void StepAttack(PlayerState p, InputCommand cmd, InputCommand prev, float dt)
        {
            if (cmd.AttackHeld && !prev.AttackHeld)
            {
                p.Phase = AttackPhase.Charging;
                p.ChargeHeldTime = 0f;
                p.AttackDragAccum = 0f;
                Emit(SimEventType.ChargeStart, p.Id);
            }

            if (p.Phase == AttackPhase.Charging)
            {
                if (cmd.AttackHeld)
                {
                    p.ChargeHeldTime += dt;
                    p.AttackDragAccum += cmd.AttackDragPixels;
                }
                else
                {
                    // release: decide jab vs throw
                    float held = p.ChargeHeldTime;
                    bool jab = held < Config.TapMaxSeconds && p.AttackDragAccum < Config.TapMaxDrag;
                    if (jab) DoJab(p);
                    else DoThrow(p, ChargePower(held));
                    p.Phase = AttackPhase.Idle;
                }
            }
        }

        public float ChargePower(float held)
            => SimMath.Clamp01((held - Config.TapMaxSeconds) / (Config.ChargeFullSeconds - Config.TapMaxSeconds));

        private void DoJab(PlayerState p)
        {
            Vector3 eye = p.EyePosition(Config.EyeHeight);
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            float cosHalf = System.MathF.Cos(Config.JabHalfAngleDeg * System.MathF.PI / 180f);
            Emit(SimEventType.Jab, p.Id, position: eye);

            for (int i = 0; i < Players.Count; i++)
            {
                var t = Players[i];
                if (i == p.Id || !t.Alive) continue;
                Vector3 to = TorsoCenter(t) - eye;
                float dist = to.Length();
                if (dist <= Config.JabRange && Vector3.Dot(dir, SimMath.NormalizeSafe(to)) >= cosHalf)
                    ApplyDamage(p, t, Config.JabDamage, HitKind.Jab, TorsoCenter(t));
            }
        }

        private void DoThrow(PlayerState p, float power)
        {
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            float speed = Config.ThrowSpeedMin + (Config.ThrowSpeedMax - Config.ThrowSpeedMin) * power;
            Vector3 origin = p.EyePosition(Config.EyeHeight)
                             + dir * Config.SpawnForwardOffset
                             + new Vector3(0f, Config.SpawnVerticalOffset, 0f);
            SpawnSpear(p.Id, origin, dir * speed);
            Emit(SimEventType.SpearThrown, p.Id, position: origin, amount: power);
        }

        // ---- projectiles ----

        private void SpawnSpear(int ownerId, Vector3 pos, Vector3 vel)
        {
            int slot = -1;
            float oldest = -1f;
            for (int i = 0; i < Spears.Length; i++)
            {
                if (!Spears[i].Active) { slot = i; break; }
                if (Spears[i].Life > oldest) { oldest = Spears[i].Life; slot = i; }
            }
            Spears[slot] = new SpearState
            {
                Active = true, Stuck = false, OwnerId = ownerId,
                Position = pos, Velocity = vel, Life = 0f,
            };
        }

        private void StepSpears(float dt)
        {
            for (int i = 0; i < Spears.Length; i++)
            {
                if (!Spears[i].Active) continue;
                Spears[i].Life += dt;
                if (Spears[i].Life > Config.SpearLifeSeconds) { Spears[i].Active = false; continue; }
                if (Spears[i].Stuck) continue;

                Spears[i].Velocity.Y += Config.SpearGravity * dt;
                Spears[i].Position += Spears[i].Velocity * dt;
                Vector3 sp = Spears[i].Position;

                // hit a player?
                bool consumed = false;
                for (int j = 0; j < Players.Count; j++)
                {
                    var t = Players[j];
                    if (j == Spears[i].OwnerId || !t.Alive) continue;
                    if ((sp - TorsoCenter(t)).Length() < Config.EnemyHurtRadius)
                    {
                        ApplyDamage(Players[Spears[i].OwnerId], t, Config.SpearDamage, HitKind.Throw, sp);
                        Spears[i].Active = false;
                        consumed = true;
                        break;
                    }
                }
                if (consumed) continue;

                // stick into ground / build / static (projectile-miss = stick, no destruction)
                if (sp.Y <= World.GroundHeight + 0.05f || World.PointInSolid(sp))
                {
                    Spears[i].Stuck = true;
                    Spears[i].Velocity = Vector3.Zero;
                    Emit(SimEventType.SpearStuck, Spears[i].OwnerId, position: sp);
                }
            }
        }

        // ---- building (Phase 1) ----

        private void StepBuilding(PlayerState p, InputCommand cmd, InputCommand prev, float dt)
        {
            if (cmd.RotateBuildHeld && !prev.RotateBuildHeld)
                p.BuildRotationSteps = (p.BuildRotationSteps + 1) & 3;

            if (cmd.BuildHeld && !prev.BuildHeld)
                TryPlaceBuild(p);

            if (p.BuildEnergy < Config.BuildMaxEnergy)
                p.BuildEnergy = System.MathF.Min(Config.BuildMaxEnergy,
                    p.BuildEnergy + Config.BuildEnergyRegenPerSec * dt);
        }

        /// <summary>Compute the ghost/ramp footprint for a builder without placing it (used by the ghost preview too).</summary>
        public bool TryGetBuildPlacement(PlayerState p, out Vector3 min, out Vector3 max, out int axis)
        {
            min = max = Vector3.Zero; axis = 1;
            Vector3 eye = p.EyePosition(Config.EyeHeight);
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            if (!World.AimGroundPoint(eye, dir, World.GroundHeight, out Vector3 ground)) return false;

            // clamp within reach on the ground plane
            Vector3 flat = new Vector3(ground.X - p.Feet.X, 0f, ground.Z - p.Feet.Z);
            float d = flat.Length();
            if (d > Config.BuildReach)
                ground = new Vector3(p.Feet.X, World.GroundHeight, p.Feet.Z) + flat * (Config.BuildReach / d);

            // grid snap
            float g = Config.BuildGridSize;
            float cx = System.MathF.Round(ground.X / g) * g;
            float cz = System.MathF.Round(ground.Z / g) * g;

            // Default the ramp to rise AWAY from the player (low edge nearest you) so
            // you can walk straight up it; RotateBuild offsets this in 90-degree steps.
            Vector3 f = SimMath.PlanarForward(p.Yaw);
            int baseSteps;
            if (System.MathF.Abs(f.Z) >= System.MathF.Abs(f.X)) baseSteps = f.Z >= 0f ? 0 : 2;
            else baseSteps = f.X >= 0f ? 1 : 3;
            int steps = (baseSteps + p.BuildRotationSteps) & 3;

            bool swapped = (steps & 1) == 1;
            float halfX = (swapped ? Config.RampLength : Config.RampWidth) * 0.5f;
            float halfZ = (swapped ? Config.RampWidth : Config.RampLength) * 0.5f;
            min = new Vector3(cx - halfX, World.GroundHeight, cz - halfZ);
            max = new Vector3(cx + halfX, World.GroundHeight + Config.RampHeight, cz + halfZ);
            axis = RotationToAxis(steps);
            return true;
        }

        private static int RotationToAxis(int steps)
        {
            // 0 -> rises +Z, 1 -> +X, 2 -> -Z, 3 -> -X
            switch (steps & 3) { case 0: return 1; case 1: return 0; case 2: return 3; default: return 2; }
        }

        public bool CanPlaceBuild(PlayerState p, Vector3 min, Vector3 max)
        {
            if (p.BuildEnergy < Config.BuildCostPerPlace) return false;
            // don't bury a player inside the new build
            for (int i = 0; i < Players.Count; i++)
            {
                var q = Players[i];
                if (!q.Alive) continue;
                if (q.Feet.X >= min.X && q.Feet.X <= max.X && q.Feet.Z >= min.Z && q.Feet.Z <= max.Z
                    && q.Feet.Y < max.Y - 0.1f)
                    return false;
            }
            return true;
        }

        private void TryPlaceBuild(PlayerState p)
        {
            if (!TryGetBuildPlacement(p, out var min, out var max, out var axis)) return;
            if (!CanPlaceBuild(p, min, max)) return;

            int id = _nextBuildId++;
            World.Add(Collider.Ramp(min, max, axis, id));
            Builds.Add(new BuildState { Id = id, OwnerId = p.Id, Min = min, Max = max, RampAxis = axis });
            p.BuildEnergy -= Config.BuildCostPerPlace;
            Emit(SimEventType.BuildPlaced, p.Id, position: (min + max) * 0.5f);

            EnforceBuildCap(p.Id);
        }

        private void EnforceBuildCap(int ownerId)
        {
            int count = 0;
            for (int i = 0; i < Builds.Count; i++) if (Builds[i].OwnerId == ownerId) count++;
            while (count > Config.MaxSimultaneousBuilds)
            {
                // oldest for this owner = lowest index with matching owner (append order)
                for (int i = 0; i < Builds.Count; i++)
                {
                    if (Builds[i].OwnerId == ownerId)
                    {
                        int evictId = Builds[i].Id;
                        Vector3 c = (Builds[i].Min + Builds[i].Max) * 0.5f;
                        World.RemoveByBuildId(evictId);
                        Builds.RemoveAt(i);
                        Emit(SimEventType.BuildEvicted, ownerId, position: c);
                        break;
                    }
                }
                count--;
            }
        }

        // ---- damage / death ----

        private void ApplyDamage(PlayerState attacker, PlayerState victim, float dmg, HitKind kind, Vector3 at)
        {
            victim.Health -= dmg;
            bool lethal = victim.Health <= 0f;
            _events.Add(new SimEvent
            {
                Type = SimEventType.Hit, ActorId = attacker.Id, TargetId = victim.Id,
                Position = at, HitKind = kind, Amount = dmg, Lethal = lethal,
            });
            if (lethal)
            {
                victim.Health = 0f;
                victim.Phase = AttackPhase.Idle;
                victim.ChargeHeldTime = RespawnDelay; // reused as respawn countdown while dead
                _events.Add(new SimEvent { Type = SimEventType.Death, ActorId = attacker.Id, TargetId = victim.Id, Position = at });
            }
        }

        private void Respawn(PlayerState p)
        {
            p.Feet = _spawnPoints[p.Id];
            p.VelocityY = 0f;
            p.Health = Config.MaxHealth;
            p.Phase = AttackPhase.Idle;
            p.ChargeHeldTime = 0f;
            p.BuildEnergy = Config.BuildMaxEnergy;
            Emit(SimEventType.Respawn, p.Id, position: p.Feet);
        }

        // ---- helpers ----

        private Vector3 TorsoCenter(PlayerState p) => new Vector3(p.Feet.X, p.Feet.Y + TorsoOffset, p.Feet.Z);

        private void Emit(SimEventType type, int actorId, int targetId = -1, Vector3 position = default, float amount = 0f)
            => _events.Add(new SimEvent { Type = type, ActorId = actorId, TargetId = targetId, Position = position, Amount = amount });

        /// <summary>For the render layer's dotted arc: the live throw preview for a committed charge.</summary>
        public bool TryGetChargePreview(PlayerState p, out Vector3 origin, out Vector3 velocity, out float power)
        {
            origin = velocity = Vector3.Zero; power = 0f;
            if (p.Phase != AttackPhase.Charging || p.ChargeHeldTime <= Config.TapMaxSeconds) return false;
            power = ChargePower(p.ChargeHeldTime);
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            float speed = Config.ThrowSpeedMin + (Config.ThrowSpeedMax - Config.ThrowSpeedMin) * power;
            origin = p.EyePosition(Config.EyeHeight) + dir * Config.SpawnForwardOffset + new Vector3(0f, Config.SpawnVerticalOffset, 0f);
            velocity = dir * speed;
            return true;
        }

        public float NextRandom() => _rng.NextFloat();
    }
}
