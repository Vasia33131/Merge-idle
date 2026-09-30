using CatMerge.Core;
using UnityEngine;

namespace CatMerge.Data
{
    /// <summary>One upgrade in the upgrades panel. Defaults are created by Tools/CatMerge/Generate Upgrade Configs.</summary>
    [CreateAssetMenu(menuName = "CatMerge/Upgrade Config", fileName = "Upgrade_")]
    public sealed class UpgradeConfig : ScriptableObject
    {
        [Tooltip("Save key. Don't rename after release, or players lose the bought levels.")]
        public string id;
        [Tooltip("Loc key of the display name.")]
        public string nameKey;
        public UpgradeEffect effect;
        [Tooltip("Effect per level: RoomCapacity = spots, IncomeMultiplier = 0.25 for +25%, OfflineHours = hours, " +
                 "OfflineRate = 0.1 for +10%. BestKitten = how many levels below the max level reached.")]
        public double value = 1;
        [Tooltip("Price of level 1. BestKitten: multiplier of that kitten's base shop price (1 = same, 0.8 = 20% off).")]
        public double basePrice = 100;
        [Tooltip("Each bought level (or kitten) multiplies the next price by this.")]
        public double priceGrowth = 2;
        [Tooltip("0 = unlimited.")]
        [Min(0)] public int maxLevel = 10;
        [Tooltip("The row is locked until a cat of this level was reached.")]
        [Min(1)] public int unlockAtCatLevel = 1;
        [Tooltip("Swatch color in the panel.")]
        public Color color = Color.white;
        [Tooltip("Optional pictures of the item this upgrade puts in the room, one per stage (first = first third " +
                 "of the levels...). Empty keeps a tinted placeholder square in 3 stages. 1 unit across = 1 world unit.")]
        public Sprite[] propSprites = new Sprite[0];

        public UpgradeDef ToDef() => new UpgradeDef(id, effect, value, basePrice, priceGrowth, maxLevel, unlockAtCatLevel);

        private void OnValidate()
        {
            basePrice = System.Math.Max(0, basePrice);
            priceGrowth = System.Math.Max(1, priceGrowth);
        }
    }
}
