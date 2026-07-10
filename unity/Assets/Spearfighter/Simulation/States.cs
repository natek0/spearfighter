using System.Numerics;

namespace Spearfighter.Simulation
{
    public enum AttackPhase { Idle, Charging }

    /// <summary>An integer world voxel cell (grid coordinate). Cell (x,y,z) occupies
    /// world box [x*cs,(x+1)*cs] × [y*cs,(y+1)*cs] × [z*cs,(z+1)*cs].</summary>
    public struct Cell
    {
        public int X, Y, Z;
        public Cell(int x, int y, int z) { X = x; Y = y; Z = z; }
    }

    /// <summary>
    /// Everything the simulation knows about one combatant (human OR bot — they are
    /// identical to the sim). Position is FEET position (bottom-center of the box);
    /// eye = feet + EyeHeight.
    /// </summary>
    public sealed class PlayerState
    {
        public int Id;
        public Vector3 Feet;
        public float Yaw;
        public float Pitch;
        public float VelocityY;
        public bool Grounded = true;

        public float Health;

        // Match stocks: start with SimConfig.MatchLives, lose one per death; at 0 the
        // player is Eliminated (no respawn) and the opponent wins the match.
        public int Lives;
        public bool Eliminated;

        // Charge FSM (jab vs throw).
        public AttackPhase Phase = AttackPhase.Idle;
        public float ChargeHeldTime;
        public float AttackDragAccum;

        // Building
        public float BuildEnergy;
        public int BuildRotationSteps;   // 0..3, 90-degree increments
        public bool IsBuildPreviewing;   // holding BUILD this tick (ghost visible)
        public VoxelTemplate BuildTemplate; // player's authored custom shape; null = default staircase

        // Rendering aid: after an instant step-up, the visual eye lags by this much
        // and eases back to 0, so a voxel staircase reads as a smooth ramp.
        public float StepEaseOffset;

        public Vector3 EyePosition(float eyeHeight) => new Vector3(Feet.X, Feet.Y + eyeHeight, Feet.Z);
        public bool Alive => Health > 0f;
    }

    /// <summary>A spear in flight or stuck. Pooled; capped by SimConfig.MaxSpears.</summary>
    public struct SpearState
    {
        public bool Active;
        public bool Stuck;
        public int OwnerId;
        public Vector3 Position;
        public Vector3 Velocity;   // last velocity (also orients the render mesh)
        public float Life;
    }

    /// <summary>A placed build: a set of solid voxel cells owned by a player.</summary>
    public struct BuildState
    {
        public int Id;
        public int OwnerId;
        public Cell[] Cells;
    }
}
