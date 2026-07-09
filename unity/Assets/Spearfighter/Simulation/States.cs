using System.Numerics;

namespace Spearfighter.Simulation
{
    public enum AttackPhase { Idle, Charging }

    /// <summary>
    /// Everything the simulation knows about one combatant (human OR bot — they are
    /// identical to the sim). Position is FEET position; eye = feet + EyeHeight.
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

        // Charge FSM (jab vs throw). Driven by the attack button + hold time.
        public AttackPhase Phase = AttackPhase.Idle;
        public float ChargeHeldTime;
        public float AttackDragAccum;

        // Building
        public float BuildEnergy;
        public int BuildRotationSteps; // 0..3, 90-degree increments for the ghost/ramp

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
        public Vector3 Velocity;   // last velocity (also used to orient the render mesh)
        public float Life;
    }

    /// <summary>A placed build (default = ramp-wall). Mirrors a Collider in the world.</summary>
    public struct BuildState
    {
        public int Id;
        public int OwnerId;
        public Vector3 Min;
        public Vector3 Max;
        public int RampAxis;
    }
}
