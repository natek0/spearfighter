using System.Collections.Generic;
using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Pure view layer: every frame it reads the authoritative sim state and moves
    /// Unity Transforms to match. It never writes to the sim. Pools spears and
    /// build meshes; positions the first-person camera on the local player.
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        private SimCore _sim;
        private int _localIndex;
        private Camera _camera;
        private SimConfig _cfg;

        private readonly Dictionary<int, GameObject> _remoteBodies = new();
        private GameObject[] _spears;
        private readonly Dictionary<int, GameObject> _builds = new();
        private GameObject _ghost;

        public void Init(SimCore sim, int localIndex, Camera cam)
        {
            _sim = sim; _localIndex = localIndex; _camera = cam; _cfg = sim.Config;

            _spears = new GameObject[_cfg.MaxSpears];
            for (int i = 0; i < _spears.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = $"Spear{i}";
                DestroyCollider(go);
                go.transform.localScale = new Vector3(0.06f, 0.7f, 0.06f); // cylinder height is 2*scale.y
                SetColor(go, new Color(0.91f, 0.89f, 0.78f));
                go.SetActive(false);
                _spears[i] = go;
            }

            _ghost = MakeGhost();
        }

        public void Render()
        {
            RenderCamera();
            RenderRemotePlayers();
            RenderSpears();
            RenderBuilds();
            RenderGhost();
        }

        private void RenderCamera()
        {
            var p = _sim.Players[_localIndex];
            var eye = p.EyePosition(_cfg.EyeHeight).ToUnity();
            eye.y -= p.StepEaseOffset; // eased step-up: visual eye lags then catches up
            _camera.transform.position = eye;
            // Sim forward at yaw=0 is world -Z; a Unity camera looks down +Z, so the
            // yaw needs a 180deg offset for the camera to face where spears actually go.
            _camera.transform.rotation = Quaternion.Euler(-p.Pitch * Mathf.Rad2Deg, p.Yaw * Mathf.Rad2Deg + 180f, 0f);
        }

        private void RenderRemotePlayers()
        {
            for (int i = 0; i < _sim.Players.Count; i++)
            {
                if (i == _localIndex) continue;
                var p = _sim.Players[i];
                if (!_remoteBodies.TryGetValue(i, out var body))
                {
                    body = MakeEnemy(i);
                    _remoteBodies[i] = body;
                }
                body.SetActive(p.Alive);
                body.transform.position = p.Feet.ToUnity();
                body.transform.rotation = Quaternion.Euler(0f, p.Yaw * Mathf.Rad2Deg + 180f, 0f);
            }
        }

        private void RenderSpears()
        {
            for (int i = 0; i < _spears.Length; i++)
            {
                var s = _sim.Spears[i];
                var go = _spears[i];
                if (!s.Active) { if (go.activeSelf) go.SetActive(false); continue; }
                if (!go.activeSelf) go.SetActive(true);
                go.transform.position = s.Position.ToUnity();
                Vector3 v = s.Velocity.ToUnity();
                if (v.sqrMagnitude > 0.01f)
                    go.transform.rotation = Quaternion.FromToRotation(Vector3.up, v.normalized);
            }
        }

        private void RenderBuilds()
        {
            var live = new HashSet<int>();
            for (int i = 0; i < _sim.Builds.Count; i++)
            {
                var b = _sim.Builds[i];
                live.Add(b.Id);
                if (!_builds.ContainsKey(b.Id))
                {
                    var go = new GameObject($"Build{b.Id}");
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mf.sharedMesh = MeshFactory.BuildVoxels(b.Cells, _cfg.CellSize);
                    mr.material = MakeMat(new Color(0.95f, 0.40f, 0.70f)); // placed build = pink
                    _builds[b.Id] = go;
                }
            }
            // remove evicted builds
            if (_builds.Count > live.Count)
            {
                var stale = new List<int>();
                foreach (var kv in _builds) if (!live.Contains(kv.Key)) stale.Add(kv.Key);
                foreach (var id in stale) { Destroy(_builds[id]); _builds.Remove(id); }
            }
        }

        private void RenderGhost()
        {
            var p = _sim.Players[_localIndex];
            // hold-to-preview: only show the ghost while the BUILD button is held
            if (p != null && p.Alive && p.IsBuildPreviewing &&
                _sim.TryGetBuildPlacement(p, out var cells))
            {
                _ghost.SetActive(true);
                var mf = _ghost.GetComponent<MeshFilter>();
                if (mf.mesh != null) Destroy(mf.mesh);
                mf.mesh = MeshFactory.BuildVoxels(cells, _cfg.CellSize);
            }
            else _ghost.SetActive(false);
        }

        // ---- factory helpers ----
        private GameObject MakeEnemy(int id)
        {
            var root = new GameObject($"Enemy{id}");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            DestroyCollider(body);
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0, 0.9f, 0);
            body.transform.localScale = new Vector3(0.9f, 0.75f, 0.9f);
            SetColor(body, new Color(0.95f, 0.30f, 0.62f)); // NPC = pink
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(head);
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.7f, 0);
            head.transform.localScale = Vector3.one * 0.55f;
            SetColor(head, new Color(1f, 0.55f, 0.78f)); // NPC head = lighter pink
            return root;
        }

        private GameObject MakeGhost()
        {
            var go = new GameObject("BuildGhost");
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.material = Mats.NewTransparent(new Color(0.35f, 0.9f, 1f, 0.35f)); // ghost = translucent cyan
            go.SetActive(false);
            return go;
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<UnityEngine.Collider>();
            if (c != null) Destroy(c); // sim owns collision; Unity colliders are not used
        }

        private static Material MakeMat(Color c) => Mats.New(c);

        private static void SetColor(GameObject go, Color c) => Mats.Apply(go, c);
    }
}
