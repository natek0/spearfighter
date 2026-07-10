using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// The custom voxel build editor (WS4). Tap cells on a bounded grid, one Y-layer
    /// (top-down slice) at a time, to author a build shape; SAVE sets it as the local
    /// player's <see cref="PlayerState.BuildTemplate"/> and persists it (PlayerPrefs).
    /// From then on the normal BUILD button places YOUR shape instead of the default
    /// staircase. Greybox IMGUI, matching HudGui. Opening it pauses the sim so you can
    /// build in peace; the same voxel grid is used for mesh + collision + (later)
    /// replication, so what you draw is exactly what gets placed.
    /// </summary>
    public sealed class BuildEditorGui : MonoBehaviour
    {
        private const string PrefKey = "sf_build_template_v1";
        private const int GX = 4, GY = 4, GZ = 4; // default authoring grid

        private SimCore _sim;
        private int _local;
        private SimulationRunner _runner;

        public bool IsOpen { get; private set; }
        private VoxelTemplate _edit;
        private int _layer;
        private GUIStyle _title, _small, _btn;

        public void Init(SimCore sim, int localIndex, SimulationRunner runner)
        {
            _sim = sim; _local = localIndex; _runner = runner;
            // restore a previously-saved template
            var saved = VoxelTemplate.Decode(PlayerPrefs.GetString(PrefKey, ""));
            if (saved != null) _sim.Players[_local].BuildTemplate = saved;
        }

        private void Open()
        {
            var cur = _sim.Players[_local].BuildTemplate;
            _edit = cur != null ? VoxelTemplate.Decode(cur.Encode()) : new VoxelTemplate(GX, GY, GZ);
            if (_edit == null) _edit = new VoxelTemplate(GX, GY, GZ);
            _layer = 0;
            IsOpen = true;
            if (_runner != null) _runner.Paused = true;
        }

        private void Close()
        {
            IsOpen = false;
            if (_runner != null) _runner.Paused = false;
        }

        private void Save()
        {
            var p = _sim.Players[_local];
            if (_edit.IsEmpty)
            {
                p.BuildTemplate = null;                 // empty ⇒ back to default staircase
                PlayerPrefs.SetString(PrefKey, "");
            }
            else
            {
                p.BuildTemplate = VoxelTemplate.Decode(_edit.Encode()); // clone
                PlayerPrefs.SetString(PrefKey, _edit.Encode());
            }
            PlayerPrefs.Save();
            Close();
        }

        private void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title.normal.textColor = new Color(0.95f, 0.97f, 1f);
            _small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = new Color(0.82f, 0.88f, 0.95f);
            _btn = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }

        private void OnGUI()
        {
            if (_sim == null) return;
            EnsureStyles();
            float W = Screen.width, H = Screen.height, u = H / 900f;
            _title.fontSize = (int)(30 * u);
            _small.fontSize = (int)(18 * u);
            _btn.fontSize = (int)(19 * u);

            if (!IsOpen)
            {
                // small launcher, top-centre (clear of the HP / arc clusters)
                if (GUI.Button(new Rect(W / 2 - 85 * u, 14 * u, 170 * u, 42 * u), "EDIT BUILD", _btn)) Open();
                return;
            }

            GUI.depth = -5; // draw the editor above the HUD
            DrawRect(new Rect(0, 0, W, H), new Color(0.05f, 0.06f, 0.09f, 0.92f));

            GUI.Label(new Rect(0, 26 * u, W, 40 * u), "BUILD EDITOR", _title);
            GUI.Label(new Rect(0, 66 * u, W, 26 * u),
                $"tap cells to fill · top-down slice · front row = nearest you   (filled: {_edit.Count})", _small);

            // ---- layer selector ----
            float topY = 110 * u;
            GUI.Label(new Rect(W / 2 - 200 * u, topY, 400 * u, 30 * u), $"Layer  {_layer + 1} / {_edit.SizeY}  (height)", _small);
            if (GUI.Button(new Rect(W / 2 - 200 * u, topY + 30 * u, 90 * u, 40 * u), "▼ lower", _btn))
                _layer = Mathf.Max(0, _layer - 1);
            if (GUI.Button(new Rect(W / 2 + 110 * u, topY + 30 * u, 90 * u, 40 * u), "higher ▲", _btn))
                _layer = Mathf.Min(_edit.SizeY - 1, _layer + 1);

            // ---- the X (width) × Z (depth) grid for the current layer ----
            float cell = 62 * u, gap = 8 * u;
            float gridW = _edit.SizeX * cell + (_edit.SizeX - 1) * gap;
            float gridH = _edit.SizeZ * cell + (_edit.SizeZ - 1) * gap;
            float gx0 = W / 2 - gridW / 2;
            float gy0 = topY + 90 * u;
            for (int z = 0; z < _edit.SizeZ; z++)          // rows: far (top) → near (bottom)
                for (int x = 0; x < _edit.SizeX; x++)      // cols: width
                {
                    int zz = _edit.SizeZ - 1 - z;          // draw far at top, near at bottom
                    var r = new Rect(gx0 + x * (cell + gap), gy0 + z * (cell + gap), cell, cell);
                    bool on = _edit.Get(x, _layer, zz);
                    var old = GUI.backgroundColor;
                    GUI.backgroundColor = on ? new Color(0.95f, 0.45f, 0.72f) : new Color(0.3f, 0.34f, 0.4f);
                    if (GUI.Button(r, "", _btn)) _edit.Toggle(x, _layer, zz);
                    GUI.backgroundColor = old;
                }

            // ---- actions ----
            float by = gy0 + gridH + 22 * u;
            float bw = 150 * u, bh = 46 * u, bgap = 14 * u;
            float bx = W / 2 - (bw * 4 + bgap * 3) / 2;
            if (GUI.Button(new Rect(bx + 0 * (bw + bgap), by, bw, bh), "SAVE & USE", _btn)) Save();
            if (GUI.Button(new Rect(bx + 1 * (bw + bgap), by, bw, bh), "CLEAR", _btn)) _edit.Clear();
            if (GUI.Button(new Rect(bx + 2 * (bw + bgap), by, bw, bh), "USE DEFAULT", _btn))
            {
                _edit.Clear();
                Save(); // empty template ⇒ default staircase
            }
            if (GUI.Button(new Rect(bx + 3 * (bw + bgap), by, bw, bh), "CLOSE", _btn)) Close();
        }

        private static Texture2D _px;
        private static void DrawRect(Rect r, Color c)
        {
            if (_px == null) { _px = new Texture2D(1, 1); _px.SetPixel(0, 0, Color.white); _px.Apply(); }
            var old = GUI.color; GUI.color = c;
            GUI.DrawTexture(r, _px);
            GUI.color = old;
        }
    }
}
