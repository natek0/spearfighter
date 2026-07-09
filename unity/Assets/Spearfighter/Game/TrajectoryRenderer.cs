using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Dotted trajectory-preview arc (WS3). Pools small dots and places them along
    /// the predicted path while a throw is charging. Prediction uses the SAME
    /// Ballistics.PredictPath the live spear integrates, so the preview is honest.
    /// </summary>
    public sealed class TrajectoryRenderer : MonoBehaviour
    {
        private SimCore _sim;
        private GameObject[] _dots;
        private System.Numerics.Vector3[] _buffer;

        public void Init(SimCore sim)
        {
            _sim = sim;
            int n = sim.Config.TrajectoryMaxPoints;
            _buffer = new System.Numerics.Vector3[n];
            _dots = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var c = d.GetComponent<UnityEngine.Collider>(); if (c != null) Destroy(c);
                d.transform.localScale = Vector3.one * 0.14f;
                d.GetComponent<Renderer>().material = Mats.New(new Color(1f, 0.82f, 0.29f)); // arc = yellow
                d.SetActive(false);
                _dots[i] = d;
            }
        }

        public void Render(int localIndex, bool show)
        {
            var p = _sim.Players[localIndex];
            int count = 0;
            if (show && p.Alive && _sim.TryGetChargePreview(p, out var origin, out var vel, out _))
            {
                count = Ballistics.PredictPath(origin, vel, _sim.Config.SpearGravity,
                    _sim.Config.TrajectoryStepDt, _sim.World, _sim.World.GroundHeight, _buffer);
            }
            for (int i = 0; i < _dots.Length; i++)
            {
                bool on = i < count;
                if (_dots[i].activeSelf != on) _dots[i].SetActive(on);
                if (on) _dots[i].transform.position = _buffer[i].ToUnity();
            }
        }
    }
}
