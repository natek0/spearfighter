using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using Spearfighter.Game;      // PlayerInput, Mats
using Spearfighter.Simulation; // InputCommand

namespace Spearfighter.Net
{
    /// <summary>
    /// WS10.0 session driver: a code-driven Fusion launcher matching the project's
    /// build-everything-in-code style. HOST (your Mac) creates a room and is the
    /// authority; JOIN (your iPhone) connects to it. It samples local input, ships it
    /// to the tick via OnInput, and (on the authority) spawns a NetPlayer per joined
    /// player. Two peers = a full test; no second person needed.
    /// </summary>
    public sealed class NetLauncher : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Tooltip("The NetPlayer prefab (GameObject with NetworkObject + NetPlayer).")]
        public NetworkObject playerPrefab;

        private NetworkRunner _runner;
        private PlayerInput _input;
        private int _spawnCounter;
        private string _status = "Choose HOST (Mac) or JOIN (iPhone)";
        private GUIStyle _big;

        private void Awake()
        {
            NetArena.Ensure();
            BuildEnvironment();
            _input = gameObject.AddComponent<PlayerInput>();
        }

        private void Update() => _input?.Sample();

        private async void StartGame(GameMode mode)
        {
            _status = $"Starting {mode}…";
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            _runner.AddCallbacks(this);

            var result = await _runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = "spearfighter-test",
                PlayerCount = 2,
            });

            _status = result.Ok ? $"Connected as {mode}" : $"Failed: {result.ShutdownReason}";
        }

        // ---------- session callbacks ----------

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer || playerPrefab == null) return;
            int slot = _spawnCounter++;
            Vector3 pos = new Vector3(0f, 0f, slot % 2 == 0 ? 8f : -8f);
            runner.Spawn(playerPrefab, pos, Quaternion.identity, player,
                (r, obj) => obj.GetComponent<NetPlayer>().SpawnSlot = slot);
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            InputCommand cmd = _input != null ? _input.Consume() : InputCommand.Empty;
            input.Set(new NetInput
            {
                Move = new Vector2(cmd.Move.X, cmd.Move.Y),
                LookYawDelta = cmd.LookYawDelta,
                LookPitchDelta = cmd.LookPitchDelta,
                Jump = cmd.JumpHeld,
                Attack = cmd.AttackHeld,
                Build = cmd.BuildHeld,
            });
        }

        // ---------- IMGUI: host / join ----------

        private void OnGUI()
        {
            if (_big == null)
            {
                _big = new GUIStyle(GUI.skin.button) { fontSize = 26, fontStyle = FontStyle.Bold };
            }
            GUI.Label(new Rect(24, 20, Screen.width - 40, 40), _status);

            bool running = _runner != null && _runner.IsRunning;
            if (running) return;

            float w = 300, h = 90, cx = Screen.width / 2f, cy = Screen.height / 2f;
            if (GUI.Button(new Rect(cx - w - 20, cy - h / 2, w, h), "HOST\n(Mac)", _big)) StartGame(GameMode.Host);
            if (GUI.Button(new Rect(cx + 20, cy - h / 2, w, h), "JOIN\n(iPhone)", _big)) StartGame(GameMode.Client);
        }

        // ---------- environment (flat ground + light + camera) ----------

        private void BuildEnvironment()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "NetGround";
            var col = ground.GetComponent<Collider>(); if (col != null) Destroy(col);
            ground.transform.position = new Vector3(0f, -0.02f, 0f);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            Mats.Apply(ground, new Color(0.13f, 0.14f, 0.16f));

            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var l = new GameObject("Sun").AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(1f, 0.95f, 0.84f);
                l.intensity = 1.1f;
                l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.fieldOfView = 74f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
        }

        // ---------- required no-op callbacks ----------

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    }
}
