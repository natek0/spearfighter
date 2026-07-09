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
        private Simulation _sim;
        private int _localIndex;
        private Camera _camera;
        private SimConfig _cfg;

        private readonly Dictionary<int, GameObject> _remoteBodies = new();
        private GameObject[] _spears;
        private readonly Dictionary<int, GameObject> _builds = new();
        private GameObject _ghost;

        public void Init(Simulation sim, int localIndex, Camera cam)
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
            _camera.transform.position = p.EyePosition(_cfg.EyeHeight).ToUnity();
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
                    mf.sharedMesh = MeshFactory.BuildRamp(b.Min, b.Max, b.RampAxis);
                    mr.material = MakeMat(new Color(0.42f, 0.47f, 0.58f));
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
            if (p != null && p.Alive &&
                _sim.TryGetBuildPlacement(p, out var min, out var max, out var axis) &&
                _sim.CanPlaceBuild(p, min, max))
            {
                _ghost.SetActive(true);
                var mf = _ghost.GetComponent<MeshFilter>();
                if (mf.mesh != null) Destroy(mf.mesh);
                mf.mesh = MeshFactory.BuildRamp(min, max, axis);
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
            SetColor(body, new Color(1f, 0.42f, 0.35f));
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(head);
            head.transform.SetParent(root.transform);
            head.transform.localPosition = new Vector3(0, 1.7f, 0);
            head.transform.localScale = Vector3.one * 0.55f;
            SetColor(head, new Color(1f, 0.69f, 0.65f));
            return root;
        }

        private GameObject MakeGhost()
        {
            var go = new GameObject("BuildGhost");
            go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var m = MakeMat(new Color(0.4f, 0.85f, 1f, 0.35f));
            m.SetFloat("_Surface", 1f); // URP transparent, ignored elsewhere
            mr.material = m;
            go.SetActive(false);
            return go;
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) Destroy(c); // sim owns collision; Unity colliders are not used
        }

        private static Material MakeMat(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh);
            SetMatColor(m, c);
            return m;
        }

        private static void SetColor(GameObject go, Color c) => SetMatColor(go.GetComponent<Renderer>().material, c);

        private static void SetMatColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
    }
}
