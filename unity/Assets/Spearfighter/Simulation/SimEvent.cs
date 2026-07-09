using System.Numerics;

namespace Spearfighter.Simulation
{
    public enum SimEventType { SpearThrown, Jab, Hit, SpearStuck, BuildPlaced, BuildEvicted, Death, Respawn, ChargeStart }
    public enum HitKind { None, Jab, Throw }

    /// <summary>
    /// A discrete thing that happened during one Tick. The render/audio/haptics
    /// layer drains these each frame to fire VFX, SFX and haptics WITHOUT reaching
    /// into simulation internals. Keeps rendering fully downstream of the sim.
    /// </summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int ActorId;   // who caused it (attacker / builder)
        public int TargetId;  // who received it (-1 if n/a)
        public Vector3 Position;
        public HitKind HitKind;
        public float Amount;  // damage, charge fraction, etc.
        public bool Lethal;
    }
}
