using CatMerge.Core;
using UnityEngine;

namespace CatMerge.Data
{
    [CreateAssetMenu(menuName = "CatMerge/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Kitten price")]
        [Tooltip("Price of the very first level-1 kitten.")]
        public double baseKittenPrice = 10;
        [Tooltip("A level-N kitten's base price is baseKittenPrice * this^(N-1). Keep it well above incomeMultiplier, " +
                 "otherwise the shop out-earns merging and the game is over in minutes.")]
        public double levelPriceMultiplier = 5;
        [Tooltip("Each purchase of a level multiplies that level's next price by this (separate counter per level).")]
        public double priceGrowth = 1.12;
        [Tooltip("The shop sells kittens up to (max level reached - this). Reached 4 -> buy level 1, reached 7 -> up to 4.")]
        [Min(0)] public int shopLevelOffset = 3;

        [Header("Income")]
        [Tooltip("Coins per second of a level-1 cat.")]
        public double baseIncomePerSec = 1;
        [Tooltip("Each level multiplies income by this. Merging 2 cats into 1 pays off when > 2.")]
        public double incomeMultiplier = 2.6;

        [Header("Offline income")]
        [Tooltip("Share of normal income earned while the game is closed, before upgrades.")]
        [Range(0f, 1f)] public float offlineIncomeRate = 0.5f;
        [Tooltip("Time away beyond this many hours earns nothing (upgrades add hours).")]
        [Min(0f)] public float offlineMaxHours = 2f;
        [Tooltip("Shorter absences (seconds) give nothing and show no popup, so quick tab switches stay quiet.")]
        [Min(0f)] public float offlineMinSeconds = 60f;

        [Header("Start")]
        public double startCoins = 50;
        [Tooltip("Free level-1 kittens on a fresh start so there is something to merge right away.")]
        [Min(0)] public int startCats = 3;

        [Header("Room")]
        [Tooltip("Max cats in the room at once, before the capacity upgrade.")]
        [Min(1)] public int capacity = 12;

        [Header("Moving to a new home (prestige)")]
        [Tooltip("Permanent income bonus per fish: 0.1 = +10%.")]
        public double fishIncomeBonus = 0.1;
        [Tooltip("Coins earned in one run that give 1 fish. Reaching the top cat takes ~1.5e9 (Print Balance Table), " +
                 "so 5e7 gives ~5 fish on the first move.")]
        public double fishBaseCoins = 5e7;
        [Tooltip("Fish = floor((earned / fishBaseCoins)^this), 0.1..1. 0.5: 4x the coins for 2x the fish.")]
        public double fishExponent = 0.5;

        public double IncomeForLevel(int level) => BalanceMath.IncomePerSec(baseIncomePerSec, incomeMultiplier, level);

        public double KittenPrice(int level, long purchased) =>
            BalanceMath.KittenPrice(baseKittenPrice, levelPriceMultiplier, priceGrowth, level, purchased);

        public double OfflineMaxSeconds => offlineMaxHours * 3600.0;

        public EconomyParams ToEconomyParams(int maxLevel) => new EconomyParams
        {
            BaseKittenPrice = baseKittenPrice,
            LevelPriceMultiplier = levelPriceMultiplier,
            PriceGrowth = priceGrowth,
            BaseIncomePerSec = baseIncomePerSec,
            IncomeMultiplier = incomeMultiplier,
            StartCoins = startCoins,
            StartCats = startCats,
            Capacity = capacity,
            MaxLevel = maxLevel,
            ShopLevelOffset = shopLevelOffset,
        };

        private void OnValidate()
        {
            // [Min] doesn't handle double precisely, so clamp here.
            baseKittenPrice = System.Math.Max(0, baseKittenPrice);
            levelPriceMultiplier = System.Math.Max(1, levelPriceMultiplier);
            priceGrowth = System.Math.Max(1, priceGrowth);
            baseIncomePerSec = System.Math.Max(0, baseIncomePerSec);
            incomeMultiplier = System.Math.Max(1, incomeMultiplier);
            startCoins = System.Math.Max(0, startCoins);
            fishIncomeBonus = System.Math.Max(0, fishIncomeBonus);
            fishBaseCoins = System.Math.Max(1, fishBaseCoins);
            fishExponent = System.Math.Min(1, System.Math.Max(0.1, fishExponent));
        }
    }
}
