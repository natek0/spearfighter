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

            SimConfig cfg = config != null ? config.ToSimConfig() : SimConfig.Default();
            var sim = new Simulation(cfg, seed);

            BuildArena(sim);
            var human = sim.AddPlayer(new System.Numerics.Vector3(0, 0, 18f), yaw: 0f);       // faces -Z
            var bot = sim.AddPlayer(new System.Numerics.Vector3(0, 0, -9f), yaw: Mathf.PI);   // faces +Z

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
        }

        private static void BuildArena(Simulation sim)
        {
            // Mirrors the validated prototype greybox: a cover wall forcing arced
            // lobs, plus pillars for depth reference and stick targets.
            AddBox(sim, 0, 1.3f, 0, 12, 2.6f, 0.7f);
            AddBox(sim, -8, 1.6f, -3, 1.2f, 3.2f, 1.2f);
            AddBox(sim, 8, 1.6f, -3, 1.2f, 3.2f, 1.2f);
            AddBox(sim, -6, 1.4f, -16, 1.2f, 2.8f, 1.2f);
            AddBox(sim, 6, 1.4f, -16, 1.2f, 2.8f, 1.2f);
        }

        private static void AddBox(Simulation sim, float cx, float cy, float cz, float sx, float sy, float sz)
        {
            var min = new System.Numerics.Vector3(cx - sx / 2, cy - sy / 2, cz - sz / 2);
            var max = new System.Numerics.Vector3(cx + sx / 2, cy + sy / 2, cz + sz / 2);
            sim.AddStaticBox(min, max);

            // matching visual (Unity collider stripped — sim owns collision)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "ArenaBox";
            var col = go.GetComponent<Collider>(); if (col != null) Destroy(col);
            go.transform.position = new Vector3(cx, cy, cz);
            go.transform.localScale = new Vector3(sx, sy, sz);
            SetColor(go, new Color(0.28f, 0.32f, 0.39f));
        }

        private void BuildEnvironmentVisuals()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            var col = ground.GetComponent<Collider>(); if (col != null) Destroy(col);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20, 1, 20); // plane is 10u => 200u
            SetColor(ground, new Color(0.17f, 0.21f, 0.27f));

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
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.13f, 0.19f);
            return cam;
        }

        private static void SetColor(GameObject go, Color c)
        {
            var m = go.GetComponent<Renderer>().material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

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
