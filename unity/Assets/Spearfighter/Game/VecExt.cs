using UnityEngine;

namespace Spearfighter.Game
{
    /// <summary>Conversions across the sim boundary: sim uses System.Numerics, Unity uses UnityEngine.</summary>
    public static class VecExt
    {
        public static Vector3 ToUnity(this System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        public static System.Numerics.Vector3 ToNumerics(this Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
    }
}
