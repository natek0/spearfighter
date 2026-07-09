using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// First-person viewmodel: a greybox right arm holding a spear in the lower-right
    /// of the view, so you can see your weapon (foundation for skins) and the aim arc
    /// clearly emanates from the spear tip rather than screen center.
    ///
    /// Rendered by the main camera (parented to it). A previous version used a URP
    /// overlay camera to avoid wall-clipping, but that API threw at runtime and broke
    /// startup, so this is the robust version — minor clipping when hugging a wall is
    /// acceptable for a greybox; the no-clip overlay can be revisited carefully later.
    /// </summary>
    public sealed class ViewmodelRig : MonoBehaviour
    {
        private GameObject _spear;
        private float _hideTimer;

        public void Init(Camera baseCamera, SimConfig cfg)
        {
            float mr = cfg.MuzzleRight, mu = cfg.MuzzleUp, mf = cfg.MuzzleForward;
            var armColor = new Color(0.78f, 0.6f, 0.5f);
            var spearColor = new Color(0.91f, 0.89f, 0.78f);
            var t = baseCamera.transform;

            // spear: thin cylinder pointing mostly forward, tip near the muzzle
            _spear = MakePrimitive(PrimitiveType.Cylinder, t, spearColor);
            _spear.name = "HeldSpear";
            _spear.transform.localScale = new Vector3(0.03f, 0.5f, 0.03f); // ~1.0 m long
            _spear.transform.localRotation = Quaternion.Euler(80f, 5f, 0f);
            _spear.transform.localPosition = new Vector3(mr, mu, mf);

            // forearm + hand: greybox grip, lower-right, in front of the near plane
            var forearm = MakePrimitive(PrimitiveType.Cube, t, armColor);
            forearm.name = "Forearm";
            forearm.transform.localScale = new Vector3(0.09f, 0.09f, 0.34f);
            forearm.transform.localRotation = Quaternion.Euler(10f, -14f, 0f);
            forearm.transform.localPosition = new Vector3(mr + 0.05f, mu - 0.11f, mf - 0.28f);

            var hand = MakePrimitive(PrimitiveType.Cube, t, armColor);
            hand.name = "Hand";
            hand.transform.localScale = new Vector3(0.11f, 0.1f, 0.12f);
            hand.transform.localPosition = new Vector3(mr, mu - 0.03f, mf - 0.13f);
        }

        /// <summary>Call when the local player throws — briefly hide the held spear.</summary>
        public void OnThrow() => _hideTimer = 0.28f;

        private void Update()
        {
            if (_spear == null) return;
            if (_hideTimer > 0f)
            {
                _hideTimer -= Time.deltaTime;
                if (_spear.activeSelf) _spear.SetActive(false);
            }
            else if (!_spear.activeSelf) _spear.SetActive(true);
        }

        private static GameObject MakePrimitive(PrimitiveType type, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<UnityEngine.Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.GetComponent<Renderer>().material = Mats.New(color);
            return go;
        }
    }
}
