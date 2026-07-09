using System.Collections.Generic;
using System.Numerics;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    /// <summary>Small helpers to drive the simulation from tests.</summary>
    internal static class Harness
    {
        public static Simulation FlatSim(SimConfig cfg = null, uint seed = 1)
            => new Simulation(cfg ?? SimConfig.Default(), seed);

        /// <summary>Step the sim once with the given per-player commands.</summary>
        public static void Step(Simulation sim, params InputCommand[] cmds) => sim.Tick(cmds);

        /// <summary>Hold a single command for n ticks (same command each tick).</summary>
        public static void Hold(Simulation sim, InputCommand cmd, int ticks, int players = 1)
        {
            var arr = new InputCommand[players];
            for (int t = 0; t < ticks; t++)
            {
                arr[0] = cmd;
                sim.Tick(arr);
            }
        }

        public static InputCommand Attack(bool held) => new InputCommand { AttackHeld = held };

        public static bool HasEvent(Simulation sim, SimEventType type)
        {
            foreach (var e in sim.Events) if (e.Type == type) return true;
            return false;
        }

        public static int CountEvents(Simulation sim, SimEventType type)
        {
            int n = 0;
            foreach (var e in sim.Events) if (e.Type == type) n++;
            return n;
        }
    }
}
