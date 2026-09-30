using System;

namespace CatMerge.Core
{
    public enum UpgradeEffect
    {
        /// <summary>+Value room spots per level.</summary>
        RoomCapacity,
        /// <summary>+Value (0.25 = +25%) to the income multiplier per level, additive: ×1, ×1.25, ×1.5...</summary>
        IncomeMultiplier,
        /// <summary>+Value hours to the offline income limit per level.</summary>
        OfflineHours,
        /// <summary>+Value (0.1 = +10 percentage points) to the offline income rate per level, capped at 100%.</summary>
        OfflineRate,
        /// <summary>
        /// Repeatable purchase instead of a level: a kitten Value levels below the max level reached. The "level"
        /// counts purchases and drives the price; price = BasePrice × that kitten's base shop price × PriceGrowth^count.
        /// </summary>
        BestKitten,
    }

    /// <summary>Static description of one upgrade (Unity-free copy of UpgradeConfig).</summary>
    public sealed class UpgradeDef
    {
        public string Id { get; }
        public UpgradeEffect Effect { get; }
        /// <summary>Effect per level; for <see cref="UpgradeEffect.BestKitten"/> — how many levels below the max.</summary>
        public double Value { get; }
        public double BasePrice { get; }
        public double PriceGrowth { get; }
        /// <summary>0 = no limit.</summary>
        public int MaxLevel { get; }
        /// <summary>Hidden behind a lock until a cat of this level was reached.</summary>
        public int UnlockAtCatLevel { get; }

        public UpgradeDef(string id, UpgradeEffect effect, double value, double basePrice, double priceGrowth,
            int maxLevel, int unlockAtCatLevel = GameState.StartLevel)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Upgrade needs an id.", nameof(id));
            if (!(basePrice >= 0) || double.IsInfinity(basePrice)) throw new ArgumentOutOfRangeException(nameof(basePrice));
            if (!(priceGrowth >= 1) || double.IsInfinity(priceGrowth)) throw new ArgumentOutOfRangeException(nameof(priceGrowth));
            if (maxLevel < 0) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            Id = id;
            Effect = effect;
            Value = value;
            BasePrice = basePrice;
            PriceGrowth = priceGrowth;
            MaxLevel = maxLevel;
            UnlockAtCatLevel = unlockAtCatLevel;
        }

        public bool IsRepeatable => Effect == UpgradeEffect.BestKitten;
    }

    public enum UpgradeStatus
    {
        Available,
        /// <summary>Max cat level reached is below <see cref="UpgradeDef.UnlockAtCatLevel"/>.</summary>
        Locked,
        MaxLevel,
        /// <summary>A kitten purchase with no free spot.</summary>
        RoomFull,
        NotEnoughCoins,
    }
}
