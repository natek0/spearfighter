using System.Collections.Generic;
using Xunit;
using Spearfighter.Simulation;

namespace Spearfighter.Simulation.Tests
{
    /// <summary>
    /// The remote-config override engine (WS11): fetched key→value pairs must map onto
    /// the right SimConfig fields, leave un-set fields alone, ignore junk keys, and
    /// round int fields. This is the pure logic behind live-tuning; the vendor SDK
    /// (Firebase) only feeds it a dictionary.
    /// </summary>
    public class RemoteConfigTests
    {
        [Fact]
        public void Apply_OverridesOnlyTheGivenKeys()
        {
            var cfg = SimConfig.Default();
            float healthBefore = cfg.MaxHealth;
            var values = new Dictionary<string, double>
            {
                { "spearDamage", 50.0 },
                { "moveSpeed", 7.5 },
            };

            SimConfigRemote.Apply(cfg, values);

            Assert.Equal(50f, cfg.SpearDamage, 3);
            Assert.Equal(7.5f, cfg.MoveSpeed, 3);
            Assert.Equal(healthBefore, cfg.MaxHealth); // untouched key keeps its default
        }

        [Fact]
        public void Apply_RoundsIntegerFields()
        {
            var cfg = SimConfig.Default();
            SimConfigRemote.Apply(cfg, new Dictionary<string, double>
            {
                { "matchLives", 5.0 },
                { "maxSimultaneousBuilds", 8.0 },
            });
            Assert.Equal(5, cfg.MatchLives);
            Assert.Equal(8, cfg.MaxSimultaneousBuilds);
        }

        [Fact]
        public void Apply_IgnoresUnknownKeys_AndNulls()
        {
            var cfg = SimConfig.Default();
            SimConfigRemote.Apply(cfg, new Dictionary<string, double> { { "not_a_real_key", 999.0 } });
            SimConfigRemote.Apply(cfg, null); // must not throw
            Assert.Equal(SimConfig.Default().SpearDamage, cfg.SpearDamage);
        }

        [Fact]
        public void Defaults_CoversEveryKey_WithCurrentValues()
        {
            var cfg = SimConfig.Default();
            var defaults = SimConfigRemote.Defaults(cfg);

            Assert.Equal(SimConfigRemote.Keys.Length, defaults.Count);
            foreach (var key in SimConfigRemote.Keys)
                Assert.True(defaults.ContainsKey(key), $"defaults missing key {key}");
            Assert.Equal(cfg.SpearDamage, defaults["spearDamage"], 3);
            Assert.Equal(cfg.MatchLives, defaults["matchLives"], 3);
        }

        [Fact]
        public void Defaults_RoundTripThroughApply_IsAStableNoOp()
        {
            var cfg = SimConfig.Default();
            var defaults = SimConfigRemote.Defaults(cfg);
            var fresh = SimConfig.Default();
            SimConfigRemote.Apply(fresh, defaults);
            Assert.Equal(cfg.SpearDamage, fresh.SpearDamage, 3);
            Assert.Equal(cfg.BotChargeSeconds, fresh.BotChargeSeconds, 3);
            Assert.Equal(cfg.MatchLives, fresh.MatchLives);
        }
    }
}
