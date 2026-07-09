using System;
using System.Numerics;

namespace Spearfighter.Simulation
{
    /// <summary>
    /// The MVP opponent (WS5). CRITICAL PROPERTY: the bot produces a normal
    /// InputCommand — the exact struct a human thumb produces — which the
    /// Simulation consumes identically. So "play vs bots" validates the real
    /// simulation/netcode path, not a throwaway. Swap this for a network peer later
    /// and nothing downstream changes.
    ///
    /// Behaviour: hold spear range, turn to face, ballistically solve the aim,
    /// charge+throw, jab up close, occasionally build cover. Navigation over
    /// dynamic geometry is intentionally naive here (straight-line approach/retreat
    /// + jump) — real pathing over player-built ramps is the flagged WS5 ⚠ item and
    /// is scoped for later.
    /// </summary>
    public sealed class BotBrain
    {
        private Rng _rng;
        private float _decisionCd;
        private float _chargeTimer;
        private bool _charging;
        private int _strafeDir = 1;
        private float _strafeCd;

        public BotBrain(uint seed) { _rng = new Rng(seed); }

        public InputCommand Think(Simulation sim, PlayerState me, PlayerState foe, float dt)
        {
            var cmd = InputCommand.Empty;
            if (me == null || foe == null || !me.Alive || !foe.Alive) { _charging = false; return cmd; }
            var cfg = sim.Config;

            Vector3 toFoe = new Vector3(foe.Feet.X - me.Feet.X, 0f, foe.Feet.Z - me.Feet.Z);
            float dist = toFoe.Length();
            if (dist < 1e-3f) dist = 1e-3f;

            // ---- desired aim ----
            float desiredYaw = MathF.Atan2(-toFoe.X, -toFoe.Z); // matches PlanarForward convention
            float power = sim.ChargePower(cfg.BotChargeSeconds);
            float speed = cfg.ThrowSpeedMin + (cfg.ThrowSpeedMax - cfg.ThrowSpeedMin) * power;
            float heightDelta = (foe.Feet.Y + 1.1f) - (me.Feet.Y + cfg.EyeHeight);
            if (!Ballistics.SolveLaunchPitch(dist, heightDelta, speed, MathF.Abs(cfg.SpearGravity), out float desiredPitch))
                desiredPitch = 0.5f; // out of range: lob high

            float maxTurn = 3.0f * dt; // rad per tick
            float yawErr = ShortestAngle(desiredYaw - me.Yaw);
            float pitchErr = desiredPitch - me.Pitch;
            cmd.LookYawDelta = -Clamp(yawErr, -maxTurn, maxTurn);   // sim does Yaw -= delta
            cmd.LookPitchDelta = -Clamp(pitchErr, -maxTurn, maxTurn);
            bool aimed = MathF.Abs(yawErr) < 0.15f;

            // ---- spacing ----
            if (dist > cfg.BotPreferredRange + cfg.BotRangeTolerance) cmd.Move = new Vector2(0f, 1f);
            else if (dist < cfg.BotPreferredRange - cfg.BotRangeTolerance) cmd.Move = new Vector2(0f, -1f);
            else
            {
                _strafeCd -= dt;
                if (_strafeCd <= 0f) { _strafeDir = -_strafeDir; _strafeCd = _rng.Range(0.8f, 1.8f); }
                cmd.Move = new Vector2(_strafeDir, 0f);
            }

            // ---- attack ----
            if (_charging)
            {
                cmd.AttackHeld = true;
                _chargeTimer -= dt;
                if (_chargeTimer <= 0f)
                {
                    _charging = false;
                    cmd.AttackHeld = false; // release -> throw
                    _decisionCd = cfg.BotReactionSeconds + _rng.Range(0.3f, 1.0f);
                }
            }
            else
            {
                _decisionCd -= dt;
                if (_decisionCd <= 0f && aimed)
                {
                    if (dist <= cfg.JabRange)
                    {
                        cmd.AttackHeld = true; // single tick -> jab
                        _decisionCd = cfg.BotReactionSeconds + _rng.Range(0.2f, 0.6f);
                    }
                    else
                    {
                        _charging = true;
                        _chargeTimer = cfg.BotChargeSeconds;
                        cmd.AttackHeld = true; // begin charge
                    }
                }
                else if (_rng.NextFloat() < cfg.BotBuildChance * dt)
                {
                    cmd.BuildHeld = true; // occasional cover
                }
            }

            return cmd;
        }

        private static float ShortestAngle(float a)
        {
            while (a > MathF.PI) a -= 2f * MathF.PI;
            while (a < -MathF.PI) a += 2f * MathF.PI;
            return a;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
