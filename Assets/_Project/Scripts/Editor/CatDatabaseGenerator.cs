using System.Collections.Generic;
using System.IO;
using CatMerge.Data;
using UnityEditor;
using UnityEngine;

namespace CatMerge.Editor
{
    /// <summary>
    /// Creates/updates BalanceConfig, CatDatabase and one CatConfig per level with placeholder data.
    /// Re-running recalculates income from BalanceConfig and recolors, but keeps custom sprites and meow clips;
    /// empty meow slots are filled from Audio/Meows/Meow_XX.
    /// </summary>
    public static class CatDatabaseGenerator
    {
        public const int CatCount = 15;

        public const string SettingsDir = "Assets/_Project/Settings";
        public const string BalancePath = SettingsDir + "/BalanceConfig.asset";
        public const string DatabasePath = SettingsDir + "/CatDatabase.asset";
        private const string CatsDir = SettingsDir + "/Cats";
        /// <summary>Meow_01.wav … Meow_15.wav here are put into empty meowClip slots (see README there).</summary>
        public const string MeowsDir = AudioMixerGenerator.AudioDir + "/Meows";

        // Cream kitten -> ginger -> pink -> blue -> deep night: light & warm early, dark & rare late.
        private static readonly Gradient LevelGradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color32(0xF2, 0xE8, 0xD5, 0xFF), 0.00f),
                new GradientColorKey(new Color32(0xFF, 0x9F, 0x5A, 0xFF), 0.25f),
                new GradientColorKey(new Color32(0xF4, 0x9C, 0xC8, 0xFF), 0.50f),
                new GradientColorKey(new Color32(0x5C, 0x7C, 0xE0, 0xFF), 0.75f),
                new GradientColorKey(new Color32(0x2E, 0x2A, 0x4A, 0xFF), 1.00f),
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };

        [MenuItem("Tools/CatMerge/Generate Cat Configs")]
        public static void GenerateFromMenu()
        {
            var (database, balance) = Generate();
            Selection.activeObject = database;
            BalanceTableReport.Print(database, balance);
        }

        public static (CatDatabase database, BalanceConfig balance) Generate()
        {
            Directory.CreateDirectory(CatsDir);
            AssetDatabase.Refresh();

            var balance = LoadOrCreate<BalanceConfig>(BalancePath);
            var database = LoadOrCreate<CatDatabase>(DatabasePath);
            var placeholder = GameSceneBuilder.LoadOrCreateCircleSprite();

            var cats = new List<CatConfig>(CatCount);
            for (int level = 1; level <= CatCount; level++)
            {
                var cat = LoadOrCreate<CatConfig>($"{CatsDir}/Cat_{level:00}.asset");
                cat.level = level;
                cat.nameKey = $"cat_{level:00}_name";
                cat.descKey = $"cat_{level:00}_desc";
                cat.color = LevelGradient.Evaluate((level - 1f) / (CatCount - 1f));
                cat.incomePerSec = balance.IncomeForLevel(level);
                if (cat.sprite == null) cat.sprite = placeholder;
                if (cat.meowClip == null) cat.meowClip = GameSceneBuilder.FindFirstClip(MeowsDir, $"Meow_{level:00}");
                EditorUtility.SetDirty(cat);
                cats.Add(cat);
            }

            database.SetCats(cats);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CatMerge] Generated {CatCount} cat configs in {CatsDir}, database: {DatabasePath}.", database);
            return (database, balance);
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
