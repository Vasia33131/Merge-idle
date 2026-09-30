using System.Collections.Generic;
using CatMerge.Core;
using NUnit.Framework;
using UnityEngine;

namespace CatMerge.Tests
{
    public class OfflineIncomeTests
    {
        [Test]
        public void ElapsedSeconds_ClockTurnedBack_IsZero()
        {
            Assert.AreEqual(0, OfflineIncome.ElapsedSeconds(lastSeenUnix: 1000, nowUnix: 900));
            Assert.AreEqual(0, OfflineIncome.ElapsedSeconds(lastSeenUnix: 1000, nowUnix: 1000));
            Assert.AreEqual(0, OfflineIncome.ElapsedSeconds(lastSeenUnix: 0, nowUnix: 1000), "no previous time");
            Assert.AreEqual(250, OfflineIncome.ElapsedSeconds(lastSeenUnix: 1000, nowUnix: 1250));
        }

        [Test]
        public void NextLastSeen_NeverMovesBackwards()
        {
            Assert.AreEqual(2000, OfflineIncome.NextLastSeen(2000, 1500));
            Assert.AreEqual(2500, OfflineIncome.NextLastSeen(2000, 2500));
        }

        [Test]
        public void Calculate_AppliesRateAndCap()
        {
            const double twoHours = 7200;
            Assert.AreEqual(10 * 0.5 * 600, OfflineIncome.Calculate(10, 600, 0.5, twoHours));
            Assert.AreEqual(10 * 0.5 * twoHours, OfflineIncome.Calculate(10, 100000, 0.5, twoHours));
        }

        [Test]
        public void Calculate_NothingToPay_IsZero()
        {
            Assert.AreEqual(0, OfflineIncome.Calculate(0, 600, 0.5, 7200));
            Assert.AreEqual(0, OfflineIncome.Calculate(10, -5, 0.5, 7200));
            Assert.AreEqual(0, OfflineIncome.Calculate(10, double.NaN, 0.5, 7200));
            Assert.AreEqual(0, OfflineIncome.Calculate(10, 600, 0, 7200));
        }
    }

    public class GameStateRestoreTests
    {
        [Test]
        public void Restore_AddsCatsInOrderAndKeepsCounters()
        {
            var state = new GameState(12, 15, 3);
            var added = new List<int>();
            int reachedEvents = 0;
            state.CatAdded += cat => added.Add(cat.Level);
            state.LevelReached += _ => reachedEvents++;

            state.Restore(new[] { 3, 1, 5 }, new long[] { 0, 7, 2 }, maxLevelReached: 6);

            CollectionAssert.AreEqual(new[] { 3, 1, 5 }, added);
            Assert.AreEqual(6, state.MaxLevelReached);
            Assert.AreEqual(7, state.GetPurchaseCount(1));
            Assert.AreEqual(2, state.GetPurchaseCount(2));
            Assert.AreEqual(0, state.GetPurchaseCount(3));
            Assert.AreEqual(0, reachedEvents);
        }

        [Test]
        public void Restore_RepairsBadValues()
        {
            var state = new GameState(2, 5, 3);
            state.Restore(new[] { 0, 99, 1 }, new long[] { 0, -4 }, maxLevelReached: -1);

            Assert.AreEqual(2, state.CatCount, "extra cats beyond capacity are dropped");
            Assert.AreEqual(1, state.Cats[0].Level);
            Assert.AreEqual(5, state.Cats[1].Level);
            Assert.AreEqual(5, state.MaxLevelReached, "raised to the best restored cat");
            Assert.AreEqual(0, state.GetPurchaseCount(1));
        }

        [Test]
        public void Restore_IntoNonEmptyState_Throws()
        {
            var state = new GameState(12, 15, 3);
            state.TryGrantCat(1, out _);
            Assert.Throws<System.InvalidOperationException>(() => state.Restore(new[] { 1 }, null, 1));
        }
    }

    public class SaveDataJsonTests
    {
        [Test]
        public void JsonRoundTrip_KeepsEverything()
        {
            var data = new SaveData
            {
                coins = 1.5e30,
                maxLevelReached = 9,
                purchases = new long[] { 0, 12, 3 },
                lastSeenUnix = 1790000000,
            };
            data.cats.Add(new CatSave { level = 4, x = 1.25f, y = -2f });
            data.upgrades.Add(new UpgradeSave { id = "income", level = 2 });

            var copy = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

            Assert.AreEqual(SaveData.CurrentVersion, copy.version);
            Assert.AreEqual(data.coins, copy.coins);
            Assert.AreEqual(9, copy.maxLevelReached);
            CollectionAssert.AreEqual(data.purchases, copy.purchases);
            Assert.AreEqual(data.lastSeenUnix, copy.lastSeenUnix);
            Assert.AreEqual(1, copy.cats.Count);
            Assert.AreEqual(4, copy.cats[0].level);
            Assert.AreEqual(1.25f, copy.cats[0].x);
            Assert.AreEqual("income", copy.upgrades[0].id);
        }
    }
}
