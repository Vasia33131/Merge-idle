using System.Collections.Generic;
using System.IO;
using CatMerge.Core;
using CatMerge.Data;
using UnityEditor;
using UnityEngine;

namespace CatMerge.Editor
{
    /// <summary>
    /// Creates UpgradeDatabase and the default upgrade configs. Existing configs are left as they are, so tuning in
    /// the inspector survives re-runs; configs the database already has beyond the defaults stay in the list.
    /// </summary>
    public static class UpgradeDatabaseGenerator
    {
        public const string DatabasePath = CatDatabaseGenerator.SettingsDir + "/UpgradeDatabase.asset";
        private const string UpgradesDir = CatDatabaseGenerator.SettingsDir + "/Upgrades";

        private struct Default
        {
            public string Id;
            public UpgradeEffect Effect;
            public double Value;
            public double BasePrice;
            public double PriceGrowth;
            public int MaxLevel;
            public int UnlockAt;
            public Color32 Color;
        }

        // Prices are a first guess against the default BalanceConfig (L1 cat 1/s, x2.6 per level); tune in the assets.
        private static readonly Default[] Defaults =
        {
            // 12 base + 2 × 9 = 30 spots.
            new Default { Id = "room_capacity", Effect = UpgradeEffect.RoomCapacity, Value = 2, BasePrice = 300,
                PriceGrowth = 2.5, MaxLevel = 9, UnlockAt = 1, Color = new Color32(0x9C, 0xD6, 0x8A, 0xFF) },
            new Default { Id = "income_multiplier", Effect = UpgradeEffect.IncomeMultiplier, Value = 0.25, BasePrice = 200,
                PriceGrowth = 1.9, MaxLevel = 20, UnlockAt = 1, Color = new Color32(0xFF, 0xC8, 0x4A, 0xFF) },
            new Default { Id = "offline_hours", Effect = UpgradeEffect.OfflineHours, Value = 1, BasePrice = 1000,
                PriceGrowth = 2.5, MaxLevel = 10, UnlockAt = 1, Color = new Color32(0x8F, 0xA8, 0xE8, 0xFF) },
            // 50% base + 10% × 5 = 100%.
            new Default { Id = "offline_rate", Effect = UpgradeEffect.OfflineRate, Value = 0.1, BasePrice = 800,
                PriceGrowth = 3, MaxLevel = 5, UnlockAt = 1, Color = new Color32(0xB9, 0x9A, 0xE0, 0xFF) },
            // Kitten 4 levels below the best, for its base shop price × 1.1^bought.
            new Default { Id = "best_kitten", Effect = UpgradeEffect.BestKitten, Value = 4, BasePrice = 1,
                PriceGrowth = 1.1, MaxLevel = 0, UnlockAt = 6, Color = Color.white },
        };

        [MenuItem("Tools/CatMerge/Generate Upgrade Configs")]
        public static void GenerateFromMenu() => Selection.activeObject = Generate();

        public static UpgradeDatabase Generate()
        {
            Directory.CreateDirectory(UpgradesDir);
            AssetDatabase.Refresh();

            var database = AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(DatabasePath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<UpgradeDatabase>();
                AssetDatabase.CreateAsset(database, DatabasePath);
            }

            var list = new List<UpgradeConfig>();
            foreach (var d in Defaults)
            {
                string path = $"{UpgradesDir}/Upgrade_{d.Id}.asset";
                var config = AssetDatabase.LoadAssetAtPath<UpgradeConfig>(path);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<UpgradeConfig>();
                    config.id = d.Id;
                    config.nameKey = $"upg_{NameKeyPart(d.Effect)}_name";
                    config.effect = d.Effect;
                    config.value = d.Value;
                    config.basePrice = d.BasePrice;
                    config.priceGrowth = d.PriceGrowth;
                    config.maxLevel = d.MaxLevel;
                    config.unlockAtCatLevel = d.UnlockAt;
                    config.color = d.Color;
                    AssetDatabase.CreateAsset(config, path);
                }
                list.Add(config);
            }

            foreach (var extra in database.Upgrades)
            {
                if (extra != null && !list.Contains(extra)) list.Add(extra);
            }

            database.SetUpgrades(list);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            Debug.Log($"[CatMerge] Upgrade database: {DatabasePath} ({list.Count} upgrades).", database);
            return database;
        }

        private static string NameKeyPart(UpgradeEffect effect)
        {
            switch (effect)
            {
                case UpgradeEffect.RoomCapacity: return "capacity";
                case UpgradeEffect.IncomeMultiplier: return "income";
                case UpgradeEffect.OfflineHours: return "offline_time";
                case UpgradeEffect.OfflineRate: return "offline_rate";
                default: return "best_kitten";
            }
        }
    }
}
