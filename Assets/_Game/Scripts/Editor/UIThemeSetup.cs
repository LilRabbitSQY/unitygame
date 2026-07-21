using UnityEditor;
using UnityEngine;
using FinalDefense.UI;

namespace FinalDefense.Editor
{
    public static class UIThemeSetup
    {
        private const string ResourcesPath = "Assets/Resources";
        private const string ThemePath = "Assets/Resources/UITheme.asset";

        [MenuItem("FinalDefense/UI/Create Default Theme")]
        public static void CreateDefaultTheme()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            var existingTheme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
            if (existingTheme != null)
            {
                Debug.Log("UITheme already exists at " + ThemePath);
                Selection.activeObject = existingTheme;
                return;
            }

            var theme = ScriptableObject.CreateInstance<UITheme>();
            theme.SetDefaultValues();

            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Default UITheme created at " + ThemePath);
            Selection.activeObject = theme;
        }

        [MenuItem("FinalDefense/UI/Setup UI Bootstrap in Scene")]
        public static void SetupUIBootstrapInScene()
        {
            var existingBootstrap = Object.FindFirstObjectByType<UIBootstrap>();
            if (existingBootstrap != null)
            {
                Debug.Log("UIBootstrap already exists in scene");
                Selection.activeObject = existingBootstrap.gameObject;
                return;
            }

            var go = new GameObject("UIBootstrap");
            var bootstrap = go.AddComponent<UIBootstrap>();

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                var serializedObject = new SerializedObject(bootstrap);
                var cameraProp = serializedObject.FindProperty("uiCamera");
                cameraProp.objectReferenceValue = mainCamera;
                serializedObject.ApplyModifiedProperties();
            }

            Debug.Log("UIBootstrap created in scene");
            Selection.activeObject = go;
        }

        [MenuItem("FinalDefense/UI/Apply Theme to All UI")]
        public static void ApplyThemeToAllUI()
        {
            var bootstrap = Object.FindFirstObjectByType<UIBootstrap>();
            if (bootstrap == null)
            {
                var go = new GameObject("UIBootstrap");
                bootstrap = go.AddComponent<UIBootstrap>();
            }

            bootstrap.ApplyThemeToAllUI();
            Debug.Log("Theme applied to all UI elements");
        }
    }
}
