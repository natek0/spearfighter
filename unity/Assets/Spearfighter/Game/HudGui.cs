using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Minimal greybox HUD (health, build energy, charge, crosshair, hit feedback)
    /// plus the on-screen Scheme B buttons, drawn with IMGUI so the prototype runs
    /// with zero Canvas wiring. Production HUD (uGUI/UI Toolkit, customizable
    /// layout, FP-friendly peripheral placement) is WS9.
    /// </summary>
    public sealed class HudGui : MonoBehaviour
    {
        private SimulationRunner _runner;
        private Simulation _sim;
        private PlayerInput _input;
        private int _local;

        private int _hits;
        private float _flash;      // crosshair flash timer
        private float _popTimer;
        private string _popText = "";
        private GUIStyle _big, _small, _pop;

        public void Init(SimulationRunner runner, Simulation sim, PlayerInput input, int localIndex)
        {
            _runner = runner; _sim = sim; _input = input; _local = localIndex;
        }

        public void HandleEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Hit when e.ActorId == _local:
                    _hits++;
                    _flash = 0.09f;
                    _popText = e.HitKind == HitKind.Jab ? "JAB!" : "HIT!";
                    _popTimer = 0.22f;
                    if (_input != null && _input.IsTouch) Handheld.Vibrate();
                    break;
                case SimEventType.Jab when e.ActorId == _local:
                    _flash = 0.06f;
                    break;
            }
        }

        private void Update()
        {
            if (_flash > 0) _flash -= Time.deltaTime;
            if (_popTimer > 0) _popTimer -= Time.deltaTime;
        }

        private void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            _big.normal.textColor = new Color(0.92f, 0.94f, 0.96f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _small.normal.textColor = new Color(0.56f, 0.64f, 0.72f);
            _pop = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _pop.normal.textColor = new Color(1f, 0.82f, 0.29f);
        }

        private void OnGUI()
        {
            if (_sim == null) return;
            EnsureStyles();
            var p = _sim.Players[_local];
            float w = Screen.width, h = Screen.height;

            // crosshair
            var cross = new Color(0.92f, 0.94f, 0.96f, 0.85f);
            if (_flash > 0) cross = new Color(1f, 0.82f, 0.29f, 1f);
            DrawRect(new Rect(w / 2 - 3, h / 2 - 3, 6, 6), cross);

            // counters
            GUI.Label(new Rect(14, 10, 300, 24), $"Hits: {_hits}", _big);
            GUI.Label(new Rect(14, 32, 300, 20), $"HP {Mathf.CeilToInt(p.Health)}   Energy {Mathf.CeilToInt(p.BuildEnergy)}", _small);
            GUI.Label(new Rect(w - 150, 10, 140, 20), _runner.ShowTrajectory ? "arc: on" : "arc: off", _small);

            // health + energy bars (top-left)
            DrawBar(new Rect(14, 54, 180, 8), p.Health / _sim.Config.MaxHealth, new Color(1f, 0.36f, 0.32f));
            DrawBar(new Rect(14, 66, 180, 8), p.BuildEnergy / _sim.Config.BuildMaxEnergy, new Color(0.37f, 0.69f, 1f));

            // charge bar (bottom center) while charging
            if (p.Phase == AttackPhase.Charging && p.ChargeHeldTime > _sim.Config.TapMaxSeconds)
            {
                float power = _sim.ChargePower(p.ChargeHeldTime);
                var r = new Rect(w / 2 - 90, h - 70, 180, 10);
                DrawRect(r, new Color(1f, 1f, 1f, 0.12f));
                DrawRect(new Rect(r.x, r.y, r.width * power, r.height), new Color(1f, 0.82f, 0.29f, 0.95f));
            }

            // hit popup
            if (_popTimer > 0) GUI.Label(new Rect(w / 2 - 100, h / 2 - 70, 200, 40), _popText, _pop);

            // death banner
            if (!p.Alive) GUI.Label(new Rect(w / 2 - 100, h / 2 + 30, 200, 24), "respawning...", _big);

            if (_input != null && _input.IsTouch) DrawTouchControls();
            else GUI.Label(new Rect(14, h - 24, w, 20), "WASD move · mouse look · LMB tap=jab hold=throw · Space jump · B build · R rotate · T arc", _small);
        }

        private void DrawTouchControls()
        {
            DrawButton(_input.AttackRect, "THROW\n/JAB", new Color(1f, 0.82f, 0.29f, 0.28f));
            DrawButton(_input.JumpRect, "JUMP", new Color(0.37f, 0.69f, 1f, 0.18f));
            DrawButton(_input.BuildRect, "BUILD", new Color(0.5f, 1f, 0.6f, 0.18f));
            DrawButton(_input.RotateRect, "ROT", new Color(1f, 1f, 1f, 0.14f));
            if (_input.JoyActive)
            {
                DrawCircle(ToGui(_input.JoyCenter), 55, new Color(1f, 1f, 1f, 0.10f));
                DrawCircle(ToGui(_input.JoyKnob), 27, new Color(1f, 1f, 1f, 0.22f));
            }
        }

        // ---- draw helpers (screen rects are y-up; GUI is y-down) ----
        private Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);
        private Rect ToGui(Rect r) => new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);

        private void DrawButton(Rect screenRect, string label, Color c)
        {
            var g = ToGui(screenRect);
            DrawRect(g, c);
            GUI.Label(g, label, _small);
        }

        private void DrawBar(Rect r, float t, Color c)
        {
            DrawRect(r, new Color(1f, 1f, 1f, 0.12f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
        }

        private void DrawCircle(Vector2 centerGui, float radius, Color c)
            => DrawRect(new Rect(centerGui.x - radius, centerGui.y - radius, radius * 2, radius * 2), c);

        private static Texture2D _px;
        private void DrawRect(Rect r, Color c)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = old;
        }
    }
}
