using System.Numerics;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    /// <summary>
    /// Covers the Phase-1 "real opponent" bot depth: line-of-sight reasoning, fire
    /// discipline in the open, tactical cover building, and dodging incoming spears.
    /// All of it lives in the engine-agnostic sim, so it is verified here without Unity.
    /// </summary>
    public class BotDepthTests
    {
        [Fact]
        public void SegmentBlocked_SeesWall_ButNotClearAir()
        {
            var world = new VoxelWorld();
            // a wall slab spanning x∈[-2,2], y∈[0,3], z∈[-0.25,0.25]
            world.AddStaticBox(new Vector3(-2, 0, -0.25f), new Vector3(2, 3, 0.25f));

            var a = new Vector3(0, 1.5f, -3f);
            var b = new Vector3(0, 1.5f, 3f);
            Assert.True(world.SegmentBlocked(a, b), "a line through the wall should be blocked");

            // a line entirely on one side never crosses the slab
            Assert.False(world.SegmentBlocked(new Vector3(0, 1.5f, -3f), new Vector3(0, 1.5f, -1f)),
                "a line in clear air should not be blocked");
        }

        [Fact]
        public void Bot_LandsHitsOnStationaryFoe_InTheOpen()
        {
            var cfg = SimConfig.Default();
            cfg.MaxHealth = 10000f; // keep the foe alive so we can measure damage without respawns
            var sim = new SimCore(cfg, seed: 99);
            var bot = sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f);        // faces -Z
            var foe = sim.AddPlayer(new Vector3(0, 0, -12f), yaw: 0f);     // stationary target
            var brain = new BotBrain(seed: 0xB07u);

            float startHealth = foe.Health;
            for (int t = 0; t < 1200; t++) // ~20s
            {
                var botCmd = brain.Think(sim, bot, foe, cfg.TickDt);
                sim.Tick(new[] { botCmd, InputCommand.Empty });
            }

            Assert.True(foe.Health < startHealth, $"bot should land throws on an open target; foe HP={foe.Health}");
        }

        [Fact]
        public void Bot_PlacesTacticalCover_WhenExposedAndUnderFire()
        {
            var cfg = SimConfig.Default();
            var sim = new SimCore(cfg, seed: 7);
            var bot = sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f);        // faces -Z toward foe
            var foe = sim.AddPlayer(new Vector3(0, 0, -12f), yaw: 0f);
            var brain = new BotBrain(seed: 0xC0Feu);

            bool built = false;
            for (int t = 0; t < 300 && !built; t++)
            {
                if (t == 5) bot.Health -= 25f; // simulate taking a hit → arms reactive cover
                var botCmd = brain.Think(sim, bot, foe, cfg.TickDt);
                sim.Tick(new[] { botCmd, InputCommand.Empty });
                if (Harness.HasEvent(sim, SimEventType.BuildPlaced)) built = true;
            }

            Assert.True(built, "an exposed bot under fire should drop a cover build");
        }

        [Fact]
        public void Bot_DodgesAnIncomingSpear()
        {
            var cfg = SimConfig.Default();
            var sim = new SimCore(cfg, seed: 3);
            var bot = sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f);
            var foe = sim.AddPlayer(new Vector3(0, 0, -12f), yaw: 0f);
            var brain = new BotBrain(seed: 0xD0D6u);

            // inject a foe-owned spear on a near-collision course with the bot's torso
            sim.Spears[0] = new SpearState
            {
                Active = true, Stuck = false, OwnerId = foe.Id,
                Position = new Vector3(0, 1.1f, -5f),
                Velocity = new Vector3(0, 0, 14f), // travelling +Z straight at the bot
            };

            var cmd = brain.Think(sim, bot, foe, cfg.TickDt);

            Assert.True(System.MathF.Abs(cmd.Move.X) > 0.1f || cmd.JumpHeld,
                "bot should side-step or jump out of the incoming spear's line");
        }
    }
}
