using System;

namespace CatMerge.Core
{
    public struct EconomyParams
    {
        public double BaseKittenPrice;
        public double LevelPriceMultiplier;
        public double PriceGrowth;
        public double BaseIncomePerSec;
        public double IncomeMultiplier;
        public double StartCoins;
        public int StartCats;
        public int Capacity;
        public int MaxLevel;
        public int ShopLevelOffset;

        public double Income(int level) => BalanceMath.IncomePerSec(BaseIncomePerSec, IncomeMultiplier, level);

        public double Price(int level, long purchased) =>
            BalanceMath.KittenPrice(BaseKittenPrice, LevelPriceMultiplier, PriceGrowth, level, purchased);
    }

    public sealed class EconomySimResult
    {
        /// <summary>Seconds from start until level i was first reached; NaN if never. Index = level.</summary>
        public double[] ReachedAt;
        /// <summary>Total kittens bought when level i was first reached. Index = level.</summary>
        public long[] PurchasesAt;
        /// <summary>Coins earned from income (start coins excluded) when level i was first reached. Index = level.</summary>
        public double[] EarnedAt;
        /// <summary>Why the run ended early, or null if the max level was reached.</summary>
        public string StopReason;
    }

    /// <summary>
    /// Plays the game on the real <see cref="GameState"/> rules with a simple greedy player:
    /// always buys the highest kitten the shop sells, waits for coins, merges everything instantly.
    /// Ignores tapping, offline income and prestige, so real players are somewhat slower (no instant merges)
    /// or faster (smarter buying) — treat it as a curve shape check.
    /// </summary>
    public static class EconomySimulator
    {
        public static EconomySimResult Run(EconomyParams p, double maxSeconds = 10 * 365.25 * 86400, int maxPurchases = 1_000_000)
        {
            var state = new GameState(p.Capacity, p.MaxLevel, p.ShopLevelOffset);
            var result = new EconomySimResult
            {
                ReachedAt = new double[p.MaxLevel + 1],
                PurchasesAt = new long[p.MaxLevel + 1],
                EarnedAt = new double[p.MaxLevel + 1],
            };
            for (int i = 0; i <= p.MaxLevel; i++) result.ReachedAt[i] = double.NaN;
            result.ReachedAt[GameState.StartLevel] = 0;

            double time = 0;
            double coins = p.StartCoins;
            double earned = 0;
            long purchases = 0;
            state.LevelReached += level =>
            {
                result.ReachedAt[level] = time;
                result.PurchasesAt[level] = purchases;
                result.EarnedAt[level] = earned;
            };

            for (int i = 0; i < p.StartCats; i++) state.TryGrantCat(GameState.StartLevel, out _);
            MergeAll(state);

            while (state.MaxLevelReached < p.MaxLevel)
            {
                if (purchases >= maxPurchases) { result.StopReason = $"больше {maxPurchases} покупок"; break; }

                int level = ChooseLevel(state);
                if (level == 0) { result.StopReason = "комната забита котами, которые не сливаются"; break; }

                double price = p.Price(level, state.GetPurchaseCount(level));
                if (coins < price)
                {
                    double income = TotalIncome(state, p);
                    if (income <= 0) { result.StopReason = "нет дохода и не хватает монет"; break; }
                    time += (price - coins) / income;
                    earned += price - coins;
                    coins = price;
                    if (time > maxSeconds) { result.StopReason = "дольше лимита симуляции"; break; }
                }

                coins -= price;
                purchases++;
                state.TryBuyCat(level, out _);
                MergeAll(state);
            }

            return result;
        }

        private static int ChooseLevel(GameState state)
        {
            int best = state.MaxBuyableLevel;
            int free = state.MaxCats - state.CatCount;
            if (free >= 2 || (free >= 1 && HasLevel(state, best)) ) return best;

            // Almost full: buy the cheapest kitten that merges right away instead of taking a slot.
            for (int level = GameState.StartLevel; level <= best; level++)
            {
                if (HasLevel(state, level) && state.CanBuy(level)) return level;
            }
            return free >= 1 ? best : 0;
        }

        private static bool HasLevel(GameState state, int level)
        {
            foreach (var cat in state.Cats)
            {
                if (cat.Level == level) return true;
            }
            return false;
        }

        private static void MergeAll(GameState state)
        {
            bool merged;
            do
            {
                merged = false;
                var cats = state.Cats;
                for (int i = 0; i < cats.Count && !merged; i++)
                {
                    for (int j = i + 1; j < cats.Count; j++)
                    {
                        if (state.TryMerge(cats[i].Id, cats[j].Id, out _) != MergeResult.Success) continue;
                        merged = true;
                        break;
                    }
                }
            } while (merged);
        }

        private static double TotalIncome(GameState state, EconomyParams p)
        {
            double sum = 0;
            foreach (var cat in state.Cats) sum += p.Income(cat.Level);
            return sum;
        }
    }
}
