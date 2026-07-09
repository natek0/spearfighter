using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// THE single input struct (WS0/WS1). A human (touch/desktop), a bot, or the
    /// network all produce this exact value, and the simulation consumes it
    /// identically. Nothing else is allowed to drive player state. This is the
    /// contract that lets server-authoritative PvP be added later without a rewrite.
    ///
    /// Design notes:
    ///  - Buttons are carried as LEVEL state (held true/false), not edges. The sim
    ///    derives rising/falling edges by comparing against the previous tick's
    ///    command. Level state is what survives packet loss cleanly over the wire.
    ///  - Look is carried as a per-tick DELTA in radians at BASE sensitivity. The
    ///    "reduced sensitivity while charging" rule is applied inside the sim,
    ///    because only the sim authoritatively knows the charge state.
    /// </summary>
    public struct InputCommand
    {
        /// <summary>Move intent, components in [-1,1]. x = strafe (+right), y = forward (+fwd).</summary>
        public Vector2 Move;

        /// <summary>Look deltas this tick, radians, at base sensitivity (sim scales while charging).</summary>
        public float LookYawDelta;
        public float LookPitchDelta;

        /// <summary>How far (px) the aim thumb dragged during an attack hold — jab-vs-throw tiebreak.</summary>
        public float AttackDragPixels;

        public bool AttackHeld;
        public bool JumpHeld;
        public bool BuildHeld;
        public bool RotateBuildHeld;
        public bool TrajectoryToggleHeld;

        public static InputCommand Empty => new InputCommand();
    }
}
