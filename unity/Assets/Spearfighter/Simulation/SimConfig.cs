using System;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// Plain-data config for every tunable in the simulation. Engine-agnostic POCO:
    /// the Unity layer builds one of these from ScriptableObjects / remote config
    /// (WS0: "all tunables data-driven, never hardcoded"), and tests use Default().
    ///
    /// Phase 0 combat/movement values are ported VERBATIM from the validated
    /// Phase 0 web prototype (spear_prototype.html lines ~82-99). Do not "improve"
    /// them without a fresh on-thumb feel test — that number set is the whole point
    /// of Phase 0. Phase 1 build/energy values are NEW and marked as needing tuning.
    /// </summary>
    [Serializable]
    public sealed class SimConfig
    {
        // ---- fixed tick ----
        public int TickRate = 60;              // Hz. Sim is fixed-step; render interpolates.
        public float TickDt => 1f / TickRate;

        // ---- movement / body (prototype) ----
        public float EyeHeight = 1.7f;         // EYE
        public float MoveSpeed = 6.0f;         // MOVE_SPEED (m/s)
        public float Gravity = -22.0f;         // GRAVITY (player)
        public float JumpSpeed = 8.0f;         // JUMP_SPEED
        public float PlayerRadius = 0.45f;     // half-width of the player collision box

        // ---- voxel collision / character controller ----
        public float PlayerHeight = 1.8f;      // full height of the player collision box
        public float StepHeight = 0.55f;       // auto-climb threshold (>= one cell)
        public float StepEaseSeconds = 0.08f;  // smooth the visual rise after a step-up
        public float CellSize = 0.5f;          // world voxel grid cell size

        // ---- attack / charge (prototype) ----
        public float TapMaxSeconds = 0.15f;    // TAP_MAX: hold shorter than this => jab
        public float ChargeFullSeconds = 1.15f;// CHARGE_FULL: hold-after-tap for full power
        public float ThrowSpeedMin = 15.0f;    // THROW_MIN (m/s at 0 charge)
        public float ThrowSpeedMax = 34.0f;    // THROW_MAX (m/s at full charge)
        public float SpearGravity = -15.0f;    // SPEAR_GRAVITY: spears arc floatier than the player
        public float JabRange = 3.0f;          // JAB_RANGE
        public float JabHalfAngleDeg = 38.0f;  // JAB_DOT = cos(38deg): jab acceptance cone
        public float TapMaxDrag = 14.0f;       // px of drag under which a release still counts as a jab
        public float AimSensMultiplier = 0.42f;// AIM_SENS_MULT: look sensitivity while charging
        public int MaxSpears = 14;             // MAX_SPEARS: pooled projectile cap
        public float SpearLifeSeconds = 6.0f;  // prototype spear life
        // Muzzle = where the held spear leaves the hand (lower-RIGHT of the view).
        // Both the thrown spear and the aim-arc originate here so the arc emanates
        // from the visible spear tip, not screen center.
        public float MuzzleForward = 0.6f;
        public float MuzzleRight = 0.32f;
        public float MuzzleUp = -0.22f;

        // ---- combat resolution ----
        public float EnemyHurtRadius = 1.05f;  // enemyState.hitRadius: projectile hit sphere
        public float SpearDamage = 34f;        // NEW (tune): per-throw damage
        public float JabDamage = 18f;          // NEW (tune): per-jab damage
        public float MaxHealth = 100f;         // NEW (tune)

        // ---- trajectory preview ----
        public int TrajectoryMaxPoints = 60;   // TRAJ_MAX
        public float TrajectoryStepDt = 0.045f;// prototype preview integration step

        // ---- Phase 1: building (voxel staircase; NEW — needs on-device tuning) ----
        public float BuildMaxEnergy = 100f;
        public float BuildEnergyRegenPerSec = 20f;
        public float BuildCostPerPlace = 34f;    // ~3 builds before empty; regens in ~5s
        public int MaxSimultaneousBuilds = 6;    // oldest despawns past this (also perf bound)
        public float BuildReach = 8f;            // max place distance from the player
        public int BuildRunLength = 4;           // default staircase: steps up-slope (cells)
        public int BuildWidth = 2;               // default staircase: width (cells)

        // ---- Phase 1: bot (NEW — needs tuning) ----
        public float BotPreferredRange = 12f;    // holds spear range
        public float BotRangeTolerance = 3f;
        public float BotReactionSeconds = 0.25f;
        public float BotChargeSeconds = 0.7f;    // how long the bot charges before throwing
        public float BotBuildChance = 0.15f;     // per decision, chance to build cover

        public static SimConfig Default() => new SimConfig();
    }
}
