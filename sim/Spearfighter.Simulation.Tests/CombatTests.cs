using System;
using System.Numerics;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    public class ChargeAndAttackTests
    {
        [Fact]
        public void ChargePower_MapsTapToZero_AndFullHoldToOne()
        {
            var sim = Harness.FlatSim();
            var c = sim.Config;
            Assert.Equal(0f, sim.ChargePower(c.TapMaxSeconds), 3);
            Assert.Equal(1f, sim.ChargePower(c.ChargeFullSeconds), 3);
            Assert.Equal(0f, sim.ChargePower(0f), 3);                 // below tap clamps to 0
            Assert.Equal(1f, sim.ChargePower(c.ChargeFullSeconds + 5f), 3); // above full clamps to 1
            float mid = (c.TapMaxSeconds + c.ChargeFullSeconds) * 0.5f;
            Assert.Equal(0.5f, sim.ChargePower(mid), 2);
        }

        [Fact]
        public void QuickTap_ProducesJab_NotAThrow()
        {
            var sim = Harness.FlatSim();
            sim.AddPlayer(Vector3.Zero);

            // press one tick, release next -> held ~ 1 tick < TapMax -> jab
            sim.Tick(new[] { Harness.Attack(true) });
            sim.Tick(new[] { Harness.Attack(false) });

            Assert.True(Harness.HasEvent(sim, SimEventType.Jab));
            Assert.False(Harness.HasEvent(sim, SimEventType.SpearThrown));
            Assert.All(sim.Spears, s => Assert.False(s.Active));
        }

        [Fact]
        public void LongHold_ProducesThrow_NotAJab()
        {
            var sim = Harness.FlatSim();
            sim.AddPlayer(Vector3.Zero);

            Harness.Hold(sim, Harness.Attack(true), 40); // > TapMax
            sim.Tick(new[] { Harness.Attack(false) });   // release

            Assert.True(Harness.HasEvent(sim, SimEventType.SpearThrown));
            Assert.False(Harness.HasEvent(sim, SimEventType.Jab));
            int active = 0;
            foreach (var s in sim.Spears) if (s.Active) active++;
            Assert.Equal(1, active);
        }

        [Fact]
        public void ShortHoldButBigDrag_CountsAsThrow_NotJab()
        {
            var sim = Harness.FlatSim();
            sim.AddPlayer(Vector3.Zero);
            // brief hold but the aim thumb dragged a lot -> should throw, not jab
            var drag = new InputCommand { AttackHeld = true, AttackDragPixels = 30f };
            sim.Tick(new[] { drag });
            sim.Tick(new[] { Harness.Attack(false) });
            Assert.True(Harness.HasEvent(sim, SimEventType.SpearThrown));
            Assert.False(Harness.HasEvent(sim, SimEventType.Jab));
        }

        [Fact]
        public void JabHitsEnemyInCone_AndDealsDamage()
        {
            var sim = Harness.FlatSim();
            var me = sim.AddPlayer(Vector3.Zero, yaw: 0f);        // faces -Z
            var foe = sim.AddPlayer(new Vector3(0, 0, -1.5f));    // 1.5m ahead, in jab range
            float before = foe.Health;

            sim.Tick(new[] { Harness.Attack(true), InputCommand.Empty });
            sim.Tick(new[] { Harness.Attack(false), InputCommand.Empty });

            Assert.True(foe.Health < before);
            Assert.True(Harness.HasEvent(sim, SimEventType.Hit));
        }

        [Fact]
        public void ChargingReducesLookSensitivity()
        {
            // control: not charging, apply a yaw delta
            var ctrl = Harness.FlatSim();
            ctrl.AddPlayer(Vector3.Zero);
            ctrl.Tick(new[] { new InputCommand { LookYawDelta = 0.1f } });
            float ctrlYaw = ctrl.Players[0].Yaw;

            // charging (committed): same yaw delta should be scaled by AimSensMultiplier
            var sim = Harness.FlatSim();
            var p = sim.AddPlayer(Vector3.Zero);
            Harness.Hold(sim, Harness.Attack(true), 15); // exceed TapMax => committed charge
            float yawBefore = p.Yaw;
            sim.Tick(new[] { new InputCommand { AttackHeld = true, LookYawDelta = 0.1f } });
            float applied = p.Yaw - yawBefore;
            float expected = ctrlYaw * sim.Config.AimSensMultiplier;

            Assert.Equal(expected, applied, 4);
        }
    }

    public class ProjectileTests
    {
        [Fact]
        public void PredictPath_MatchesActualSpearIntegration_WithSameStep()
        {
            var cfg = SimConfig.Default();
            var world = new CollisionWorld();
            Vector3 origin = new Vector3(0, 1.6f, 0);
            Vector3 vel = new Vector3(0, 3f, -20f);

            // reference: predicted path at the sim tick dt
            var pts = new Vector3[200];
            int n = Ballistics.PredictPath(origin, vel, cfg.SpearGravity, cfg.TickDt, world, 0f, pts);

            // manual Euler integration with the same step must match point-for-point
            Vector3 p = origin, v = vel;
            for (int i = 0; i < n; i++)
            {
                Assert.Equal(p.X, pts[i].X, 4);
                Assert.Equal(p.Y, pts[i].Y, 4);
                Assert.Equal(p.Z, pts[i].Z, 4);
                v.Y += cfg.SpearGravity * cfg.TickDt;
                p += v * cfg.TickDt;
            }
        }

        [Fact]
        public void ThrownSpear_Arcs_And_Sticks_InGround()
        {
            var sim = Harness.FlatSim();
            sim.AddPlayer(Vector3.Zero);
            Harness.Hold(sim, Harness.Attack(true), 40);
            sim.Tick(new[] { Harness.Attack(false) });

            bool stuck = false;
            for (int t = 0; t < 600 && !stuck; t++)
            {
                sim.Tick(new[] { InputCommand.Empty });
                foreach (var s in sim.Spears) if (s.Active && s.Stuck) stuck = true;
            }
            Assert.True(stuck, "spear should arc down and stick into the ground");
        }

        [Fact]
        public void SolvedAim_LandsProjectileOnTarget()
        {
            var sim = Harness.FlatSim();
            var me = sim.AddPlayer(Vector3.Zero, yaw: 0f);
            var foe = sim.AddPlayer(new Vector3(0, 0, -12f)); // 12m ahead
            float before = foe.Health;

            // aim like the bot would, at full charge speed
            float power = sim.ChargePower(sim.Config.ChargeFullSeconds);
            float speed = sim.Config.ThrowSpeedMin + (sim.Config.ThrowSpeedMax - sim.Config.ThrowSpeedMin) * power;
            float dist = 12f;
            float heightDelta = (foe.Feet.Y + 1.1f) - (me.Feet.Y + sim.Config.EyeHeight);
            Assert.True(Ballistics.SolveLaunchPitch(dist, heightDelta, speed, MathF.Abs(sim.Config.SpearGravity), out float pitch));

            me.Pitch = pitch;
            Harness.Hold(sim, Harness.Attack(true), 80);       // full charge
            sim.Tick(new[] { Harness.Attack(false), InputCommand.Empty });

            for (int t = 0; t < 400 && foe.Health >= before; t++)
                sim.Tick(new[] { InputCommand.Empty, InputCommand.Empty });

            Assert.True(foe.Health < before, "solved ballistic aim should connect with the target");
        }
    }
}
