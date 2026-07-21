using UnityEngine;
using UnityEditor;
using TMPro;

namespace FinalDefense.Setup
{
    public static class ChineseFontSetup
    {
        private const string FontPath = "Assets/_Game/Fonts/HiraginoSansGB.ttc";
        private const string OutputPath = "Assets/_Game/Fonts/HiraginoSansGB SDF.asset";

        [MenuItem("FinalDefense/Setup Chinese Font")]
        public static void SetupChineseFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"Font not found at {FontPath}");
                return;
            }

            // Delete old asset if exists
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null)
            {
                AssetDatabase.DeleteAsset(OutputPath);
            }

            // Create font asset with Dynamic atlas population
            var fontAsset = TMP_FontAsset.CreateFontAsset(font,
                44, 5,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                2048, 2048,
                AtlasPopulationMode.Dynamic);

            if (fontAsset == null)
            {
                Debug.LogError("Failed to create font asset!");
                return;
            }

            fontAsset.name = "HiraginoSansGB SDF";

            // Save the font asset first
            AssetDatabase.CreateAsset(fontAsset, OutputPath);

            // Save atlas texture as sub-asset
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = "HiraginoSansGB SDF Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }

            // Save material as sub-asset
            if (fontAsset.material != null)
            {
                fontAsset.material.name = "HiraginoSansGB SDF Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Set as fallback
            SetAsFallback(fontAsset);

            Debug.Log("Chinese font setup complete! Dynamic SDF font created and set as fallback.");
        }

        private static void SetAsFallback(TMP_FontAsset chineseFont)
        {
            // Add to default font's fallback list
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                if (defaultFont.fallbackFontAssetTable == null)
                    defaultFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

                // Remove any previous entry of the same name
                defaultFont.fallbackFontAssetTable.RemoveAll(f => f == null || f.name == chineseFont.name);
                defaultFont.fallbackFontAssetTable.Add(chineseFont);

                EditorUtility.SetDirty(defaultFont);
                AssetDatabase.SaveAssets();
                Debug.Log("Added Chinese font as fallback to default TMP font (LiberationSans SDF).");
            }
            else
            {
                Debug.LogWarning("Default TMP font not found. Please manually assign the Chinese font in TMP Settings.");
            }
        }
    }
}
