using System.Collections.Generic;
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
            Assert.Equal(0f, p.Feet.Y, 2);
        }

        [Fact]
        public void WalksUpVoxelStaircase()
        {
            var sim = Harness.FlatSim();
            AddStaircase(sim, buildId: 1); // rises along +Z, x in [0,1], up to y=2.0

            // face +Z (yaw = PI) and stand just in front of the staircase base
            var p = sim.AddPlayer(new Vector3(0.5f, 0f, -0.5f), yaw: 3.14159265f);

            float maxY = 0f;
            for (int t = 0; t < 200; t++)
            {
                sim.Tick(new[] { new InputCommand { Move = new Vector2(0, 1f) } });
                if (p.Feet.Y > maxY) maxY = p.Feet.Y;
            }

            Assert.True(maxY > 1.4f, $"player should climb the staircase; maxY={maxY}");
        }

        [Fact]
        public void WallTooTallToStepIsBlocked()
        {
            var sim = Harness.FlatSim();
            // a 2-cell-tall wall (1.0 m > StepHeight) at z in [0,0.5], x in [0,1]
            for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                    sim.World.AddBuild(10 + x * 2 + y, new[] { new Cell(x, y, 0) });

            var p = sim.AddPlayer(new Vector3(0.5f, 0f, -1f), yaw: 3.14159265f); // faces +Z into the wall

            for (int t = 0; t < 120; t++)
                sim.Tick(new[] { new InputCommand { Move = new Vector2(0, 1f) } });

            Assert.True(p.Feet.Z < -0.3f, $"wall taller than step should block; z={p.Feet.Z}");
            Assert.True(p.Feet.Y < 0.3f, $"player should not have climbed a too-tall wall; y={p.Feet.Y}");
        }

        [Fact]
        public void StaticWallStopsHorizontalMovement()
        {
            var sim = Harness.FlatSim();
            sim.World.AddStaticBox(new Vector3(-3, 0, -2.2f), new Vector3(3, 4f, -1.8f));
            var p = sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f); // faces -Z toward the wall

            for (int t = 0; t < 120; t++)
                sim.Tick(new[] { new InputCommand { Move = new Vector2(0, 1f) } });

            Assert.True(p.Feet.Z > -1.9f, "player should be stopped by the wall, not pass through");
            Assert.True(p.Feet.Z < -0.5f, "player should have advanced toward the wall");
        }

        // A staircase: for run r (0..3), a solid column of cells y=0..r, width x in {0,1}, at z=r.
        private static void AddStaircase(SimCore sim, int buildId)
        {
            var cells = new List<Cell>();
            for (int r = 0; r < 4; r++)
                for (int y = 0; y <= r; y++)
                    for (int x = 0; x <= 1; x++)
                        cells.Add(new Cell(x, y, r));
            sim.World.AddBuild(buildId, cells.ToArray());
        }
    }

    public class BuildTests
    {
        private static InputCommand Build(bool held) => new InputCommand { BuildHeld = held };

        [Fact]
        public void PlacingABuild_OnRelease_DrainsEnergyAndCreatesSolidCells()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.Pitch = -0.6f; // look down so the aim ray meets the ground
            float energyBefore = p.BuildEnergy;

            sim.Tick(new[] { Build(true) });   // press → preview, no placement yet
            Assert.Empty(sim.Builds);
            Assert.True(p.IsBuildPreviewing);

            sim.Tick(new[] { Build(false) });  // release → place

            Assert.Single(sim.Builds);
            Assert.True(sim.Builds[0].Cells.Length > 0);
            var c = sim.Builds[0].Cells[0];
            Assert.True(sim.World.IsSolidCell(c.X, c.Y, c.Z));
            Assert.True(p.BuildEnergy < energyBefore);
        }

        [Fact]
        public void CannotBuild_WithoutEnoughEnergy()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.Pitch = -0.6f;
            p.BuildEnergy = 1f;

            sim.Tick(new[] { Build(true) });
            sim.Tick(new[] { Build(false) });

            Assert.Empty(sim.Builds);
        }

        [Fact]
        public void EnergyRegeneratesOverTime()
        {
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            p.BuildEnergy = 0f;
            for (int t = 0; t < 60; t++) sim.Tick(new[] { InputCommand.Empty });
            Assert.True(p.BuildEnergy > 0f);
            Assert.Equal(sim.Config.BuildEnergyRegenPerSec, p.BuildEnergy, 0);
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
                sim.Tick(new[] { Build(true) });
                evicted += Harness.CountEvents(sim, SimEventType.BuildEvicted);
                sim.Tick(new[] { Build(false) }); // release → place
                evicted += Harness.CountEvents(sim, SimEventType.BuildEvicted);
            }

            Assert.Equal(3, sim.Builds.Count);
            Assert.Equal(2, evicted);
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
            sim.AddPlayer(new Vector3(0, 0, 10f), yaw: 0f);
            sim.AddPlayer(new Vector3(0, 0, -10f), yaw: 3.14159f);
            return new Scenario { sim = sim, bot = new BotBrain(seed: 4242) };
        }

        private static void StepScenario(Scenario s, int t)
        {
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
