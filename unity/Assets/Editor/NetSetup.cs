#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fusion;
using Spearfighter.Net;

namespace Spearfighter.EditorTools
{
    /// <summary>
    /// One-shot: builds the WS10.0 netcode-test artifacts that can't be authored in
    /// plain C# — the NetPlayer prefab (GameObject + NetworkObject + NetPlayer + a body
    /// visual) and the NetTest scene (a NetLauncher wired to that prefab) — and adds the
    /// scene to Build Settings. Run headless via
    ///   -executeMethod Spearfighter.EditorTools.NetSetup.Run
    /// or from the menu. Fusion bakes the prefab's NetworkObject on import.
    /// </summary>
    public static class NetSetup
    {
        private const string Dir = "Assets/Spearfighter/Net";
        private const string PrefabPath = Dir + "/NetPlayer.prefab";
        private const string ScenePath = Dir + "/NetTest.unity";

        [MenuItem("Spearfighter/Setup Netcode Test")]
        public static void Run()
        {
            // 1) NetPlayer prefab: NetworkObject + NetPlayer + a pink capsule body.
            var root = new GameObject("NetPlayer");
            root.AddComponent<NetworkObject>();
            root.AddComponent<NetPlayer>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(root.transform);
            body.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            body.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            var mr = body.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
                var mat = new Material(shader);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.95f, 0.30f, 0.62f));
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.95f, 0.30f, 0.62f));
                mr.sharedMaterial = mat;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            // 2) NetTest scene: default camera/light + a NetLauncher wired to the prefab.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var launcher = new GameObject("NetLauncher").AddComponent<NetLauncher>();
            launcher.playerPrefab = prefab.GetComponent<NetworkObject>();
            EditorSceneManager.SaveScene(scene, ScenePath);

            // 3) Add NetTest to Build Settings (so device builds include it).
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NetSetup] Done. Prefab={PrefabPath}, Scene={ScenePath}, prefab NetworkObject={(prefab.GetComponent<NetworkObject>() != null)}");
        }
    }
}
#endif
