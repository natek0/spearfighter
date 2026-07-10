using System;
using System.Collections.Generic;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// The remote-config seam (WS11). A single binding table maps string keys to
    /// <see cref="SimConfig"/> fields so combat/build/economy/bot values can be live-
    /// tuned or A/B-tested from a backend (Firebase Remote Config) WITHOUT shipping a
    /// new build. Engine-agnostic + unit-tested: the Unity layer collects the fetched
    /// key→value pairs and calls <see cref="Apply"/>; the sim never knows a backend
    /// exists. <see cref="Defaults"/> emits the current values (for the backend's
    /// in-app defaults and the console setup sheet). Keys mirror SimConfigAsset names.
    /// </summary>
    public static class SimConfigRemote
    {
        private struct Binding
        {
            public string Key;
            public Func<SimConfig, double> Get;
            public Action<SimConfig, double> Set;
        }

        private static Binding B(string key, Func<SimConfig, double> get, Action<SimConfig, double> set)
            => new Binding { Key = key, Get = get, Set = set };

        // Only values worth hot-fixing / A/B-testing are exposed. Add a line to grow the set.
        private static readonly Binding[] Bindings =
        {
            // combat
            B("spearDamage",        c => c.SpearDamage,        (c, v) => c.SpearDamage = (float)v),
            B("jabDamage",          c => c.JabDamage,          (c, v) => c.JabDamage = (float)v),
            B("maxHealth",          c => c.MaxHealth,          (c, v) => c.MaxHealth = (float)v),
            B("throwSpeedMin",      c => c.ThrowSpeedMin,      (c, v) => c.ThrowSpeedMin = (float)v),
            B("throwSpeedMax",      c => c.ThrowSpeedMax,      (c, v) => c.ThrowSpeedMax = (float)v),
            B("spearGravity",       c => c.SpearGravity,       (c, v) => c.SpearGravity = (float)v),
            B("jabRange",           c => c.JabRange,           (c, v) => c.JabRange = (float)v),
            B("chargeFullSeconds",  c => c.ChargeFullSeconds,  (c, v) => c.ChargeFullSeconds = (float)v),
            B("tapMaxSeconds",      c => c.TapMaxSeconds,      (c, v) => c.TapMaxSeconds = (float)v),
            B("enemyHurtRadius",    c => c.EnemyHurtRadius,    (c, v) => c.EnemyHurtRadius = (float)v),
            // movement
            B("moveSpeed",          c => c.MoveSpeed,          (c, v) => c.MoveSpeed = (float)v),
            B("jumpSpeed",          c => c.JumpSpeed,          (c, v) => c.JumpSpeed = (float)v),
            B("gravity",            c => c.Gravity,            (c, v) => c.Gravity = (float)v),
            // building / economy
            B("buildMaxEnergy",         c => c.BuildMaxEnergy,         (c, v) => c.BuildMaxEnergy = (float)v),
            B("buildEnergyRegenPerSec", c => c.BuildEnergyRegenPerSec, (c, v) => c.BuildEnergyRegenPerSec = (float)v),
            B("buildCostPerPlace",      c => c.BuildCostPerPlace,      (c, v) => c.BuildCostPerPlace = (float)v),
            B("maxSimultaneousBuilds",  c => c.MaxSimultaneousBuilds,  (c, v) => c.MaxSimultaneousBuilds = (int)Math.Round(v)),
            B("buildReach",             c => c.BuildReach,             (c, v) => c.BuildReach = (float)v),
            // match / stocks
            B("matchLives",            c => c.MatchLives,            (c, v) => c.MatchLives = (int)Math.Round(v)),
            B("respawnDelaySeconds",   c => c.RespawnDelaySeconds,   (c, v) => c.RespawnDelaySeconds = (float)v),
            B("matchResetDelaySeconds",c => c.MatchResetDelaySeconds,(c, v) => c.MatchResetDelaySeconds = (float)v),
            // bot difficulty
            B("botPreferredRange",     c => c.BotPreferredRange,     (c, v) => c.BotPreferredRange = (float)v),
            B("botReactionSeconds",    c => c.BotReactionSeconds,    (c, v) => c.BotReactionSeconds = (float)v),
            B("botChargeSeconds",      c => c.BotChargeSeconds,      (c, v) => c.BotChargeSeconds = (float)v),
            B("botTurnRateRadPerSec",  c => c.BotTurnRateRadPerSec,  (c, v) => c.BotTurnRateRadPerSec = (float)v),
            B("botDodgeTimeToImpact",  c => c.BotDodgeTimeToImpact,  (c, v) => c.BotDodgeTimeToImpact = (float)v),
            B("botBuildCooldownSeconds",c => c.BotBuildCooldownSeconds,(c, v) => c.BotBuildCooldownSeconds = (float)v),
            B("botThreatMemorySeconds",c => c.BotThreatMemorySeconds,(c, v) => c.BotThreatMemorySeconds = (float)v),
        };

        /// <summary>Every remote-config key, for the Unity layer to query the backend with.</summary>
        public static readonly string[] Keys;

        static SimConfigRemote()
        {
            Keys = new string[Bindings.Length];
            for (int i = 0; i < Bindings.Length; i++) Keys[i] = Bindings[i].Key;
        }

        /// <summary>Override cfg fields from fetched values. Missing/unknown keys are ignored.</summary>
        public static void Apply(SimConfig cfg, IReadOnlyDictionary<string, double> values)
        {
            if (cfg == null || values == null) return;
            foreach (var b in Bindings)
                if (values.TryGetValue(b.Key, out double v))
                    b.Set(cfg, v);
        }

        /// <summary>Current values of every key — the backend's in-app defaults + the
        /// numbers to seed the remote-config console with.</summary>
        public static Dictionary<string, double> Defaults(SimConfig cfg)
        {
            var d = new Dictionary<string, double>(Bindings.Length);
            foreach (var b in Bindings) d[b.Key] = b.Get(cfg);
            return d;
        }
    }
}
