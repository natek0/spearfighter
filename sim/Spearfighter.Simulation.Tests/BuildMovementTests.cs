using System.Numerics;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    public class MovementTests
    {
        [Fact]
        public void PlayerFallsAndLandsOnGroundPlane()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(new Vector3(0, 5f, 0));
            for (int t = 0; t < 120; t++) sim.Tick(new[] { InputCommand.Empty });
            Assert.True(p.Grounded);
            Assert.Equal(0f, p.Feet.Y, 3);
        }

        [Fact]
        public void PlayerWalksUpAndRestsOnRampSurface()
        {
            var sim = Harness.FlatSim();
            // ramp rising along +Z, from y=0 (low, z=-4) to y=1.9 (high, z=-2)
            sim.World.Add(Collider.Ramp(new Vector3(-1, 0, -4), new Vector3(1, 1.9f, -2), axis: 1, buildId: 1));
            var p = sim.AddPlayer(new Vector3(0, 3f, -3f)); // above the middle of the ramp

            for (int t = 0; t < 180; t++) sim.Tick(new[] { InputCommand.Empty });

            Assert.True(p.Grounded);
            Assert.Equal(0.95f, p.Feet.Y, 2); // mid-ramp surface height
        }

        [Fact]
        public void WallBlocksHorizontalMovement()
        {
            var sim = Harness.FlatSim();
            // a tall wall right in front of the player (spans well above the head)
            sim.World.Add(Collider.Box(new Vector3(-3, 0, -2.2f), new Vector3(3, 4f, -1.8f)));
            var p = sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f); // faces -Z, toward the wall

            // push forward into the wall for a while
            for (int t = 0; t < 120; t++)
                sim.Tick(new[] { new InputCommand { Move = new Vector2(0, 1f) } });

            Assert.True(p.Feet.Z > -1.8f - sim.Config.PlayerRadius - 0.05f,
                "player should be stopped by the wall, not pass through it");
        }
    }

    public class BuildTests
    {
        [Fact]
        public void PlacingABuild_DrainsEnergy_AndCreatesRampAndCollider()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.Pitch = -0.6f; // look down so the aim ray meets the ground
            float energyBefore = p.BuildEnergy;
            int collidersBefore = sim.World.Colliders.Count;

            sim.Tick(new[] { new InputCommand { BuildHeld = true } }); // rising edge -> place

            Assert.True(Harness.HasEvent(sim, SimEventType.BuildPlaced));
            Assert.Single(sim.Builds);
            Assert.Equal(collidersBefore + 1, sim.World.Colliders.Count);
            // build cost is spent, then the same tick's regen tops it up by regen*dt
            float expected = energyBefore - sim.Config.BuildCostPerPlace
                             + sim.Config.BuildEnergyRegenPerSec * sim.Config.TickDt;
            Assert.Equal(expected, p.BuildEnergy, 2);
        }

        [Fact]
        public void CannotBuild_WithoutEnoughEnergy()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.Pitch = -0.6f;
            p.BuildEnergy = 1f; // below cost

            sim.Tick(new[] { new InputCommand { BuildHeld = true } });

            Assert.Empty(sim.Builds);
            Assert.False(Harness.HasEvent(sim, SimEventType.BuildPlaced));
        }

        [Fact]
        public void EnergyRegeneratesOverTime()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.BuildEnergy = 0f;
            for (int t = 0; t < 60; t++) sim.Tick(new[] { InputCommand.Empty }); // ~1s
            Assert.True(p.BuildEnergy > 0f);
            Assert.Equal(sim.Config.BuildEnergyRegenPerSec, p.BuildEnergy, 0); // ~regen*1s
        }

        [Fact]
        public void SimultaneousBuildCap_EvictsOldest()
        {
            var cfg = SimConfig.Default();
            cfg.MaxSimultaneousBuilds = 3;
            cfg.BuildCostPerPlace = 5f;
            cfg.BuildMaxEnergy = 100f;
            var sim = new SimCore(cfg);
            var p = sim.AddPlayer(Vector3.Zero);
            p.Pitch = -0.6f;

            int evicted = 0;
            for (int i = 0; i < 5; i++)
            {
                sim.Tick(new[] { new InputCommand { BuildHeld = true } });  // place (rising edge)
                evicted += Harness.CountEvents(sim, SimEventType.BuildEvicted);
                sim.Tick(new[] { new InputCommand { BuildHeld = false } }); // reset edge
                evicted += Harness.CountEvents(sim, SimEventType.BuildEvicted);
            }

            Assert.Equal(3, sim.Builds.Count);
            Assert.Equal(2, evicted);
            int rampColliders = 0;
            foreach (var c in sim.World.Colliders) if (c.Kind == ColliderKind.Ramp) rampColliders++;
            Assert.Equal(3, rampColliders);
        }
    }

    public class DeterminismTests
    {
        [Fact]
        public void SameInputsAndSeed_ProduceIdenticalWorlds()
        {
            var a = BuildScenario();
            var b = BuildScenario();

            for (int t = 0; t < 300; t++)
            {
                StepScenario(a, t);
                StepScenario(b, t);
            }

            // exact equality: deterministic ops, same seed, same input sequence
            Assert.Equal(a.sim.Players[0].Feet, b.sim.Players[0].Feet);
            Assert.Equal(a.sim.Players[1].Feet, b.sim.Players[1].Feet);
            Assert.Equal(a.sim.Players[0].Health, b.sim.Players[0].Health);
            Assert.Equal(a.sim.Players[1].Health, b.sim.Players[1].Health);
            Assert.Equal(a.sim.Players[1].Yaw, b.sim.Players[1].Yaw);
            Assert.Equal(a.sim.Builds.Count, b.sim.Builds.Count);
        }

        private struct Scenario { public SimCore sim; public BotBrain bot; }

        private static Scenario BuildScenario()
        {
            var sim = new SimCore(SimConfig.Default(), seed: 777);
            sim.AddPlayer(new Vector3(0, 0, 10f), yaw: 0f);   // "human"
            sim.AddPlayer(new Vector3(0, 0, -10f), yaw: 3.14159f); // bot, facing +Z
            return new Scenario { sim = sim, bot = new BotBrain(seed: 4242) };
        }

        private static void StepScenario(Scenario s, int t)
        {
            // scripted "human": move forward, throw every 90 ticks
            var human = new InputCommand
            {
                Move = new Vector2((t % 120 < 60) ? 1f : -1f, 1f),
                AttackHeld = (t % 90) < 45,
            };
            var botCmd = s.bot.Think(s.sim, s.sim.Players[1], s.sim.Players[0], s.sim.Config.TickDt);
            s.sim.Tick(new[] { human, botCmd });
        }
    }
}
