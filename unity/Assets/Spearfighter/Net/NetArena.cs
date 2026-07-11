using Spearfighter.Simulation;

namespace Spearfighter.Net
{
    /// <summary>
    /// The shared, deterministic collision world for the networked match (WS10.0/10.1).
    /// STATIC geometry (ground now; the arena boxes come next) is identical on every
    /// peer, so each peer builds its own copy locally — no networking needed for it.
    /// Only the MUTABLE builds (WS10.2) will be replicated. Reuses the exact same
    /// engine-agnostic VoxelWorld + SimConfig the offline game uses, which is the whole
    /// point of the sim/render split: the server can reproduce collision identically.
    /// </summary>
    public static class NetArena
    {
        public static VoxelWorld World { get; private set; }
        public static SimConfig Config { get; private set; }

        public static void Ensure()
        {
            if (World != null) return;
            Config = SimConfig.Default();
            World = new VoxelWorld
            {
                CellSize = Config.CellSize,
                StepHeight = Config.StepHeight,
                GroundHeight = 0f,
            };
            // WS10.0: flat ground only (isolate the netcode pipeline). Arena boxes +
            // mutable builds are layered in WS10.1/10.2 once sync is proven.
        }
    }
}
