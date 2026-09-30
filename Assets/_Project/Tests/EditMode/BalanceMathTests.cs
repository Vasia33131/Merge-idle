using System;
using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class BalanceMathTests
    {
        [Test]
        public void Income_GrowsByMultiplierPerLevel()
        {
            Assert.AreEqual(1.0, BalanceMath.IncomePerSec(1, 2.6, 1), 1e-9);
            Assert.AreEqual(2.6, BalanceMath.IncomePerSec(1, 2.6, 2), 1e-9);
            Assert.AreEqual(6.76, BalanceMath.IncomePerSec(1, 2.6, 3), 1e-9);
        }

        [Test]
        public void KittensForLevel_IsPowerOfTwo()
        {
            Assert.AreEqual(1, BalanceMath.KittensForLevel(1));
            Assert.AreEqual(2, BalanceMath.KittensForLevel(2));
            Assert.AreEqual(16384, BalanceMath.KittensForLevel(15));
        }

        [Test]
        public void KittenPrice_ScalesByLevelAndPurchases()
        {
            Assert.AreEqual(10, BalanceMath.KittenPrice(10, 5, 1.12, 1, 0));
            Assert.AreEqual(11, BalanceMath.KittenPrice(10, 5, 1.12, 1, 1));
            Assert.AreEqual(250, BalanceMath.KittenPrice(10, 5, 1.12, 3, 0));
            Assert.AreEqual(Math.Round(250 * 1.12 * 1.12), BalanceMath.KittenPrice(10, 5, 1.12, 3, 2));
        }

        [Test]
        public void Simulator_ReachesMaxLevel_WithSlowerStepsLater()
        {
            var p = new EconomyParams
            {
                BaseKittenPrice = 10, LevelPriceMultiplier = 5, PriceGrowth = 1.12,
                BaseIncomePerSec = 1, IncomeMultiplier = 2.6,
                StartCoins = 50, StartCats = 3, Capacity = 12, MaxLevel = 15, ShopLevelOffset = 3,
            };
            var r = EconomySimulator.Run(p);

            Assert.IsNull(r.StopReason);
            Assert.IsFalse(double.IsNaN(r.ReachedAt[15]));
            for (int level = 2; level <= 15; level++)
                Assert.GreaterOrEqual(r.ReachedAt[level], r.ReachedAt[level - 1], $"level {level}");
            Assert.Greater(r.ReachedAt[15] - r.ReachedAt[14], r.ReachedAt[10] - r.ReachedAt[9]);
        }
    }
}
