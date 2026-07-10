using System.Linq;
using System.Numerics;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    /// <summary>WS4 custom voxel authoring: the template data type + its use by the
    /// sim's build placement.</summary>
    public class VoxelTemplateTests
    {
        [Fact]
        public void EncodeDecode_RoundTrips()
        {
            var t = new VoxelTemplate(4, 3, 4);
            t.Set(0, 0, 0, true);
            t.Set(3, 2, 3, true);
            t.Set(1, 1, 2, true);
            Assert.Equal(3, t.Count);

            var t2 = VoxelTemplate.Decode(t.Encode());
            Assert.NotNull(t2);
            Assert.Equal(t.SizeX, t2.SizeX);
            Assert.Equal(t.SizeY, t2.SizeY);
            Assert.Equal(t.SizeZ, t2.SizeZ);
            Assert.Equal(t.Count, t2.Count);
            Assert.True(t2.Get(0, 0, 0) && t2.Get(3, 2, 3) && t2.Get(1, 1, 2));
            Assert.False(t2.Get(2, 2, 2));
        }

        [Fact]
        public void Decode_BadInput_ReturnsNull()
        {
            Assert.Null(VoxelTemplate.Decode(null));
            Assert.Null(VoxelTemplate.Decode(""));
            Assert.Null(VoxelTemplate.Decode("garbage"));
        }

        [Fact]
        public void Toggle_And_Clear_Work()
        {
            var t = new VoxelTemplate(2, 2, 2);
            t.Toggle(1, 1, 1);
            Assert.True(t.Get(1, 1, 1));
            t.Toggle(1, 1, 1);
            Assert.False(t.Get(1, 1, 1));
            t.Set(0, 0, 0, true);
            t.Clear();
            Assert.True(t.IsEmpty);
        }

        [Fact]
        public void CustomTemplate_PlacesItsOwnCells_NotTheStaircase()
        {
            var sim = new SimCore(SimConfig.Default(), seed: 11);
            var p = sim.AddPlayer(Vector3.Zero, yaw: 0f); // faces -Z
            p.Pitch = -0.6f; // look down so the aim ray meets the ground

            // a 1x1x1 template = a single block
            var single = new VoxelTemplate(1, 1, 1);
            single.Set(0, 0, 0, true);
            p.BuildTemplate = single;

            Assert.True(sim.TryGetBuildPlacement(p, out var cells));
            Assert.Single(cells); // one block, not the multi-cell staircase

            // and placing it creates exactly that solid cell
            sim.Tick(new[] { new InputCommand { BuildHeld = true } });
            sim.Tick(new[] { new InputCommand { BuildHeld = false } });
            Assert.Single(sim.Builds);
            Assert.Single(sim.Builds[0].Cells);
        }

        [Fact]
        public void NullTemplate_FallsBackToStaircase()
        {
            var sim = new SimCore(SimConfig.Default(), seed: 12);
            var p = sim.AddPlayer(Vector3.Zero, yaw: 0f);
            p.Pitch = -0.6f;
            p.BuildTemplate = null;

            Assert.True(sim.TryGetBuildPlacement(p, out var cells));
            // default staircase is many cells (run*width*steps), definitely > 1
            Assert.True(cells.Count > 1, $"default staircase should be multi-cell; got {cells.Count}");
        }
    }
}
