using UnityEditor;
using UnityEngine;

namespace VocaNerd.EditorTools
{
    public static class MaterialGenerator
    {
        private const string MaterialDir = "Assets/Materials";

        [MenuItem("VocaNerd/Create/UI Sparkle Material")]
        public static void CreateUISparkleMaterial()
        {
            var shader = Shader.Find("UI/Sparkle");
            if (shader == null)
            {
                Debug.LogError("[MaterialGenerator] Shader 'UI/Sparkle' not found. Make sure UISparkle.shader is imported.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(MaterialDir))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var path = $"{MaterialDir}/UISparkle.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                Debug.Log($"[MaterialGenerator] UISparkle material already exists at {path}");
                Selection.activeObject = existing;
                return;
            }

            var mat = new Material(shader) { name = "UISparkle" };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = mat;
            Debug.Log($"[MaterialGenerator] Created {path}");
        }
    }
}
