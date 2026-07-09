using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Minimal greybox HUD + on-screen Scheme B buttons, drawn with IMGUI so the
    /// prototype runs with zero Canvas wiring. Everything is sized relative to
    /// Screen.height ("u" units) because on a Retina phone OnGUI works in raw
    /// pixels — fixed-pixel UI would be microscopic. Production HUD is WS9.
    /// </summary>
    public sealed class HudGui : MonoBehaviour
    {
        private SimulationRunner _runner;
        private SimCore _sim;
        private PlayerInput _input;
        private int _local;

        private int _hits;
        private float _flash;
        private float _popTimer;
        private string _popText = "";
        private GUIStyle _big, _small, _pop, _btn;

        public void Init(SimulationRunner runner, SimCore sim, PlayerInput input, int localIndex)
        {
            _runner = runner; _sim = sim; _input = input; _local = localIndex;
        }

        public void HandleEvent(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Hit when e.ActorId == _local:
                    _hits++; _flash = 0.09f;
                    _popText = e.HitKind == HitKind.Jab ? "JAB!" : "HIT!";
                    _popTimer = 0.22f;
                    if (_input != null && _input.IsTouch) Handheld.Vibrate();
                    break;
                case SimEventType.Jab when e.ActorId == _local:
                    _flash = 0.06f; break;
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
            _big = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _big.normal.textColor = new Color(0.94f, 0.96f, 0.98f);
            _small = new GUIStyle(GUI.skin.label);
            _small.normal.textColor = new Color(0.85f, 0.9f, 0.95f);
            _pop = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _pop.normal.textColor = new Color(1f, 0.82f, 0.29f);
            _btn = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _btn.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            if (_sim == null) return;
            EnsureStyles();
            var p = _sim.Players[_local];
            float W = Screen.width, H = Screen.height;
            float u = H / 900f; // scale unit (design for a 900px-tall reference)

            _big.fontSize = (int)(26 * u);
            _small.fontSize = (int)(19 * u);
            _pop.fontSize = (int)(42 * u);
            _btn.fontSize = (int)(21 * u);

            // crosshair
            float cs = 9 * u;
            var cross = _flash > 0 ? new Color(1f, 0.82f, 0.29f, 1f) : new Color(0.92f, 0.94f, 0.96f, 0.85f);
            DrawRect(new Rect(W / 2 - cs / 2, H / 2 - cs / 2, cs, cs), cross);

            // top-left: hits + bars
            float pad = 22 * u, barW = 300 * u, barH = 24 * u, lblW = 90 * u;
            GUI.Label(new Rect(pad, pad, 500 * u, 32 * u), $"Hits: {_hits}", _big);
            float y = pad + 40 * u;
            GUI.Label(new Rect(pad, y, lblW, barH), "HP", _small);
            DrawBar(new Rect(pad + lblW, y, barW, barH), p.Health / _sim.Config.MaxHealth, new Color(1f, 0.36f, 0.32f));
            y += barH + 8 * u;
            GUI.Label(new Rect(pad, y, lblW, barH), "BUILD", _small);
            DrawBar(new Rect(pad + lblW, y, barW, barH), p.BuildEnergy / _sim.Config.BuildMaxEnergy, new Color(0.37f, 0.69f, 1f));

            // arc toggle indicator (top-right)
            GUI.Label(new Rect(W - 180 * u, pad, 170 * u, 28 * u), _runner.ShowTrajectory ? "arc: on" : "arc: off", _small);

            // charge bar (bottom center) while charging
            if (p.Phase == AttackPhase.Charging && p.ChargeHeldTime > _sim.Config.TapMaxSeconds)
            {
                float power = _sim.ChargePower(p.ChargeHeldTime);
                var r = new Rect(W / 2 - 130 * u, H - 90 * u, 260 * u, 16 * u);
                DrawRect(r, new Color(1f, 1f, 1f, 0.15f));
                DrawRect(new Rect(r.x, r.y, r.width * power, r.height), new Color(1f, 0.82f, 0.29f, 0.95f));
            }

            if (_popTimer > 0) GUI.Label(new Rect(W / 2 - 150 * u, H / 2 - 90 * u, 300 * u, 60 * u), _popText, _pop);
            if (!p.Alive) GUI.Label(new Rect(W / 2 - 150 * u, H / 2 + 40 * u, 300 * u, 34 * u), "respawning...", _pop);

            if (_input != null && _input.IsTouch) DrawTouchControls(u);
            else GUI.Label(new Rect(pad, H - 30 * u, W, 26 * u),
                "WASD move · mouse look · LMB tap=jab hold=throw · Space jump · B build · R rotate · T arc", _small);
        }

        private void DrawTouchControls(float u)
        {
            DrawButton(_input.AttackRect, "THROW\n/JAB", new Color(1f, 0.72f, 0.15f, 0.55f), u);
            DrawButton(_input.JumpRect, "JUMP", new Color(0.30f, 0.62f, 1f, 0.50f), u);
            DrawButton(_input.BuildRect, "BUILD", new Color(0.35f, 0.9f, 0.5f, 0.50f), u);
            DrawButton(_input.RotateRect, "ROT", new Color(0.85f, 0.85f, 0.9f, 0.42f), u);
            if (_input.JoyActive)
            {
                DrawCircle(ToGui(_input.JoyCenter), 55, new Color(1f, 1f, 1f, 0.12f));
                DrawCircle(ToGui(_input.JoyKnob), 27, new Color(1f, 1f, 1f, 0.28f));
            }
        }

        private void DrawButton(Rect screenRect, string label, Color fill, float u)
        {
            var g = ToGui(screenRect);
            DrawRect(g, fill);
            DrawBorder(g, new Color(1f, 1f, 1f, 0.85f), Mathf.Max(2f, 2.5f * u));
            GUI.Label(g, label, _btn);
        }

        // ---- helpers (screen rects are y-up; GUI is y-down) ----
        private Vector2 ToGui(Vector2 screen) => new Vector2(screen.x, Screen.height - screen.y);
        private Rect ToGui(Rect r) => new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);

        private void DrawBar(Rect r, float t, Color c)
        {
            DrawRect(r, new Color(0f, 0f, 0f, 0.45f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), c);
            DrawBorder(r, new Color(1f, 1f, 1f, 0.5f), 2f);
        }

        private void DrawCircle(Vector2 centerGui, float radius, Color c)
            => DrawRect(new Rect(centerGui.x - radius, centerGui.y - radius, radius * 2, radius * 2), c);

        private void DrawBorder(Rect r, Color c, float t)
        {
            DrawRect(new Rect(r.x, r.y, r.width, t), c);
            DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            DrawRect(new Rect(r.x, r.y, t, r.height), c);
            DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

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
