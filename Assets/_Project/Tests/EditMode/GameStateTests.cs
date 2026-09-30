using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class GameStateTests
    {
        [Test]
        public void BuyCat_AddsLevelOneCat()
        {
            var state = new GameState(12, 15, 3);
            Assert.IsTrue(state.TryBuyCat(1, out var cat));
            Assert.AreEqual(1, cat.Level);
            Assert.AreEqual(1, state.CatCount);
        }

        [Test]
        public void BuyCat_FailsWhenFull()
        {
            var state = new GameState(2, 15, 3);
            state.TryBuyCat(1, out _);
            state.TryBuyCat(1, out _);
            Assert.IsTrue(state.IsFull);
            Assert.IsFalse(state.TryBuyCat(1, out var cat));
            Assert.IsNull(cat);
            Assert.AreEqual(2, state.CatCount);
        }

        [Test]
        public void Merge_SameLevel_ProducesNextLevelAndFreesSlot()
        {
            var state = new GameState(12, 15, 3);
            state.TryBuyCat(1, out var a);
            state.TryBuyCat(1, out var b);
            CatData merged = null;
            state.CatsMerged += (_, __, r) => merged = r;

            Assert.AreEqual(MergeResult.Success, state.TryMerge(a.Id, b.Id, out var result));
            Assert.AreEqual(2, result.Level);
            Assert.AreSame(result, merged);
            Assert.AreEqual(1, state.CatCount);
            Assert.IsNull(state.Find(a.Id));
            Assert.IsNull(state.Find(b.Id));
        }

        [Test]
        public void Merge_DifferentLevels_Fails()
        {
            var state = new GameState(12, 15, 3);
            state.TryBuyCat(1, out var a);
            state.TryBuyCat(1, out var b);
            state.TryMerge(a.Id, b.Id, out var level2);
            state.TryBuyCat(1, out var c);

            Assert.AreEqual(MergeResult.DifferentLevels, state.TryMerge(c.Id, level2.Id, out _));
            Assert.AreEqual(2, state.CatCount);
        }

        [Test]
        public void Merge_WithItself_Fails()
        {
            var state = new GameState(12, 15, 3);
            state.TryBuyCat(1, out var a);
            Assert.AreEqual(MergeResult.SameCat, state.TryMerge(a.Id, a.Id, out _));
        }

        [Test]
        public void Merge_AtMaxLevel_Fails()
        {
            var state = new GameState(12, 1, 3);
            state.TryBuyCat(1, out var a);
            state.TryBuyCat(1, out var b);
            Assert.AreEqual(MergeResult.MaxLevelReached, state.TryMerge(a.Id, b.Id, out _));
            Assert.AreEqual(2, state.CatCount);
        }

        [TestCase(1, 1)]
        [TestCase(4, 1)]
        [TestCase(5, 2)]
        [TestCase(7, 4)]
        [TestCase(15, 12)]
        public void Shop_SellsUpToThreeLevelsBelowMaxReached(int reached, int buyable)
        {
            Assert.AreEqual(buyable, ShopRules.MaxBuyableLevel(reached, 3));
        }

        [Test]
        public void BuyCat_AboveShopLimit_Fails()
        {
            var state = new GameState(12, 15, 3);
            state.TryGrantCat(4, out _);
            Assert.AreEqual(4, state.MaxLevelReached);
            Assert.AreEqual(1, state.MaxBuyableLevel);
            Assert.IsFalse(state.TryBuyCat(2, out _));
            Assert.IsTrue(state.TryBuyCat(1, out _));
        }

        [Test]
        public void Merge_UnlocksHigherShopLevel()
        {
            var state = new GameState(12, 15, 3);
            state.TryGrantCat(4, out var a);
            state.TryGrantCat(4, out var b);
            int reached = 0;
            state.LevelReached += level => reached = level;

            state.TryMerge(a.Id, b.Id, out _);

            Assert.AreEqual(5, reached);
            Assert.AreEqual(2, state.MaxBuyableLevel);
            Assert.IsTrue(state.TryBuyCat(2, out _));
        }

        [Test]
        public void MaxLevelReached_StaysAfterCatIsMergedAway()
        {
            var state = new GameState(12, 15, 3);
            state.TryGrantCat(5, out var a);
            state.TryGrantCat(5, out var b);
            state.TryMerge(a.Id, b.Id, out _);
            Assert.AreEqual(6, state.MaxLevelReached);
            Assert.AreEqual(3, state.MaxBuyableLevel);
        }

        [Test]
        public void PurchaseCounters_ArePerLevel_AndGrantsDontCount()
        {
            var state = new GameState(12, 15, 3);
            state.TryGrantCat(1, out _);
            state.TryBuyCat(1, out _);
            state.TryBuyCat(1, out _);
            Assert.AreEqual(2, state.GetPurchaseCount(1));
            Assert.AreEqual(0, state.GetPurchaseCount(2));
        }
    }
}
