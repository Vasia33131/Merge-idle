using System;
using System.Collections.Generic;
using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class UpgradesTests
    {
        private static readonly UpgradeDef Capacity = new UpgradeDef("cap", UpgradeEffect.RoomCapacity, 2, 100, 2, maxLevel: 3);
        private static readonly UpgradeDef Income = new UpgradeDef("inc", UpgradeEffect.IncomeMultiplier, 0.25, 10, 1, maxLevel: 0);
        private static readonly UpgradeDef OfflineHours = new UpgradeDef("hours", UpgradeEffect.OfflineHours, 1, 10, 1, maxLevel: 5);
        private static readonly UpgradeDef OfflineRate = new UpgradeDef("rate", UpgradeEffect.OfflineRate, 0.1, 10, 1, maxLevel: 10);
        private static readonly UpgradeDef BestKitten = new UpgradeDef("best", UpgradeEffect.BestKitten, 4, 1, 2, maxLevel: 0, unlockAtCatLevel: 6);

        private GameState _state;
        private Economy _economy;
        private Upgrades _upgrades;

        [SetUp]
        public void SetUp()
        {
            _state = new GameState(maxCats: 4, maxLevel: 15, shopLevelOffset: 3);
            // Income 1 per level-1 cat; kitten base price 10 × level.
            _economy = new Economy(_state, new Wallet(1000), level => level, (level, purchased) => 10 * level);
            _upgrades = new Upgrades(new[] { Capacity, Income, OfflineHours, OfflineRate, BestKitten }, _state, _economy,
                baseCapacity: 4, kittenBasePrice: level => 10 * level);
        }

        [Test]
        public void Price_GrowsWithLevel()
        {
            Assert.AreEqual(100, _upgrades.GetPrice(Capacity));
            Assert.IsTrue(_upgrades.TryBuy(Capacity));
            Assert.AreEqual(200, _upgrades.GetPrice(Capacity));
            Assert.AreEqual(900, _economy.Wallet.Coins);
        }

        [Test]
        public void Capacity_AddsSpotsAndStopsAtMaxLevel()
        {
            var seen = new List<int>();
            _state.CapacityChanged += seen.Add;

            Assert.IsTrue(_upgrades.TryBuy(Capacity)); // 100
            Assert.IsTrue(_upgrades.TryBuy(Capacity)); // 200
            Assert.IsTrue(_upgrades.TryBuy(Capacity)); // 400

            Assert.AreEqual(10, _state.MaxCats);
            CollectionAssert.AreEqual(new[] { 6, 8, 10 }, seen);
            Assert.AreEqual(UpgradeStatus.MaxLevel, _upgrades.GetStatus(Capacity));
            Assert.IsFalse(_upgrades.TryBuy(Capacity));
            Assert.AreEqual(300, _economy.Wallet.Coins);
        }

        [Test]
        public void IncomeMultiplier_IsAdditivePerLevel()
        {
            _state.TryGrantCat(1, out _);
            Assert.AreEqual(1, _economy.IncomePerSec);

            _upgrades.TryBuy(Income);
            _upgrades.TryBuy(Income);

            Assert.AreEqual(1.5, _economy.IncomeMultiplier, 1e-9);
            Assert.AreEqual(1.5, _economy.IncomePerSec, 1e-9);
            Assert.AreEqual(1.5, _economy.IncomeOf(_state.Cats[0]), 1e-9);
        }

        [Test]
        public void Offline_AddsHoursAndRateCappedAt100Percent()
        {
            _upgrades.TryBuy(OfflineHours);
            for (int i = 0; i < 8; i++) _upgrades.TryBuy(OfflineRate);

            Assert.AreEqual(2 * 3600 + 3600, _upgrades.OfflineMaxSeconds(2 * 3600));
            Assert.AreEqual(1.0, _upgrades.OfflineRate(0.5), 1e-9);
        }

        [Test]
        public void NothingBought_KeepsBaseValues()
        {
            Assert.AreEqual(4, _state.MaxCats);
            Assert.AreEqual(1, _economy.IncomeMultiplier);
            Assert.AreEqual(0.5, _upgrades.OfflineRate(0.5));
            Assert.AreEqual(7200, _upgrades.OfflineMaxSeconds(7200));
        }

        [Test]
        public void NotEnoughCoins_ChangesNothing()
        {
            var poor = new Economy(_state, new Wallet(50), level => level, (level, purchased) => 10);
            var upgrades = new Upgrades(new[] { Capacity }, _state, poor, 4, level => 10);

            Assert.AreEqual(UpgradeStatus.NotEnoughCoins, upgrades.GetStatus(Capacity));
            Assert.IsFalse(upgrades.TryBuy(Capacity));
            Assert.AreEqual(0, upgrades.GetLevel(Capacity));
            Assert.AreEqual(50, poor.Wallet.Coins);
        }

        [Test]
        public void BestKitten_LockedBeforeLevel6()
        {
            _state.Restore(null, null, maxLevelReached: 5);
            Assert.AreEqual(UpgradeStatus.Locked, _upgrades.GetStatus(BestKitten));
            Assert.IsFalse(_upgrades.TryBuy(BestKitten));
        }

        [Test]
        public void BestKitten_GrantsKittenFourBelowMaxWithoutShopCounter()
        {
            _state.Restore(null, null, maxLevelReached: 7);
            Assert.AreEqual(3, _upgrades.BestKittenLevel(BestKitten));
            Assert.AreEqual(30, _upgrades.GetPrice(BestKitten)); // 1 × base price of L3 (30) × 2^0

            Assert.IsTrue(_upgrades.TryBuy(BestKitten));

            Assert.AreEqual(1, _state.CatCount);
            Assert.AreEqual(3, _state.Cats[0].Level);
            Assert.AreEqual(0, _state.GetPurchaseCount(3));
            Assert.AreEqual(970, _economy.Wallet.Coins);
            Assert.AreEqual(1, _upgrades.GetLevel(BestKitten));
            Assert.AreEqual(60, _upgrades.GetPrice(BestKitten));
        }

        [Test]
        public void BestKitten_RoomFull_CannotBuy()
        {
            _state.Restore(new[] { 1, 1, 1, 1 }, null, maxLevelReached: 8);
            Assert.AreEqual(UpgradeStatus.RoomFull, _upgrades.GetStatus(BestKitten));
            Assert.IsFalse(_upgrades.TryBuy(BestKitten));
            Assert.AreEqual(1000, _economy.Wallet.Coins);
        }

        [Test]
        public void SaveRoundTrip_RestoresLevelsAndEffects()
        {
            _upgrades.TryBuy(Capacity);
            _upgrades.TryBuy(Income);
            var saved = new List<UpgradeSave>();
            _upgrades.WriteTo(saved);

            var state = new GameState(4, 15, 3);
            var economy = new Economy(state, new Wallet(), level => level, (level, purchased) => 10);
            var restored = new Upgrades(new[] { Capacity, Income, BestKitten }, state, economy, 4, level => 10);
            restored.Restore(saved);

            Assert.AreEqual(2, saved.Count);
            Assert.AreEqual(1, restored.GetLevel(Capacity));
            Assert.AreEqual(1, restored.GetLevel(Income));
            Assert.AreEqual(0, restored.GetLevel(BestKitten));
            Assert.AreEqual(6, state.MaxCats);
            Assert.AreEqual(1.25, economy.IncomeMultiplier, 1e-9);
        }

        [Test]
        public void Restore_IgnoresUnknownIdsAndClampsLevels()
        {
            _upgrades.Restore(new[]
            {
                new UpgradeSave { id = "removed", level = 5 },
                new UpgradeSave { id = "cap", level = 99 },
                new UpgradeSave { id = "inc", level = -3 },
                null,
            });

            Assert.AreEqual(3, _upgrades.GetLevel(Capacity));
            Assert.AreEqual(0, _upgrades.GetLevel(Income));
            Assert.AreEqual(10, _state.MaxCats);
        }

        [Test]
        public void RestoredCapacity_KeepsCatsBeyondBaseCapacity()
        {
            _upgrades.Restore(new[] { new UpgradeSave { id = "cap", level = 1 } });
            _state.Restore(new[] { 1, 1, 1, 1, 1, 1 }, null, 1);

            Assert.AreEqual(6, _state.CatCount);
        }

        [Test]
        public void DuplicateIds_Throw()
        {
            var twin = new UpgradeDef("cap", UpgradeEffect.IncomeMultiplier, 1, 1, 1, 1);
            Assert.Throws<ArgumentException>(() => new Upgrades(new[] { Capacity, twin }, _state, _economy, 4, l => 1));
        }
    }
}
