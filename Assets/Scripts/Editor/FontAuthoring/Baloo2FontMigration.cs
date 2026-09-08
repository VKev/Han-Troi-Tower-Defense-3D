using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow.Editor
{
    /// <summary>One-time project migration to the Baloo 2 UI typeface.</summary>
    public static class Baloo2FontMigration
    {
        private const string AutomaticRunKey = "TowerDefense3D.Baloo2FontMigration.Completed";
        private const string FontRoot = "Assets/Font/Baloo2";
        private const string RegularFontPath = FontRoot + "/Baloo2-Regular.ttf";
        private const string SemiBoldFontPath = FontRoot + "/Baloo2-SemiBold.ttf";
        private const string BoldFontPath = FontRoot + "/Baloo2-Bold.ttf";
        private const string TmpFolder = FontRoot + "/TMP";
        private const string TmpFontPath = TmpFolder + "/Baloo2-SemiBold SDF.asset";
        private const string UiCharacters =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 "
            + "ÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ"
            + "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđ"
            + ".,!?;:-_+*/()[]{}<>#&@%\"'\\|=…";

        private static readonly string[] UiPrefabPaths =
        {
            "Assets/Resources/Prefabs/ApplicationUI.prefab",
            "Assets/Resources/Prefabs/GameplayUI.prefab",
            "Assets/Resources/Prefabs/LevelButton.prefab",
            "Assets/Resources/Prefabs/LoadoutButton.prefab",
            "Assets/Resources/Prefabs/TowerBuildButton.prefab"
        };

        static Baloo2FontMigration()
        {
            if (!EditorPrefs.GetBool(AutomaticRunKey, false))
            {
                EditorApplication.delayCall += RunAutomaticMigration;
            }
        }

        private static void RunAutomaticMigration()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunAutomaticMigration;
                return;
            }

            Migrate();
            EditorPrefs.SetBool(AutomaticRunKey, true);
        }

        [MenuItem("Tools/Tower Defense/Migrate UI Fonts To Baloo 2")]
        public static void MigrateFromMenu()
        {
            Migrate();
        }

        private static void Migrate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureUguiFontImporter(RegularFontPath);
            ConfigureUguiFontImporter(SemiBoldFontPath);
            ConfigureUguiFontImporter(BoldFontPath);
            Font regular = LoadFont(RegularFontPath);
            Font semiBold = LoadFont(SemiBoldFontPath);
            Font bold = LoadFont(BoldFontPath);
            TMP_FontAsset tmpFont = LoadOrCreateTmpFont(semiBold);

            foreach (string prefabPath in UiPrefabPaths)
            {
                ReplaceFontsInPrefab(prefabPath, regular, bold, tmpFont);
            }

            AssetDatabase.SaveAssets();
            RemoveUnusedLegacyFonts();
            AssetDatabase.Refresh();
            Debug.Log("Migrated project-owned UI fonts to Baloo 2.");
        }

        private static Font LoadFont(string path)
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null) throw new InvalidOperationException($"Missing Baloo 2 font at '{path}'.");
            return font;
        }

        private static void ConfigureUguiFontImporter(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TrueTypeFontImporter;
            if (importer == null) throw new InvalidOperationException($"Missing font importer at '{path}'.");
            importer.fontSize = 64;
            importer.characterPadding = 4;
            importer.fontRenderingMode = FontRenderingMode.HintedSmooth;
            importer.customCharacters = UiCharacters;
            importer.SaveAndReimport();
        }

        private static TMP_FontAsset LoadOrCreateTmpFont(Font source)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);

            if (!AssetDatabase.IsValidFolder(TmpFolder))
            {
                AssetDatabase.CreateFolder(FontRoot, "TMP");
            }

            if (existing == null)
            {
                existing = TMP_FontAsset.CreateFontAsset(
                    source,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);
                if (existing == null) throw new InvalidOperationException("Could not create the Baloo 2 TMP font asset.");
                AssetDatabase.CreateAsset(existing, TmpFontPath);
            }

            if (existing.atlasTexture == null)
            {
                var atlas = new Texture2D(1024, 1024, TextureFormat.Alpha8, false, true)
                {
                    name = "Baloo2-SemiBold Atlas"
                };
                AssetDatabase.AddObjectToAsset(atlas, existing);
                existing.atlasTextures = new[] { atlas };
            }

            if (existing.material == null)
            {
                Material template = TMP_Settings.defaultFontAsset.material;
                var material = new Material(template)
                {
                    name = "Baloo2-SemiBold SDF Material"
                };
                material.mainTexture = existing.atlasTexture;
                AssetDatabase.AddObjectToAsset(material, existing);
                existing.material = material;
            }

            existing.ReadFontAssetDefinition();
            existing.TryAddCharacters(UiCharacters, out _);

            EditorUtility.SetDirty(existing);
            EditorUtility.SetDirty(existing.material);
            return existing;
        }

        private static void ReplaceFontsInPrefab(
            string prefabPath,
            Font regular,
            Font bold,
            TMP_FontAsset tmpFont)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                {
                    text.font = text.fontStyle == FontStyle.Bold || text.fontStyle == FontStyle.BoldAndItalic
                        ? bold
                        : regular;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    EditorUtility.SetDirty(text);
                }

                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.font = tmpFont;
                    EditorUtility.SetDirty(text);
                }

                foreach (TutorialOverlayView overlay in root.GetComponentsInChildren<TutorialOverlayView>(true))
                {
                    SerializedObject serialized = new SerializedObject(overlay);
                    SerializedProperty font = serialized.FindProperty("instructionFont");
                    if (font != null)
                    {
                        font.objectReferenceValue = regular;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void RemoveUnusedLegacyFonts()
        {
            AssetDatabase.DeleteAsset("Assets/Font/TMP");
            AssetDatabase.DeleteAsset("Assets/Font/static");
            AssetDatabase.DeleteAsset("Assets/Font/CormorantGaramond-Italic-VariableFont_wght.ttf");
            AssetDatabase.DeleteAsset("Assets/Font/CormorantGaramond-VariableFont_wght.ttf");
            AssetDatabase.DeleteAsset("Assets/Font/Montserrat-Italic-VariableFont_wght.ttf");
            AssetDatabase.DeleteAsset("Assets/Font/Montserrat-VariableFont_wght.ttf");
        }
    }
}
