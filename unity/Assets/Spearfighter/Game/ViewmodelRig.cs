using UnityEngine;
using UnityEngine.Rendering.Universal;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// First-person viewmodel: a greybox right arm holding a spear in the lower-right
    /// of the view, so you can see your weapon (foundation for skins) and the aim arc
    /// clearly emanates from the spear tip rather than screen center.
    ///
    /// Rendered by a dedicated URP **overlay camera** on its own layer, stacked on the
    /// main camera. The overlay clears depth, so the arms never clip through walls when
    /// you stand close to geometry — the standard FPS technique.
    /// </summary>
    public sealed class ViewmodelRig : MonoBehaviour
    {
        private const int ViewmodelLayer = 6; // an unused built-in layer

        private GameObject _spear;
        private float _hideTimer;

        public void Init(Camera baseCamera, SimConfig cfg)
        {
            // --- overlay camera stacked on the base camera ---
            var overlayGo = new GameObject("ViewmodelCamera");
            overlayGo.transform.SetParent(baseCamera.transform, false);
            var overlay = overlayGo.AddComponent<Camera>();
            overlay.fieldOfView = baseCamera.fieldOfView; // same FOV so the spear tip lines up with the world arc
            overlay.nearClipPlane = 0.01f;
            overlay.farClipPlane = 5f;

            var baseData = baseCamera.GetUniversalAdditionalCameraData();
            var overlayData = overlay.GetUniversalAdditionalCameraData();
            baseData.renderType = CameraRenderType.Base;
            overlayData.renderType = CameraRenderType.Overlay;
            baseData.cameraStack.Add(overlay);

            overlay.cullingMask = 1 << ViewmodelLayer;      // overlay sees ONLY the viewmodel
            baseCamera.cullingMask &= ~(1 << ViewmodelLayer); // base sees everything else

            // --- geometry, parented to the base camera so it moves with the view ---
            var mr = cfg.MuzzleRight; var mu = cfg.MuzzleUp; var mf = cfg.MuzzleForward;
            var armColor = new Color(0.78f, 0.6f, 0.5f);
            var spearColor = new Color(0.91f, 0.89f, 0.78f);

            // spear: thin cylinder, tip near the muzzle, shaft angled back toward the hand
            _spear = MakePrimitive(PrimitiveType.Cylinder, baseCamera.transform, spearColor);
            _spear.name = "HeldSpear";
            _spear.transform.localScale = new Vector3(0.03f, 0.9f, 0.03f); // ~1.8 m long
            _spear.transform.localRotation = Quaternion.Euler(78f, 6f, 0f); // point mostly forward, slight tilt
            _spear.transform.localPosition = new Vector3(mr, mu, mf - 0.9f);

            // forearm + hand: a couple of boxes at the grip (lower-right, nearer the camera)
            var forearm = MakePrimitive(PrimitiveType.Cube, baseCamera.transform, armColor);
            forearm.name = "Forearm";
            forearm.transform.localScale = new Vector3(0.09f, 0.09f, 0.42f);
            forearm.transform.localRotation = Quaternion.Euler(12f, -14f, 0f);
            forearm.transform.localPosition = new Vector3(mr + 0.06f, mu - 0.12f, mf - 0.55f);

            var hand = MakePrimitive(PrimitiveType.Cube, baseCamera.transform, armColor);
            hand.name = "Hand";
            hand.transform.localScale = new Vector3(0.11f, 0.1f, 0.12f);
            hand.transform.localPosition = new Vector3(mr, mu - 0.03f, mf - 0.32f);
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
            go.layer = ViewmodelLayer;
            go.GetComponent<Renderer>().material = Mats.New(color);
            return go;
        }
    }
}
