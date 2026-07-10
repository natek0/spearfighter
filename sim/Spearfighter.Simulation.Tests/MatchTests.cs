using System.Numerics;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    /// <summary>
    /// Phase-1 match structure: stocks (lives), elimination, win condition, and the
    /// auto-rematch reset. Combat is driven through real jabs so the whole death path
    /// (ApplyDamage → life loss → respawn/eliminate) is exercised.
    /// </summary>
    public class MatchTests
    {
        // A config where one jab is lethal and respawns are near-instant, so tests can
        // burn through stocks quickly without changing the combat code paths.
        private static SimConfig OneShotConfig(int lives)
        {
            var cfg = SimConfig.Default();
            cfg.MaxHealth = 50f;
            cfg.JabDamage = 999f;         // one jab kills
            cfg.MatchLives = lives;
            cfg.RespawnDelaySeconds = 0.05f; // ~3 ticks
            cfg.MatchResetDelaySeconds = 0.2f;
            return cfg;
        }

        // attacker (index 0) faces the victim (index 1) point-blank and jabs once.
        private static void Jab(SimCore sim)
        {
            sim.Tick(new[] { new InputCommand { AttackHeld = true }, InputCommand.Empty });
            sim.Tick(new[] { new InputCommand { AttackHeld = false }, InputCommand.Empty });
        }

        private static SimCore TwoFighters(SimConfig cfg)
        {
            var sim = new SimCore(cfg, seed: 5);
            sim.AddPlayer(new Vector3(0, 0, 0), yaw: 0f);       // attacker, faces -Z
            sim.AddPlayer(new Vector3(0, 0, -1.5f), yaw: 0f);   // victim, inside jab range
            return sim;
        }

        [Fact]
        public void Death_CostsALife_AndRespawns_WhileStocksRemain()
        {
            var sim = TwoFighters(OneShotConfig(lives: 3));
            var victim = sim.Players[1];

            Jab(sim);
            Assert.Equal(2, victim.Lives);
            Assert.False(victim.Eliminated);
            Assert.False(sim.MatchOver);

            // it respawns to full health and can fight again
            for (int t = 0; t < 10; t++) sim.Tick(new[] { InputCommand.Empty, InputCommand.Empty });
            Assert.True(victim.Alive);
            Assert.Equal(sim.Config.MaxHealth, victim.Health);
        }

        [Fact]
        public void LosingAllStocks_EndsMatch_WithTheKillerAsWinner()
        {
            var sim = TwoFighters(OneShotConfig(lives: 1));
            var victim = sim.Players[1];

            Jab(sim);

            Assert.True(sim.MatchOver);
            Assert.Equal(0, sim.WinnerId);          // attacker (index 0) won
            Assert.Equal(0, victim.Lives);
            Assert.True(victim.Eliminated);
        }

        [Fact]
        public void MatchOver_FreezesTheWorld_ThenAutoResets()
        {
            var cfg = OneShotConfig(lives: 1);
            var sim = TwoFighters(cfg);
            var attacker = sim.Players[0];
            var victim = sim.Players[1];

            // give the world some builds + a spear to prove reset clears them
            sim.World.AddBuild(999, new[] { new Cell(3, 0, 3) });
            sim.Builds.Add(new BuildState { Id = 999, OwnerId = 0, Cells = new[] { new Cell(3, 0, 3) } });

            Jab(sim);
            Assert.True(sim.MatchOver);

            // during the results freeze, inputs do nothing (world is frozen)
            Vector3 frozen = attacker.Feet;
            sim.Tick(new[] { new InputCommand { Move = new Vector2(0, 1f) }, InputCommand.Empty });
            Assert.Equal(frozen, attacker.Feet);

            // tick past the reset delay → fresh match
            for (int t = 0; t < 30; t++) sim.Tick(new[] { InputCommand.Empty, InputCommand.Empty });

            Assert.False(sim.MatchOver);
            Assert.Equal(-1, sim.WinnerId);
            Assert.Equal(cfg.MatchLives, victim.Lives);
            Assert.Equal(cfg.MatchLives, attacker.Lives);
            Assert.False(victim.Eliminated);
            Assert.Empty(sim.Builds);
            Assert.False(sim.World.IsSolidCell(3, 0, 3)); // build geometry cleared
        }
    }
}
