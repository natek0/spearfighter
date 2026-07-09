using UnityEngine;

namespace Spearfighter.Game
{
    /// <summary>
    /// Robust runtime materials for a URP build.
    ///
    /// Two traps this avoids:
    ///  1. GameObject.CreatePrimitive() gives objects the BUILT-IN default material,
    ///     which URP cannot render -> everything shows up magenta/pink on device.
    ///     So we always ASSIGN a fresh URP material, never recolor the default.
    ///  2. A shader can be stripped from a player build, so we resolve a valid lit
    ///     shader once and fall back through options that are guaranteed present.
    /// </summary>
    public static class Mats
    {
        private static Shader _lit;

        private static Shader Lit()
        {
            if (_lit != null) return _lit;
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (_lit == null) _lit = Shader.Find("Sprites/Default"); // always included; unlit but never pink
            if (_lit == null) _lit = Shader.Find("Standard");
            return _lit;
        }

        public static Material New(Color c)
        {
            var m = new Material(Lit());
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        /// <summary>Replace a GameObject's material with a fresh URP one of colour c.</summary>
        public static void Apply(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = New(c);
        }
    }
}
