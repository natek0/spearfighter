namespace Spearfighter.Simulation
{
    /// <summary>
    /// Deterministic xorshift RNG. The simulation must never touch wall-clock time
    /// or UnityEngine.Random: all non-determinism flows through a seeded stream so
    /// that (same input sequence + same seed) reproduces the same world. This is
    /// what makes bots, replays, and (later) server-authoritative netcode line up.
    /// </summary>
    public struct Rng
    {
        private uint _state;

        public Rng(uint seed)
        {
            // Avoid the zero fixed-point of xorshift.
            _state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return x;
        }

        /// <summary>Float in [0,1).</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Float in [min,max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Symmetric float in (-mag, mag).</summary>
        public float Signed(float mag) => Range(-mag, mag);
    }
}
