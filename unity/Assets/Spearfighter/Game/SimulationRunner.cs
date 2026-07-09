using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Drives the simulation on a FIXED timestep decoupled from render framerate
    /// (WS0). Each tick it gathers one InputCommand per player — the local player's
    /// from PlayerInput, each bot's from a BotBrain — and calls sim.Tick(). Render
    /// happens once per frame from whatever the latest sim state is.
    ///
    /// When netcode arrives (WS10), the only change here is WHERE commands[i] comes
    /// from (network vs local vs bot). The tick loop and the sim are untouched. That
    /// is the whole point of the architecture.
    /// </summary>
    public sealed class SimulationRunner : MonoBehaviour
    {
        public SimCore Sim;
        public int LocalIndex;
        public PlayerInput Input;
        public WorldRenderer Renderer;
        public TrajectoryRenderer Trajectory;
        public HudGui Hud;
        public Camera Cam;

        private readonly List<(int index, BotBrain brain)> _bots = new();
        private InputCommand[] _commands;
        private float _accumulator;
        private bool _showTrajectory = true;

        private const float MaxCatchUp = 0.25f; // clamp to avoid spiral-of-death

        public void AddBot(int playerIndex, BotBrain brain) => _bots.Add((playerIndex, brain));

        public void Begin()
        {
            _commands = new InputCommand[Sim.Players.Count];
            Renderer.Init(Sim, LocalIndex, Cam);
            Trajectory.Init(Sim);
            Hud.Init(this, Sim, Input, LocalIndex);
        }

        public bool ShowTrajectory => _showTrajectory;

        private void Update()
        {
            if (Sim == null) return;
            float dt = Sim.Config.TickDt;

            Input.Sample();
            _accumulator += Mathf.Min(Time.deltaTime, MaxCatchUp);

            int guard = 0;
            while (_accumulator >= dt && guard++ < 8)
            {
                for (int i = 0; i < _commands.Length; i++) _commands[i] = InputCommand.Empty;

                var local = Input.Consume();
                if (local.TrajectoryToggleHeld) _showTrajectory = !_showTrajectory;
                _commands[LocalIndex] = local;

                foreach (var (index, brain) in _bots)
                    _commands[index] = brain.Think(Sim, Sim.Players[index], Sim.Players[LocalIndex], dt);

                Sim.Tick(_commands);
                DrainEvents();
                _accumulator -= dt;
            }

            Renderer.Render();
            Trajectory.Render(LocalIndex, _showTrajectory);
        }

        private void DrainEvents()
        {
            var events = Sim.Events;
            for (int i = 0; i < events.Count; i++)
                Hud.HandleEvent(events[i]);
        }
    }
}
