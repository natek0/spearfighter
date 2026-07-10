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
        public readonly VoxelWorld World = new VoxelWorld();
        public readonly List<PlayerState> Players = new List<PlayerState>();
        public readonly List<BuildState> Builds = new List<BuildState>();
        public SpearState[] Spears;

        public int TickCount { get; private set; }

        // ---- match state (stocks-based; see SimConfig.MatchLives) ----
        public bool MatchOver { get; private set; }
        public int WinnerId { get; private set; } = -1;
        public float MatchResetTimer { get; private set; }

        private readonly List<SimEvent> _events = new List<SimEvent>();
        public IReadOnlyList<SimEvent> Events => _events;

        private readonly List<Vector3> _spawnPoints = new List<Vector3>();
        private InputCommand[] _prev = System.Array.Empty<InputCommand>();
        private Rng _rng;
        private int _nextBuildId = 1;

        private const float TorsoOffset = 1.1f;   // hurtbox center above feet (matches prototype)

        public SimCore(SimConfig config, uint seed = 12345)
        {
            Config = config ?? SimConfig.Default();
            _rng = new Rng(seed);
            Spears = new SpearState[Config.MaxSpears];
            World.CellSize = Config.CellSize;
            World.StepHeight = Config.StepHeight;
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
                Lives = Config.MatchLives,
                BuildEnergy = Config.BuildMaxEnergy,
            };
            Players.Add(p);
            _spawnPoints.Add(spawnFeet);
            _prev = new InputCommand[Players.Count];
            return p;
        }

        public void AddStaticBox(Vector3 min, Vector3 max) => World.AddStaticBox(min, max);

        // ---- main step ----

        public void Tick(IReadOnlyList<InputCommand> commands)
        {
            _events.Clear();
            float dt = Config.TickDt;

            // Match is decided: freeze the world (a results beat), then auto-rematch.
            if (MatchOver)
            {
                MatchResetTimer -= dt;
                if (MatchResetTimer <= 0f) ResetMatch();
                TickCount++;
                return;
            }

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
                if (p.Eliminated) return; // out of lives: no respawn (match is ending)
                // stock respawn while lives remain (WS3 P1)
                p.ChargeHeldTime -= dt; // reuse as respawn countdown while dead
                if (p.ChargeHeldTime <= 0f) Respawn(p);
                return;
            }

            // ----- look (reduced sensitivity once a charge is committed) -----
            bool committed = p.Phase == AttackPhase.Charging && p.ChargeHeldTime > Config.TapMaxSeconds;
            float sens = committed ? Config.AimSensMultiplier : 1f;
            p.Yaw -= cmd.LookYawDelta * sens;
            p.Pitch = SimMath.ClampPitch(p.Pitch - cmd.LookPitchDelta * sens);

            // ----- build the tick's displacement -----
            Vector3 fwd = SimMath.PlanarForward(p.Yaw);
            Vector3 right = SimMath.PlanarRight(p.Yaw);
            Vector3 move = fwd * cmd.Move.Y + right * cmd.Move.X;
            Vector3 disp = Vector3.Zero;
            if (move.LengthSquared() > 1e-6f)
            {
                move = SimMath.NormalizeSafe(move) * (Config.MoveSpeed * dt);
                disp.X = move.X;
                disp.Z = move.Z;
            }

            if (cmd.JumpHeld && !prev.JumpHeld && p.Grounded)
            {
                p.VelocityY = Config.JumpSpeed;
                p.Grounded = false;
            }
            p.VelocityY += Config.Gravity * dt;
            disp.Y = p.VelocityY * dt;

            // ----- voxel swept-AABB controller (walls / walk-up / jump-over) -----
            World.MoveBody(ref p.Feet, Config.PlayerRadius, Config.PlayerHeight, disp,
                out bool grounded, out bool ceiling, out float stepGain);
            if (grounded) { p.VelocityY = 0f; p.Grounded = true; }
            else p.Grounded = false;
            if (ceiling && p.VelocityY > 0f) p.VelocityY = 0f;

            // eased step-up: raise the logical feet instantly, ease the visual eye
            if (stepGain > 0f) p.StepEaseOffset += stepGain;
            if (p.StepEaseOffset > 0f)
            {
                p.StepEaseOffset -= p.StepEaseOffset * SimMath.Clamp01(dt / Config.StepEaseSeconds);
                if (p.StepEaseOffset < 0.001f) p.StepEaseOffset = 0f;
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
            Vector3 origin = MuzzleOrigin(p);
            SpawnSpear(p.Id, origin, dir * speed);
            Emit(SimEventType.SpearThrown, p.Id, position: origin, amount: power);
        }

        /// <summary>World position the held spear leaves the hand — lower-right of the
        /// view. The camera mirrors X vs the sim frame, so screen-right is -right here.</summary>
        public Vector3 MuzzleOrigin(PlayerState p)
        {
            Vector3 fwd = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            Vector3 right = SimMath.NormalizeSafe(Vector3.Cross(fwd, new Vector3(0f, 1f, 0f)));
            Vector3 up = Vector3.Cross(right, fwd);
            return p.EyePosition(Config.EyeHeight)
                 + fwd * Config.MuzzleForward
                 - right * Config.MuzzleRight
                 + up * Config.MuzzleUp;
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

        // ---- building (Phase 1) — voxel staircase, hold-to-preview ----

        private readonly List<Cell> _buildScratch = new List<Cell>();

        private void StepBuilding(PlayerState p, InputCommand cmd, InputCommand prev, float dt)
        {
            p.IsBuildPreviewing = cmd.BuildHeld;

            if (cmd.RotateBuildHeld && !prev.RotateBuildHeld)
                p.BuildRotationSteps = (p.BuildRotationSteps + 1) & 3;

            // hold-to-preview: place on RELEASE (a quick tap still fires once on release)
            if (!cmd.BuildHeld && prev.BuildHeld)
                TryPlaceBuild(p);

            if (p.BuildEnergy < Config.BuildMaxEnergy)
                p.BuildEnergy = System.MathF.Min(Config.BuildMaxEnergy,
                    p.BuildEnergy + Config.BuildEnergyRegenPerSec * dt);
        }

        /// <summary>
        /// Compute the world voxel cells the current build would occupy from the
        /// player's aim, without placing them (used by the ghost preview too).
        /// Uses the player's CUSTOM voxel template if they have one authored, else the
        /// default walkable staircase. Both rise/extend AWAY from the player, centred
        /// left-right on the aim; RotateBuild offsets the facing in 90-degree steps.
        /// Returns a shared scratch list — consume it before the next call.
        /// </summary>
        public bool TryGetBuildPlacement(PlayerState p, out List<Cell> cells)
        {
            cells = _buildScratch;
            cells.Clear();

            Vector3 eye = p.EyePosition(Config.EyeHeight);
            Vector3 dir = SimMath.NormalizeSafe(SimMath.Forward(p.Yaw, p.Pitch));
            if (!World.AimGroundPoint(eye, dir, World.GroundHeight, out Vector3 ground)) return false;

            // clamp within reach on the ground plane
            Vector3 flat = new Vector3(ground.X - p.Feet.X, 0f, ground.Z - p.Feet.Z);
            float d = flat.Length();
            if (d > Config.BuildReach)
                ground = new Vector3(p.Feet.X, World.GroundHeight, p.Feet.Z) + flat * (Config.BuildReach / d);

            float cs = Config.CellSize;
            int ox = (int)System.MathF.Floor(ground.X / cs);
            int oz = (int)System.MathF.Floor(ground.Z / cs);
            int oy = (int)System.MathF.Floor(World.GroundHeight / cs);

            // facing → away-from-player cell direction, plus a perpendicular width axis
            (int dx, int dz) = DominantDir(SimMath.PlanarForward(p.Yaw), p.BuildRotationSteps);
            int px = dz, pz = dx;

            if (p.BuildTemplate != null && !p.BuildTemplate.IsEmpty)
            {
                // Custom template: local X = width (centred on aim), Z = depth (away), Y = up.
                var t = p.BuildTemplate;
                int wOff = t.SizeX / 2;
                foreach (var lc in t.FilledCells())
                {
                    int wx = lc.X - wOff;
                    cells.Add(new Cell(ox + dx * lc.Z + px * wx, oy + lc.Y, oz + dz * lc.Z + pz * wx));
                }
            }
            else
            {
                // Default walkable staircase.
                for (int r = 0; r < Config.BuildRunLength; r++)
                    for (int y = 0; y <= r; y++)
                        for (int w = 0; w < Config.BuildWidth; w++)
                            cells.Add(new Cell(ox + dx * r + px * w, oy + y, oz + dz * r + pz * w));
            }
            return true;
        }

        /// <summary>Dominant cardinal facing (±X/±Z), rotated by 90-degree build steps.</summary>
        private static (int dx, int dz) DominantDir(Vector3 f, int rot)
        {
            int dx, dz;
            if (System.MathF.Abs(f.Z) >= System.MathF.Abs(f.X)) { dx = 0; dz = f.Z >= 0f ? 1 : -1; }
            else { dz = 0; dx = f.X >= 0f ? 1 : -1; }
            for (int i = 0; i < (rot & 3); i++) { int ndx = dz, ndz = -dx; dx = ndx; dz = ndz; }
            return (dx, dz);
        }

        public bool CanPlaceBuild(PlayerState p, List<Cell> cells)
        {
            if (p.BuildEnergy < Config.BuildCostPerPlace) return false;
            float cs = Config.CellSize;
            // don't bury any living player inside the new build
            for (int i = 0; i < Players.Count; i++)
            {
                var q = Players[i];
                if (!q.Alive) continue;
                Vector3 pmin = new Vector3(q.Feet.X - Config.PlayerRadius, q.Feet.Y, q.Feet.Z - Config.PlayerRadius);
                Vector3 pmax = new Vector3(q.Feet.X + Config.PlayerRadius, q.Feet.Y + Config.PlayerHeight, q.Feet.Z + Config.PlayerRadius);
                for (int c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    Vector3 cmin = new Vector3(cell.X * cs, cell.Y * cs, cell.Z * cs);
                    Vector3 cmax = new Vector3((cell.X + 1) * cs, (cell.Y + 1) * cs, (cell.Z + 1) * cs);
                    if (pmin.X < cmax.X && pmax.X > cmin.X && pmin.Y < cmax.Y && pmax.Y > cmin.Y &&
                        pmin.Z < cmax.Z && pmax.Z > cmin.Z)
                        return false;
                }
            }
            return true;
        }

        private void TryPlaceBuild(PlayerState p)
        {
            if (!TryGetBuildPlacement(p, out var cells)) return;
            if (!CanPlaceBuild(p, cells)) return;

            int id = _nextBuildId++;
            var arr = cells.ToArray();
            World.AddBuild(id, arr);
            Builds.Add(new BuildState { Id = id, OwnerId = p.Id, Cells = arr });
            p.BuildEnergy -= Config.BuildCostPerPlace;
            Emit(SimEventType.BuildPlaced, p.Id, position: BuildCenter(arr));

            EnforceBuildCap(p.Id);
        }

        private Vector3 BuildCenter(Cell[] cells)
        {
            if (cells.Length == 0) return Vector3.Zero;
            Vector3 sum = Vector3.Zero;
            float cs = Config.CellSize;
            foreach (var c in cells) sum += new Vector3((c.X + 0.5f) * cs, (c.Y + 0.5f) * cs, (c.Z + 0.5f) * cs);
            return sum / cells.Length;
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
                        World.RemoveBuild(Builds[i].Id);
                        Emit(SimEventType.BuildEvicted, ownerId, position: BuildCenter(Builds[i].Cells));
                        Builds.RemoveAt(i);
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
                victim.Lives--;
                _events.Add(new SimEvent { Type = SimEventType.Death, ActorId = attacker.Id, TargetId = victim.Id, Position = at });
                _events.Add(new SimEvent { Type = SimEventType.LifeLost, ActorId = attacker.Id, TargetId = victim.Id, Position = at, Amount = victim.Lives });

                if (victim.Lives <= 0)
                {
                    // out of stocks → attacker wins the match
                    victim.Eliminated = true;
                    MatchOver = true;
                    WinnerId = attacker.Id;
                    MatchResetTimer = Config.MatchResetDelaySeconds;
                    _events.Add(new SimEvent { Type = SimEventType.MatchOver, ActorId = attacker.Id, TargetId = victim.Id, Position = at });
                }
                else
                {
                    victim.ChargeHeldTime = Config.RespawnDelaySeconds; // reused as respawn countdown while dead
                }
            }
        }

        /// <summary>Start a fresh match: full lives/health, players back to spawns, all
        /// player-built geometry and spears cleared. Auto-invoked after the results beat;
        /// (later) a manual "rematch" button can call this too.</summary>
        public void ResetMatch()
        {
            for (int i = 0; i < Builds.Count; i++) World.RemoveBuild(Builds[i].Id);
            Builds.Clear();
            for (int i = 0; i < Spears.Length; i++) Spears[i].Active = false;

            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Feet = _spawnPoints[i];
                p.VelocityY = 0f;
                p.Grounded = true;
                p.Health = Config.MaxHealth;
                p.Lives = Config.MatchLives;
                p.Eliminated = false;
                p.Phase = AttackPhase.Idle;
                p.ChargeHeldTime = 0f;
                p.StepEaseOffset = 0f;
                p.BuildEnergy = Config.BuildMaxEnergy;
            }

            MatchOver = false;
            WinnerId = -1;
            MatchResetTimer = 0f;
            Emit(SimEventType.MatchReset, -1);
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
            origin = MuzzleOrigin(p);
            velocity = dir * speed;
            return true;
        }

        public float NextRandom() => _rng.NextFloat();
    }
}
