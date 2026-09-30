using System.Collections.Generic;
using UnityEngine;

namespace CatMerge.Data
{
    /// <summary>All cat levels, index 0 = level 1.</summary>
    [CreateAssetMenu(menuName = "CatMerge/Cat Database", fileName = "CatDatabase")]
    public sealed class CatDatabase : ScriptableObject
    {
        [SerializeField] private List<CatConfig> cats = new List<CatConfig>();

        public IReadOnlyList<CatConfig> Cats => cats;
        public int MaxLevel => cats.Count;

        public CatConfig Get(int level)
        {
            if (level < 1 || level > cats.Count)
            {
                Debug.LogError($"[CatMerge] No CatConfig for level {level} (database has {cats.Count}).");
                return null;
            }
            return cats[level - 1];
        }

        public void SetCats(IEnumerable<CatConfig> configs)
        {
            cats.Clear();
            cats.AddRange(configs);
        }

        private void OnValidate()
        {
            for (int i = 0; i < cats.Count; i++)
            {
                if (cats[i] == null)
                    Debug.LogWarning($"[CatMerge] {name}: slot {i} is empty.", this);
                else if (cats[i].level != i + 1)
                    Debug.LogWarning($"[CatMerge] {name}: slot {i} holds '{cats[i].name}' with level {cats[i].level}, expected {i + 1}.", this);
            }
        }
    }
}
