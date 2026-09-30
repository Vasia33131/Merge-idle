using System.Collections.Generic;
using CatMerge.Core;
using UnityEngine;

namespace CatMerge.Data
{
    /// <summary>Upgrades in the order they appear in the panel.</summary>
    [CreateAssetMenu(menuName = "CatMerge/Upgrade Database", fileName = "UpgradeDatabase")]
    public sealed class UpgradeDatabase : ScriptableObject
    {
        [SerializeField] private List<UpgradeConfig> upgrades = new List<UpgradeConfig>();

        public IReadOnlyList<UpgradeConfig> Upgrades => upgrades;

        public void SetUpgrades(IEnumerable<UpgradeConfig> configs)
        {
            upgrades.Clear();
            upgrades.AddRange(configs);
        }

        /// <summary>Core definitions; empty slots are skipped, broken ones are logged and skipped.</summary>
        public List<UpgradeDef> ToDefs()
        {
            var defs = new List<UpgradeDef>(upgrades.Count);
            var ids = new HashSet<string>();
            foreach (var config in upgrades)
            {
                if (config == null) continue;
                if (!ids.Add(config.id ?? string.Empty))
                {
                    Debug.LogError($"[CatMerge] {name}: duplicate upgrade id '{config.id}' in '{config.name}', skipped.", config);
                    continue;
                }
                try
                {
                    defs.Add(config.ToDef());
                }
                catch (System.ArgumentException e)
                {
                    Debug.LogError($"[CatMerge] Upgrade '{config.name}' is invalid, skipped: {e.Message}", config);
                }
            }
            return defs;
        }

        public UpgradeConfig Get(string id)
        {
            foreach (var config in upgrades)
            {
                if (config != null && config.id == id) return config;
            }
            return null;
        }
    }
}
