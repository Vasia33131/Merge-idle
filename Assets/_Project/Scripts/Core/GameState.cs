using System;
using System.Collections.Generic;

namespace CatMerge.Core
{
    /// <summary>
    /// Pure game model: which cats exist and at what level. Knows nothing about Unity or views.
    /// </summary>
    public sealed class GameState
    {
        public const int StartLevel = 1;

        private readonly List<CatData> _cats = new List<CatData>();
        private readonly long[] _purchases;
        private int _nextId = 1;

        /// <summary>Room capacity; grows with the capacity upgrade (<see cref="SetMaxCats"/>).</summary>
        public int MaxCats { get; private set; }
        public int MaxLevel { get; }
        /// <summary>The shop sells kittens up to this many levels below <see cref="MaxLevelReached"/>.</summary>
        public int ShopLevelOffset { get; }
        public IReadOnlyList<CatData> Cats => _cats;
        public int CatCount => _cats.Count;
        public bool IsFull => _cats.Count >= MaxCats;

        /// <summary>Highest level the player has ever had (not reset when that cat is merged away).</summary>
        public int MaxLevelReached { get; private set; } = StartLevel;

        /// <summary>Highest level the shop sells right now.</summary>
        public int MaxBuyableLevel => ShopRules.MaxBuyableLevel(MaxLevelReached, ShopLevelOffset);

        /// <summary>A new cat appeared (bought or granted).</summary>
        public event Action<CatData> CatAdded;

        /// <summary>(dragged, target, result): both sources are gone, result replaces them.</summary>
        public event Action<CatData, CatData, CatData> CatsMerged;

        /// <summary>A level was reached for the first time.</summary>
        public event Action<int> LevelReached;

        /// <summary>Fired with the new <see cref="MaxCats"/>.</summary>
        public event Action<int> CapacityChanged;

        public GameState(int maxCats, int maxLevel, int shopLevelOffset)
        {
            if (maxCats < 1) throw new ArgumentOutOfRangeException(nameof(maxCats));
            if (maxLevel < StartLevel) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            if (shopLevelOffset < 0) throw new ArgumentOutOfRangeException(nameof(shopLevelOffset));
            MaxCats = maxCats;
            MaxLevel = maxLevel;
            ShopLevelOffset = shopLevelOffset;
            _purchases = new long[maxLevel + 1];
        }

        /// <summary>
        /// Changes the room capacity. Lowering it below <see cref="CatCount"/> keeps every cat, the room just counts as full.
        /// </summary>
        public void SetMaxCats(int maxCats)
        {
            if (maxCats < 1) throw new ArgumentOutOfRangeException(nameof(maxCats));
            if (maxCats == MaxCats) return;
            MaxCats = maxCats;
            CapacityChanged?.Invoke(maxCats);
        }

        public bool CanBuy(int level) => !IsFull && level >= StartLevel && level <= MaxBuyableLevel;

        /// <summary>How many kittens of this level were bought (drives that level's price).</summary>
        public long GetPurchaseCount(int level) => level >= StartLevel && level <= MaxLevel ? _purchases[level] : 0;

        /// <summary>Shop purchase: allowed only up to <see cref="MaxBuyableLevel"/>, bumps that level's price counter.</summary>
        public bool TryBuyCat(int level, out CatData cat)
        {
            if (!CanBuy(level))
            {
                cat = null;
                return false;
            }

            _purchases[level]++;
            cat = AddCat(level);
            return true;
        }

        /// <summary>Free cat (start bonus, rewards): ignores the shop limit and doesn't affect prices.</summary>
        public bool TryGrantCat(int level, out CatData cat)
        {
            if (IsFull || level < StartLevel || level > MaxLevel)
            {
                cat = null;
                return false;
            }

            cat = AddCat(level);
            return true;
        }

        /// <summary>
        /// Loads saved progress into an empty state. Cats are added in order and raise <see cref="CatAdded"/>
        /// (views spawn as usual); <see cref="LevelReached"/> is not raised. Bad values are repaired rather than
        /// rejected: levels are clamped, cats beyond <see cref="MaxCats"/> are dropped, negative counters become 0.
        /// </summary>
        public void Restore(IReadOnlyList<int> catLevels, IReadOnlyList<long> purchases, int maxLevelReached)
        {
            if (_cats.Count > 0) throw new InvalidOperationException("Restore needs an empty state.");

            if (purchases != null)
            {
                for (int level = StartLevel; level <= MaxLevel && level < purchases.Count; level++)
                {
                    _purchases[level] = Math.Max(0, purchases[level]);
                }
            }

            MaxLevelReached = Clamp(maxLevelReached);
            if (catLevels == null) return;
            for (int i = 0; i < catLevels.Count && !IsFull; i++)
            {
                int level = Clamp(catLevels[i]);
                if (level > MaxLevelReached) MaxLevelReached = level;
                AddCat(level);
            }
        }

        public MergeResult CanMerge(int draggedId, int targetId)
        {
            if (draggedId == targetId) return MergeResult.SameCat;

            var dragged = Find(draggedId);
            var target = Find(targetId);
            if (dragged == null || target == null) return MergeResult.CatNotFound;
            if (dragged.Level != target.Level) return MergeResult.DifferentLevels;
            if (dragged.Level >= MaxLevel) return MergeResult.MaxLevelReached;
            return MergeResult.Success;
        }

        public MergeResult TryMerge(int draggedId, int targetId, out CatData result)
        {
            result = null;
            var check = CanMerge(draggedId, targetId);
            if (check != MergeResult.Success) return check;

            var dragged = Find(draggedId);
            var target = Find(targetId);
            _cats.Remove(dragged);
            _cats.Remove(target);

            result = new CatData(_nextId++, target.Level + 1);
            _cats.Add(result);
            CatsMerged?.Invoke(dragged, target, result);
            TrackReached(result.Level);
            return MergeResult.Success;
        }

        public CatData Find(int id)
        {
            for (int i = 0; i < _cats.Count; i++)
            {
                if (_cats[i].Id == id) return _cats[i];
            }
            return null;
        }

        private CatData AddCat(int level)
        {
            var cat = new CatData(_nextId++, level);
            _cats.Add(cat);
            CatAdded?.Invoke(cat);
            TrackReached(level);
            return cat;
        }

        private int Clamp(int level) => Math.Min(Math.Max(level, StartLevel), MaxLevel);

        private void TrackReached(int level)
        {
            if (level <= MaxLevelReached) return;
            MaxLevelReached = level;
            LevelReached?.Invoke(level);
        }
    }
}
