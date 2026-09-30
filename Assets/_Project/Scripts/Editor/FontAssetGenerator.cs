using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatMerge.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace CatMerge.Editor
{
    /// <summary>
    /// Bakes static TMP font assets (Latin + Cyrillic) from the Nunito TTFs in Assets/_Project/Fonts (SIL OFL, see OFL.txt).
    /// Regular is the main asset; ExtraBold is hooked into its weight table, so FontStyles.Bold uses the real bold face
    /// instead of TMP's faux bold. The character set is ASCII + the Russian alphabet + every character used in
    /// Resources/Localization/*.json, so re-run Tools/CatMerge/Generate Fonts after adding text with new symbols.
    /// Re-running updates the assets in place (same GUIDs), so the scene keeps its references.
    /// </summary>
    public static class FontAssetGenerator
    {
        public const string FontsDir = "Assets/_Project/Fonts";
        public const string RegularPath = FontsDir + "/Nunito SDF.asset";
        public const string BoldPath = FontsDir + "/Nunito ExtraBold SDF.asset";
        private const string RegularTtf = FontsDir + "/Nunito-Regular.ttf";
        private const string BoldTtf = FontsDir + "/Nunito-ExtraBold.ttf";
        private const string LocalizationDir = "Assets/_Project/Resources/Localization";
        private const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

        private const int SamplingPointSize = 56;
        private const int Padding = 6;
        private const int AtlasSize = 1024;

        // Typography used in UI strings and number formatting on top of what the JSON files contain.
        private const string ExtraCharacters = "«»„“”‘’—–…№·•×→←∞−";

        [MenuItem("Tools/CatMerge/Generate Fonts")]
        public static void GenerateFromMenu()
        {
            var font = Generate();
            if (font != null) Selection.activeObject = font;
        }

        /// <summary>Creates or refreshes both font assets; returns the main (Regular) one, or null if the TTFs are missing.</summary>
        public static TMP_FontAsset Generate()
        {
            string characters = CollectCharacters();
            var regular = Bake(RegularTtf, RegularPath, characters);
            var bold = Bake(BoldTtf, BoldPath, characters);
            if (regular == null) return null;

            if (bold != null)
            {
                regular.fontWeightTable[7].regularTypeface = bold; // 700 = what FontStyles.Bold asks for
                EditorUtility.SetDirty(regular);
            }

            // Glyphs Nunito lacks (rare symbols) come from TMP's dynamic LiberationSans instead of showing as boxes.
            var fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            foreach (var asset in new[] { regular, bold })
            {
                if (asset == null || fallback == null) continue;
                asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
                EditorUtility.SetDirty(asset);
            }

            MakeDefault(regular);
            AssetDatabase.SaveAssets();
            return regular;
        }

        private static TMP_FontAsset Bake(string ttfPath, string assetPath, string characters)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogError($"[CatMerge] Font file {ttfPath} not found. Put the Nunito TTFs (Google Fonts, SIL OFL) into {FontsDir}.");
                return null;
            }

            // Bake into a temporary dynamic asset first: its atlas texture can grow freely while it is not on disk.
            var fresh = TMP_FontAsset.CreateFontAsset(font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: false);
            if (fresh == null) return null;
            fresh.TryAddCharacters(characters, out string missing);
            fresh.atlasPopulationMode = AtlasPopulationMode.Static; // also drops the TTF reference, so it stays out of the build
            ReportMissing(font, characters, missing);

            string name = Path.GetFileNameWithoutExtension(assetPath);
            var texture = fresh.atlasTexture;
            var material = fresh.material;
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);

            if (existing == null)
            {
                fresh.name = name;
                texture.name = name + " Atlas";
                material.name = name + " Material";
                AssetDatabase.CreateAsset(fresh, assetPath);
                AssetDatabase.AddObjectToAsset(texture, fresh);
                AssetDatabase.AddObjectToAsset(material, fresh);
                return fresh;
            }

            // Update in place: texts in the scene reference the asset and its material sub-asset.
            var oldTexture = existing.atlasTexture;
            var oldMaterial = existing.material;
            EditorUtility.CopySerialized(fresh, existing);
            existing.name = name;

            oldTexture.Reinitialize(texture.width, texture.height, texture.format, false);
            oldTexture.LoadRawTextureData(texture.GetRawTextureData<byte>());
            oldTexture.Apply(false, false);
            oldMaterial.shader = material.shader;
            oldMaterial.CopyPropertiesFromMaterial(material);
            oldMaterial.SetTexture(ShaderUtilities.ID_MainTex, oldTexture);

            var so = new SerializedObject(existing);
            var textures = so.FindProperty("m_AtlasTextures");
            textures.arraySize = 1;
            textures.GetArrayElementAtIndex(0).objectReferenceValue = oldTexture;
            so.FindProperty("material").objectReferenceValue = oldMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();
            existing.ReadFontAssetDefinition();

            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(fresh);
            EditorUtility.SetDirty(oldTexture);
            EditorUtility.SetDirty(oldMaterial);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void ReportMissing(Font font, string requested, string missing)
        {
            if (string.IsNullOrEmpty(missing))
            {
                Debug.Log($"[CatMerge] {font.name}: baked {requested.Length} characters.");
                return;
            }
            string list = string.Join(" ", missing.Distinct().Select(c => $"{c} (U+{(int)c:X4})"));
            Debug.LogWarning($"[CatMerge] {font.name}: {missing.Length} of {requested.Length} characters not baked " +
                             $"(not in the font, or the {AtlasSize}px atlas is full): {list}. They fall back to LiberationSans.");
        }

        private static string CollectCharacters()
        {
            var set = new SortedSet<char>();
            for (char c = ' '; c <= '~'; c++) set.Add(c);
            for (char c = 'А'; c <= 'я'; c++) set.Add(c);
            set.Add('Ё');
            set.Add('ё');
            foreach (char c in ExtraCharacters) set.Add(c);

            if (Directory.Exists(LocalizationDir))
            {
                foreach (string file in Directory.GetFiles(LocalizationDir, "*.json"))
                {
                    Dictionary<string, string> table;
                    try
                    {
                        table = FlatJson.Parse(File.ReadAllText(file, Encoding.UTF8));
                    }
                    catch (System.FormatException e)
                    {
                        Debug.LogError($"[CatMerge] {file}: {e.Message}");
                        continue;
                    }
                    foreach (string text in table.Values)
                    {
                        foreach (char c in text)
                        {
                            if (!char.IsControl(c) && !char.IsSurrogate(c)) set.Add(c);
                        }
                    }
                }
            }
            return new string(set.ToArray());
        }

        /// <summary>New TMP texts created by hand in the editor get Nunito too.</summary>
        private static void MakeDefault(TMP_FontAsset font)
        {
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var property = so.FindProperty("m_defaultFontAsset");
            if (property == null || property.objectReferenceValue == font) return;
            property.objectReferenceValue = font;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
