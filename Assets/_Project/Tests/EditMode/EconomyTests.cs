using System;
using System.Collections.Generic;
using CatMerge.Core;
using NUnit.Framework;

namespace CatMerge.Tests
{
    public class WalletTests
    {
        [Test]
        public void AddAndSpend_RaiseOnChangedWithNewBalance()
        {
            var wallet = new Wallet(10);
            var seen = new List<double>();
            wallet.OnChanged += seen.Add;

            wallet.Add(5);
            Assert.IsTrue(wallet.TrySpend(12));

            Assert.AreEqual(3, wallet.Coins);
            CollectionAssert.AreEqual(new[] { 15.0, 3.0 }, seen);
        }

        [Test]
        public void TrySpend_NotEnough_ChangesNothing()
        {
            var wallet = new Wallet(10);
            bool raised = false;
            wallet.OnChanged += _ => raised = true;

            Assert.IsFalse(wallet.TrySpend(10.5));
            Assert.AreEqual(10, wallet.Coins);
            Assert.IsFalse(raised);
        }

        [Test]
        public void InvalidAmounts_Throw()
        {
            var wallet = new Wallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.TrySpend(double.PositiveInfinity));
        }
    }

    public class EconomyTests
    {
        private static Economy Create(double coins, int capacity = 3, int maxLevel = 5)
        {
            var state = new GameState(capacity, maxLevel, shopLevelOffset: 3);
            // Income 1, 3, 9, ...; price 10 * 2^purchased regardless of level.
            return new Economy(state, new Wallet(coins),
                level => Math.Pow(3, level - 1),
                (level, purchased) => 10 * Math.Pow(2, purchased));
        }

        [Test]
        public void TryBuy_SpendsPriceAndRaisesNextPrice()
        {
            var economy = Create(coins: 35);

            Assert.IsTrue(economy.TryBuy(1, out var cat));
            Assert.IsNotNull(cat);
            Assert.AreEqual(25, economy.Wallet.Coins);
            Assert.AreEqual(20, economy.GetPrice(1));
        }

        [Test]
        public void TryBuy_NotEnoughCoins_Fails()
        {
            var economy = Create(coins: 9);

            Assert.IsFalse(economy.CanBuy(1));
            Assert.IsFalse(economy.TryBuy(1, out var cat));
            Assert.IsNull(cat);
            Assert.AreEqual(9, economy.Wallet.Coins);
        }

        [Test]
        public void TryBuy_RoomFull_FailsWithoutSpending()
        {
            var economy = Create(coins: 1000, capacity: 1);
            Assert.IsTrue(economy.TryBuy(1, out _));
            double left = economy.Wallet.Coins;

            Assert.IsFalse(economy.CanBuy(1));
            Assert.IsFalse(economy.TryBuy(1, out _));
            Assert.AreEqual(left, economy.Wallet.Coins);
        }

        [Test]
        public void IncomePerSec_FollowsCatsAndMerges()
        {
            var state = new GameState(5, 5, 3);
            var economy = new Economy(state, new Wallet(), level => Math.Pow(3, level - 1), (l, p) => 0);
            var seen = new List<double>();
            economy.IncomeChanged += seen.Add;

            state.TryGrantCat(1, out var a);
            state.TryGrantCat(1, out var b);
            state.TryMerge(a.Id, b.Id, out _);

            Assert.AreEqual(3, economy.IncomePerSec);
            CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, seen);
        }
    }
}
