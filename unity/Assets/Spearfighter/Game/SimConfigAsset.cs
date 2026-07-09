using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Data-driven config (WS0). Designers tune combat/build/economy here — or via
    /// remote config later — with no code changes. This is the Unity-facing mirror
    /// of the engine-agnostic <see cref="SimConfig"/>. Defaults are the VALIDATED
    /// Phase 0 prototype numbers; do not change combat values without a fresh
    /// on-thumb feel test.
    /// </summary>
    [CreateAssetMenu(menuName = "Spearfighter/Sim Config", fileName = "SimConfig")]
    public sealed class SimConfigAsset : ScriptableObject
    {
        [Header("Fixed tick")]
        public int tickRate = 60;

        [Header("Movement / body (prototype)")]
        public float eyeHeight = 1.7f;
        public float moveSpeed = 6.0f;
        public float gravity = -22.0f;
        public float jumpSpeed = 8.0f;
        public float playerRadius = 0.45f;

        [Header("Voxel collision / controller")]
        public float playerHeight = 1.8f;
        public float stepHeight = 0.55f;
        public float stepEaseSeconds = 0.08f;
        public float cellSize = 0.5f;

        [Header("Attack / charge (prototype)")]
        public float tapMaxSeconds = 0.15f;
        public float chargeFullSeconds = 1.15f;
        public float throwSpeedMin = 15.0f;
        public float throwSpeedMax = 34.0f;
        public float spearGravity = -15.0f;
        public float jabRange = 3.0f;
        public float jabHalfAngleDeg = 38.0f;
        public float tapMaxDrag = 14.0f;
        public float aimSensMultiplier = 0.42f;
        public int maxSpears = 14;
        public float spearLifeSeconds = 6.0f;

        [Header("Combat resolution")]
        public float enemyHurtRadius = 1.05f;
        public float spearDamage = 34f;
        public float jabDamage = 18f;
        public float maxHealth = 100f;

        [Header("Trajectory preview")]
        public int trajectoryMaxPoints = 60;
        public float trajectoryStepDt = 0.045f;

        [Header("Building (voxel staircase — tune on device)")]
        public float buildMaxEnergy = 100f;
        public float buildEnergyRegenPerSec = 20f;
        public float buildCostPerPlace = 34f;
        public int maxSimultaneousBuilds = 6;
        public float buildReach = 8f;
        public int buildRunLength = 4;
        public int buildWidth = 2;

        [Header("Bot (Phase 1 — tune)")]
        public float botPreferredRange = 12f;
        public float botRangeTolerance = 3f;
        public float botReactionSeconds = 0.25f;
        public float botChargeSeconds = 0.7f;
        public float botBuildChance = 0.15f;

        public SimConfig ToSimConfig()
        {
            return new SimConfig
            {
                TickRate = tickRate,
                EyeHeight = eyeHeight, MoveSpeed = moveSpeed, Gravity = gravity,
                JumpSpeed = jumpSpeed, PlayerRadius = playerRadius,
                PlayerHeight = playerHeight, StepHeight = stepHeight,
                StepEaseSeconds = stepEaseSeconds, CellSize = cellSize,
                TapMaxSeconds = tapMaxSeconds, ChargeFullSeconds = chargeFullSeconds,
                ThrowSpeedMin = throwSpeedMin, ThrowSpeedMax = throwSpeedMax,
                SpearGravity = spearGravity, JabRange = jabRange, JabHalfAngleDeg = jabHalfAngleDeg,
                TapMaxDrag = tapMaxDrag, AimSensMultiplier = aimSensMultiplier,
                MaxSpears = maxSpears, SpearLifeSeconds = spearLifeSeconds,
                EnemyHurtRadius = enemyHurtRadius, SpearDamage = spearDamage,
                JabDamage = jabDamage, MaxHealth = maxHealth,
                TrajectoryMaxPoints = trajectoryMaxPoints, TrajectoryStepDt = trajectoryStepDt,
                BuildMaxEnergy = buildMaxEnergy, BuildEnergyRegenPerSec = buildEnergyRegenPerSec,
                BuildCostPerPlace = buildCostPerPlace, MaxSimultaneousBuilds = maxSimultaneousBuilds,
                BuildReach = buildReach, BuildRunLength = buildRunLength, BuildWidth = buildWidth,
                BotPreferredRange = botPreferredRange, BotRangeTolerance = botRangeTolerance,
                BotReactionSeconds = botReactionSeconds, BotChargeSeconds = botChargeSeconds,
                BotBuildChance = botBuildChance,
            };
        }
    }
}
