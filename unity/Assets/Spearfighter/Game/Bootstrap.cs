using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Spearfighter.Game
{
    /// <summary>
    /// Code-driven scene bootstrap. Builds the greybox arena from primitives, adds
    /// the human + one bot to the simulation, and wires every runtime component
    /// together — so the whole Phase 0/1 loop runs from a single component on an
    /// otherwise empty scene, with no manual prefab/Canvas wiring. This is a
    /// PROTOTYPE harness, not shipping scene structure (that's WS6/WS9 later).
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        [Tooltip("Optional. If empty, validated prototype defaults are used.")]
        public SimConfigAsset config;
        public uint seed = 12345;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft; // FPS plays in landscape

            SimConfig cfg = config != null ? config.ToSimConfig() : SimConfig.Default();
            var sim = new SimCore(cfg, seed);

            // WS11 backend: analytics + remote-config. NullBackend until Firebase is
            // set up (firebase_setup.md); then values from the console live-tune the
            // sim without a rebuild. Init is async, so re-apply once it's ready.
            Backend.Init(onReady: () =>
            {
                ApplyRemoteConfig(sim.Config);
                Backend.Analytics.Log("app_open");
            });
            ApplyRemoteConfig(sim.Config); // apply anything already available (no-op for NullBackend)

            BuildArena(sim);
            // Symmetric spawns: equal distance from the central cover wall (z=0).
            var human = sim.AddPlayer(new System.Numerics.Vector3(0, 0, 15f), yaw: 0f);        // faces -Z
            var bot = sim.AddPlayer(new System.Numerics.Vector3(0, 0, -15f), yaw: Mathf.PI);   // faces +Z

            BuildEnvironmentVisuals();
            var cam = EnsureCamera(cfg.EyeHeight);

            var input = new GameObject("PlayerInput").AddComponent<PlayerInput>();
            var renderer = new GameObject("WorldRenderer").AddComponent<WorldRenderer>();
            var traj = new GameObject("TrajectoryRenderer").AddComponent<TrajectoryRenderer>();
            var hud = gameObject.AddComponent<HudGui>();
            var runner = gameObject.AddComponent<SimulationRunner>();

            runner.Sim = sim;
            runner.LocalIndex = human.Id;
            runner.Input = input;
            runner.Renderer = renderer;
            runner.Trajectory = traj;
            runner.Hud = hud;
            runner.Cam = cam;
            runner.AddBot(bot.Id, new BotBrain(seed ^ 0xABCDu));
            runner.Begin();

            // The viewmodel is a non-essential visual: build it LAST and never let a
            // failure here abort startup (that is what wiped the game last time).
            try
            {
                var viewmodel = new GameObject("ViewmodelRig").AddComponent<ViewmodelRig>();
                viewmodel.Init(cam, cfg);
                runner.Viewmodel = viewmodel;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Spearfighter: viewmodel setup failed; continuing without it. " + e);
            }

            // Custom voxel build editor (WS4). Non-essential to the core loop, so it's
            // built last in its own try/catch and can never abort startup.
            try
            {
                var editor = gameObject.AddComponent<BuildEditorGui>();
                editor.Init(sim, human.Id, runner);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Spearfighter: build editor setup failed; continuing without it. " + e);
            }
        }

        // Arena half-extents (playfield). Bounds walls sit on these; spawns are inside.
        private const float ArenaHalfX = 16f;
        private const float ArenaHalfZ = 20f;

        /// <summary>Override the live SimConfig with any values the backend has fetched
        /// (WS11). No-op with the NullBackend; with Firebase, tuning done in the console
        /// applies here without a rebuild.</summary>
        private static void ApplyRemoteConfig(SimConfig cfg)
        {
            var rc = Backend.RemoteConfig;
            if (rc == null || !rc.IsReady) return;
            var overrides = new Dictionary<string, double>();
            foreach (var key in SimConfigRemote.Keys)
                if (rc.TryGetDouble(key, out double v)) overrides[key] = v;
            if (overrides.Count > 0)
            {
                SimConfigRemote.Apply(cfg, overrides);
                Debug.Log($"[remote-config] applied {overrides.Count} override(s)");
            }
        }

        private static void BuildArena(SimCore sim)
        {
            // Greybox is now SYMMETRIC about z=0 so a 1v1 is fair from either spawn:
            // a central cover wall forcing arced lobs, plus mirrored pillars for depth
            // reference, cover, and stick targets.
            AddBox(sim, 0, 1.3f, 0, 12f, 2.6f, 0.7f);           // central cover wall
            AddBox(sim, -8, 1.6f, 5f, 1.2f, 3.2f, 1.2f);        // mid flanking pillars (mirrored ±Z)
            AddBox(sim, 8, 1.6f, 5f, 1.2f, 3.2f, 1.2f);
            AddBox(sim, -8, 1.6f, -5f, 1.2f, 3.2f, 1.2f);
            AddBox(sim, 8, 1.6f, -5f, 1.2f, 3.2f, 1.2f);
            AddBox(sim, -6, 1.4f, 12f, 1.2f, 2.8f, 1.2f);       // near-spawn cover (mirrored ±Z)
            AddBox(sim, 6, 1.4f, 12f, 1.2f, 2.8f, 1.2f);
            AddBox(sim, -6, 1.4f, -12f, 1.2f, 2.8f, 1.2f);
            AddBox(sim, 6, 1.4f, -12f, 1.2f, 2.8f, 1.2f);

            BuildBounds(sim);
        }

        /// <summary>
        /// A solid perimeter so neither the player nor the bot can wander off the
        /// greybox into the void (WS6 world bounds). Sim-owned walls (same static-box
        /// collision the arena uses); no separate out-of-bounds volume needed because
        /// the floor is flat and the walls are closed.
        /// </summary>
        private static void BuildBounds(SimCore sim)
        {
            const float h = 5f, t = 1f, cy = 2.5f;
            float spanX = ArenaHalfX * 2f + t, spanZ = ArenaHalfZ * 2f + t;
            AddBox(sim, ArenaHalfX, cy, 0, t, h, spanZ, wall: true);   // +X
            AddBox(sim, -ArenaHalfX, cy, 0, t, h, spanZ, wall: true);  // -X
            AddBox(sim, 0, cy, ArenaHalfZ, spanX, h, t, wall: true);   // +Z
            AddBox(sim, 0, cy, -ArenaHalfZ, spanX, h, t, wall: true);  // -Z
        }

        private static void AddBox(SimCore sim, float cx, float cy, float cz, float sx, float sy, float sz, bool wall = false)
        {
            var min = new System.Numerics.Vector3(cx - sx / 2, cy - sy / 2, cz - sz / 2);
            var max = new System.Numerics.Vector3(cx + sx / 2, cy + sy / 2, cz + sz / 2);
            sim.AddStaticBox(min, max);

            // matching visual (Unity collider stripped — sim owns collision)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = wall ? "ArenaBounds" : "ArenaBox";
            var col = go.GetComponent<UnityEngine.Collider>(); if (col != null) Destroy(col);
            go.transform.position = new Vector3(cx, cy, cz);
            go.transform.localScale = new Vector3(sx, sy, sz);
            // bounds walls read a touch darker so they don't compete with cover.
            SetColor(go, wall ? new Color(0.34f, 0.34f, 0.38f) : new Color(0.50f, 0.50f, 0.53f));
        }

        private void BuildEnvironmentVisuals()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            var col = ground.GetComponent<UnityEngine.Collider>(); if (col != null) Destroy(col);
            // Ground a hair below the sim ground plane (y=0) so it isn't exactly
            // coplanar with object bottoms (which would z-fight even with a correct
            // depth buffer). Small enough to be invisible. Sim grounds players at y=0.
            ground.transform.position = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(20, 1, 20); // plane is 10u => 200u
            SetColor(ground, new Color(0.12f, 0.12f, 0.14f)); // ground = dark grey

            if (Object.FindObjectOfType<Light>() == null)
            {
                var lgo = new GameObject("Sun");
                var l = lgo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(1f, 0.95f, 0.84f);
                l.intensity = 1.1f;
                lgo.transform.rotation = Quaternion.Euler(50, -30, 0);
            }
            RenderSettings.ambientLight = new Color(0.42f, 0.46f, 0.52f);
        }

        private Camera EnsureCamera(float eyeHeight)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("MainCamera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.fieldOfView = 74f;                       // prototype FOV
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;                     // was 1000; a tighter far plane keeps depth precision high
            cam.clearFlags = CameraClearFlags.Skybox;              // blue gradient sky
            cam.backgroundColor = new Color(0.30f, 0.52f, 0.80f);  // fallback blue if no skybox
            return cam;
        }

        private static void SetColor(GameObject go, Color c) => Mats.Apply(go, c);

#if UNITY_EDITOR
        [MenuItem("Spearfighter/Create Play Scene")]
        private static void CreatePlayScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            var go = new GameObject("Spearfighter");
            go.AddComponent<Bootstrap>();
            Selection.activeGameObject = go;
            Debug.Log("Spearfighter play scene created. Press Play.");
        }
#endif
    }
}
