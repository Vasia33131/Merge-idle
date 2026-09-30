using CatMerge.Data;
using UnityEditor;
using UnityEngine;

namespace CatMerge.Editor
{
    /// <summary>
    /// Creates HomeDatabase with placeholder homes (colors only). An existing database is left as it is, so homes
    /// edited or added in the inspector survive re-runs; delete the asset to get the defaults back.
    /// </summary>
    public static class HomeDatabaseGenerator
    {
        public const string DatabasePath = CatDatabaseGenerator.SettingsDir + "/HomeDatabase.asset";

        // Light walls on purpose: the HUD text drawn over them is dark. The first home matches the original room.
        private static HomeConfig[] Defaults() => new[]
        {
            Home("home_01_name", new Color(0.93f, 0.87f, 0.80f), new Color(0.78f, 0.64f, 0.50f)),
            Home("home_02_name", new Color(0.87f, 0.92f, 0.80f), new Color(0.66f, 0.52f, 0.36f)),
            Home("home_03_name", new Color(0.83f, 0.91f, 0.95f), new Color(0.91f, 0.82f, 0.62f)),
            Home("home_04_name", new Color(0.93f, 0.88f, 0.93f), new Color(0.56f, 0.42f, 0.33f)),
            Home("home_05_name", new Color(0.86f, 0.86f, 0.91f), new Color(0.62f, 0.62f, 0.68f)),
        };

        [MenuItem("Tools/CatMerge/Generate Home Configs")]
        public static void GenerateFromMenu() => Selection.activeObject = Generate();

        public static HomeDatabase Generate()
        {
            var database = AssetDatabase.LoadAssetAtPath<HomeDatabase>(DatabasePath);
            if (database != null) return database;

            database = ScriptableObject.CreateInstance<HomeDatabase>();
            database.SetHomes(Defaults());
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CatMerge] Created {DatabasePath} with {database.Homes.Count} homes.", database);
            return database;
        }

        private static HomeConfig Home(string nameKey, Color wall, Color floor) =>
            new HomeConfig { nameKey = nameKey, wallColor = wall, floorColor = floor };
    }
}
