using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Poi.Editor
{
    public static class PoiPackageValidator
    {
        [MenuItem("Tools/VR Festival Poi/Validate Selected Poi")]
        private static void ValidateSelectedPoi()
        {
            GameObject target = Selection.activeGameObject;
            if (target == null) { Debug.LogWarning("Select a Poi instance first."); return; }
            PoiGrabTarget grab = target.GetComponent<PoiGrabTarget>();
            PoiPaperSurface paper = target.GetComponentInChildren<PoiPaperSurface>(true);
            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body == null || grab == null || grab.AttachTransform == null || paper == null)
            {
                Debug.LogError("Selected object is missing Rigidbody, PoiGrabTarget/GrabAttach, or PoiPaperSurface.", target);
                return;
            }
            Debug.Log("VR Festival Poi validation passed.", target);
        }

        [MenuItem("Tools/VR Festival Poi/Validate Package Assets")]
        public static void ValidatePackageAssets()
        {
            int failures = 0;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Packages/com.vrfestival.poi/Runtime/Prefabs" });
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                failures += CountMissingScripts(prefab);
            }
            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Packages/com.vrfestival.poi/Runtime/Materials" });
            for (int i = 0; i < materialGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) { Debug.LogError("Missing Material or Shader: " + path); failures++; }
                else if (material.name == "PoiPaper" && !material.HasProperty("_DissolveAmount"))
                {
                    Debug.LogError("PoiPaper shader is missing detached-fragment dissolve support: " + path);
                    failures++;
                }
            }
            string[] sceneGuids = AssetDatabase.FindAssets("Demo t:Scene");
            for (int i = 0; i < sceneGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                if (!path.Contains("VR Festival Poi System")) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                foreach (GameObject root in scene.GetRootGameObjects()) failures += CountMissingScripts(root);
                EditorSceneManager.CloseScene(scene, true);
            }
            if (failures > 0) throw new System.InvalidOperationException("VR Festival Poi package validation failed: " + failures + " missing references.");
            Debug.Log("VR Festival Poi package asset validation passed.");
        }

        private static int CountMissingScripts(GameObject root)
        {
            if (root == null) return 1;
            int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            foreach (Transform child in root.transform) missing += CountMissingScripts(child.gameObject);
            if (missing > 0) Debug.LogError("Missing Script under: " + root.name, root);
            return missing;
        }
    }
}
