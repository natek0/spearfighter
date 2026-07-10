using UnityEngine;
using UnityEngine.Rendering;

namespace Spearfighter.Game
{
    /// <summary>
    /// Runtime materials that MATCH THE ACTIVE RENDER PIPELINE.
    ///
    /// The subtle, load-bearing bug this fixes: the project has the URP package but no
    /// URP pipeline ASSET assigned, so the game actually runs on Unity's BUILT-IN
    /// pipeline. Assigning "Universal Render Pipeline/Lit" materials in that situation
    /// renders them through the built-in pipeline, which does NOT run URP's depth pass —
    /// opaque geometry stops writing depth, so the depth buffer is effectively broken
    /// and large surfaces (the ground plane) sort over walls in flat chunks that flip
    /// with the camera. It reads like "the map overlaps itself / ground climbs over
    /// walls," and no amount of moving geometry fixes it.
    ///
    /// So: if a Scriptable Render Pipeline is active, use its Lit shader; otherwise use
    /// the built-in Standard shader (opaque, ZWrite on) — the shader the running
    /// pipeline can actually render with correct depth.
    /// </summary>
    public static class Mats
    {
        private static Shader _lit;

        private static bool SrpActive => GraphicsSettings.currentRenderPipeline != null;

        private static Shader Lit()
        {
            if (_lit != null) return _lit;
            if (SrpActive)
            {
                _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            }
            // Built-in pipeline (current reality) — Standard writes depth correctly.
            if (_lit == null) _lit = Shader.Find("Standard");
            if (_lit == null) _lit = Shader.Find("Legacy Shaders/Diffuse");
            return _lit;
        }

        public static Material New(Color c)
        {
            var m = new Material(Lit());
            SetColor(m, c);
            return m;
        }

        /// <summary>Replace a GameObject's material with a fresh pipeline-correct one.</summary>
        public static void Apply(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = New(c);
        }

        /// <summary>A translucent material (for the build ghost). Sets both URP and
        /// built-in transparency properties so it works under whichever is active.</summary>
        public static Material NewTransparent(Color c)
        {
            var m = new Material(Lit());

            // URP Lit transparent knobs
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // 1 = Transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);     // 0 = Alpha
            if (m.HasProperty("_SURFACE_TYPE_TRANSPARENT")) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // Built-in Standard transparent setup (_Mode = 3 == Transparent)
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // shared blend state
            if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
            m.renderQueue = (int)RenderQueue.Transparent;

            SetColor(m, c);
            return m;
        }

        private static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); // URP
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);         // built-in / URP compat
        }
    }
}
