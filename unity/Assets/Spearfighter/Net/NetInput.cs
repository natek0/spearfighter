using Fusion;
using UnityEngine;

namespace Spearfighter.Net
{
    /// <summary>
    /// The per-tick network input (WS10.0). This is the Fusion-side twin of the sim's
    /// InputCommand — a human's thumb, and (later) a bot, both fill one of these, and
    /// Fusion delivers it to the authoritative + predicted tick. Kept blittable
    /// (NetworkBool, not bool) for clean serialization.
    /// </summary>
    public struct NetInput : INetworkInput
    {
        public Vector2 Move;          // x = strafe (+right), y = forward (+fwd)
        public float LookYawDelta;    // radians this tick (base sensitivity)
        public float LookPitchDelta;
        public NetworkBool Jump;
        public NetworkBool Attack;
        public NetworkBool Build;
    }
}
