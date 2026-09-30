using System;
using System.Collections.Generic;

namespace CatMerge.Core
{
    /// <summary>
    /// Upgrade levels, prices and buying. Applies its effects itself: room capacity to <see cref="GameState"/> and the
    /// income multiplier to <see cref="Economy"/>; offline values are read through <see cref="OfflineRate"/> and
    /// <see cref="OfflineMaxSeconds"/>.
    /// </summary>
    public sealed class Upgrades
    {
        private readonly UpgradeDef[] _defs;
        private readonly int[] _levels;
        private readonly GameState _state;
        private readonly Economy _economy;
        private readonly int _baseCapacity;
        private readonly Func<int, double> _kittenBasePrice;

        public IReadOnlyList<UpgradeDef> Defs => _defs;

        /// <summary>A level was bought or progress was restored.</summary>
        public event Action Changed;

        /// <param name="baseCapacity">Room capacity without upgrades.</param>
        /// <param name="kittenBasePrice">Shop price of a kitten of this level before any purchases (for BestKitten).</param>
        public Upgrades(IEnumerable<UpgradeDef> defs, GameState state, Economy economy, int baseCapacity,
            Func<int, double> kittenBasePrice)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _kittenBasePrice = kittenBasePrice ?? throw new ArgumentNullException(nameof(kittenBasePrice));
            if (baseCapacity < 1) throw new ArgumentOutOfRangeException(nameof(baseCapacity));
            _baseCapacity = baseCapacity;

            var list = new List<UpgradeDef>();
            var ids = new HashSet<string>();
            foreach (var def in defs ?? Array.Empty<UpgradeDef>())
            {
                if (def == null) continue;
                if (!ids.Add(def.Id)) throw new ArgumentException($"Duplicate upgrade id '{def.Id}'.", nameof(defs));
                list.Add(def);
            }
            _defs = list.ToArray();
            _levels = new int[_defs.Length];
            ApplyEffects();
        }

        public UpgradeDef Find(string id)
        {
            int index = IndexOf(id);
            return index >= 0 ? _defs[index] : null;
        }

        /// <summary>Levels bought (for BestKitten: kittens bought).</summary>
        public int GetLevel(UpgradeDef def) => _levels[IndexOf(def)];

        public bool IsMaxed(UpgradeDef def) => def.MaxLevel > 0 && GetLevel(def) >= def.MaxLevel;

        public bool IsUnlocked(UpgradeDef def) => _state.MaxLevelReached >= def.UnlockAtCatLevel;

        /// <summary>Level of the kitten a BestKitten upgrade sells right now (never below 1).</summary>
        public int BestKittenLevel(UpgradeDef def) =>
            Math.Min(_state.MaxLevel, Math.Max(GameState.StartLevel, _state.MaxLevelReached - (int)Math.Round(def.Value)));

        /// <summary>Price of the next level (or the next kitten).</summary>
        public double GetPrice(UpgradeDef def)
        {
            double price = def.BasePrice * Math.Pow(def.PriceGrowth, GetLevel(def));
            if (def.Effect == UpgradeEffect.BestKitten) price *= _kittenBasePrice(BestKittenLevel(def));
            return double.IsInfinity(price) ? double.MaxValue : Math.Round(price);
        }

        public UpgradeStatus GetStatus(UpgradeDef def)
        {
            if (!IsUnlocked(def)) return UpgradeStatus.Locked;
            if (IsMaxed(def)) return UpgradeStatus.MaxLevel;
            if (def.Effect == UpgradeEffect.BestKitten && _state.IsFull) return UpgradeStatus.RoomFull;
            if (!_economy.Wallet.CanAfford(GetPrice(def))) return UpgradeStatus.NotEnoughCoins;
            return UpgradeStatus.Available;
        }

        public bool TryBuy(UpgradeDef def)
        {
            if (GetStatus(def) != UpgradeStatus.Available) return false;

            int kittenLevel = BestKittenLevel(def);
            _economy.Wallet.TrySpend(GetPrice(def));
            _levels[IndexOf(def)]++;
            // Granted, not bought: the shop's own price counters stay as they are.
            if (def.Effect == UpgradeEffect.BestKitten) _state.TryGrantCat(kittenLevel, out _);

            ApplyEffects();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Sum of Value × level over all upgrades with this effect.</summary>
        public double Total(UpgradeEffect effect)
        {
            double sum = 0;
            for (int i = 0; i < _defs.Length; i++)
            {
                if (_defs[i].Effect == effect) sum += _defs[i].Value * _levels[i];
            }
            return sum;
        }

        public int Capacity => Math.Max(1, _baseCapacity + (int)Math.Round(Total(UpgradeEffect.RoomCapacity)));

        public double IncomeMultiplier => Math.Max(0, 1 + Total(UpgradeEffect.IncomeMultiplier));

        public double OfflineRate(double baseRate) => Math.Min(1, Math.Max(0, baseRate + Total(UpgradeEffect.OfflineRate)));

        public double OfflineMaxSeconds(double baseSeconds) => Math.Max(0, baseSeconds + Total(UpgradeEffect.OfflineHours) * 3600);

        /// <summary>
        /// Loads saved levels: unknown ids are ignored (upgrade removed), levels are clamped to 0..MaxLevel.
        /// Call before restoring cats so the extra room spots already exist.
        /// </summary>
        public void Restore(IEnumerable<UpgradeSave> saved)
        {
            Array.Clear(_levels, 0, _levels.Length);
            if (saved != null)
            {
                foreach (var entry in saved)
                {
                    int index = entry == null ? -1 : IndexOf(entry.id);
                    if (index < 0) continue;
                    int level = Math.Max(0, entry.level);
                    if (_defs[index].MaxLevel > 0) level = Math.Min(level, _defs[index].MaxLevel);
                    _levels[index] = level;
                }
            }

            ApplyEffects();
            Changed?.Invoke();
        }

        /// <summary>Replaces <paramref name="target"/> with the current levels (bought ones only).</summary>
        public void WriteTo(List<UpgradeSave> target)
        {
            target.Clear();
            for (int i = 0; i < _defs.Length; i++)
            {
                if (_levels[i] > 0) target.Add(new UpgradeSave { id = _defs[i].Id, level = _levels[i] });
            }
        }

        private void ApplyEffects()
        {
            _state.SetMaxCats(Capacity);
            _economy.IncomeMultiplier = IncomeMultiplier;
        }

        private int IndexOf(UpgradeDef def)
        {
            int index = Array.IndexOf(_defs, def);
            if (index < 0) throw new ArgumentException($"Upgrade '{def?.Id}' is not part of this set.", nameof(def));
            return index;
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _defs.Length; i++)
            {
                if (_defs[i].Id == id) return i;
            }
            return -1;
        }
    }
}
