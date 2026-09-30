using System;

namespace CatMerge.Core
{
    /// <summary>
    /// Economy formulas. Pure functions so they can be unit-tested and reused by editor tools.
    /// </summary>
    public static class BalanceMath
    {
        /// <summary>Income of a level-<paramref name="level"/> cat: base * multiplier^(level-1).</summary>
        public static double IncomePerSec(double baseIncome, double multiplier, int level)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            return baseIncome * Math.Pow(multiplier, level - 1);
        }

        /// <summary>How many level-1 kittens are merged into one cat of this level: 2^(level-1).</summary>
        public static long KittensForLevel(int level)
        {
            if (level < 1 || level > 63) throw new ArgumentOutOfRangeException(nameof(level));
            return 1L << (level - 1);
        }

        /// <summary>
        /// Shop price of a level-<paramref name="level"/> kitten after <paramref name="purchased"/> kittens of
        /// that level were bought: base * levelMultiplier^(level-1) * growth^purchased. Each level has its own counter.
        /// </summary>
        public static double KittenPrice(double basePrice, double levelMultiplier, double growth, int level, long purchased)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (purchased < 0) throw new ArgumentOutOfRangeException(nameof(purchased));
            return Math.Round(basePrice * Math.Pow(levelMultiplier, level - 1) * Math.Pow(growth, purchased));
        }
    }
}
