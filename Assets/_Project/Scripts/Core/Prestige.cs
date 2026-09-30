using System;

namespace CatMerge.Core
{
    /// <summary>
    /// "Moving to a new home": once the top cat level is reached, the player may start over for fish. Fish are
    /// computed from <see cref="Economy.RunEarned"/> and each one adds a permanent income bonus, applied to
    /// <see cref="Economy.PrestigeMultiplier"/>. The reset of the run itself is done by the caller (SaveService).
    /// </summary>
    public sealed class Prestige
    {
        private readonly GameState _state;
        private readonly Economy _economy;
        private readonly double _baseCoins;
        private readonly double _exponent;

        /// <summary>Income bonus per fish, e.g. 0.1 = +10%.</summary>
        public double BonusPerFish { get; }
        public long Fish { get; private set; }
        /// <summary>Times moved; picks the room background.</summary>
        public int Moves { get; private set; }

        public double Multiplier => 1 + Fish * BonusPerFish;

        /// <summary>The top cat level was reached in this run.</summary>
        public bool IsAvailable => _state.MaxLevelReached >= _state.MaxLevel;

        /// <summary>Fish a move would give right now.</summary>
        public long PendingFish => FishFor(_economy.RunEarned, _baseCoins, _exponent);

        /// <summary>Available and worth at least one fish.</summary>
        public bool CanMove => IsAvailable && PendingFish > 0;

        /// <summary>Fish or moves changed (a move or restored progress).</summary>
        public event Action Changed;

        /// <param name="baseCoins">Earning this much in a run gives 1 fish.</param>
        /// <param name="exponent">Fish = floor((earned / baseCoins)^exponent); 0.5 means 4× the coins for 2× the fish.</param>
        public Prestige(GameState state, Economy economy, double bonusPerFish, double baseCoins, double exponent)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            if (!(bonusPerFish >= 0) || double.IsInfinity(bonusPerFish)) throw new ArgumentOutOfRangeException(nameof(bonusPerFish));
            if (!(baseCoins > 0) || double.IsInfinity(baseCoins)) throw new ArgumentOutOfRangeException(nameof(baseCoins));
            if (!(exponent > 0) || double.IsInfinity(exponent)) throw new ArgumentOutOfRangeException(nameof(exponent));
            BonusPerFish = bonusPerFish;
            _baseCoins = baseCoins;
            _exponent = exponent;
            Apply();
        }

        public static long FishFor(double earned, double baseCoins, double exponent)
        {
            if (!(earned > 0) || !(baseCoins > 0)) return 0; // also rejects NaN
            double fish = Math.Floor(Math.Pow(earned / baseCoins, exponent));
            if (double.IsNaN(fish)) return 0;
            return fish >= long.MaxValue ? long.MaxValue : (long)fish;
        }

        /// <summary>Adds the pending fish and counts the move. The caller then resets the run.</summary>
        public bool TryMove(out long gained)
        {
            gained = 0;
            if (!CanMove) return false;

            gained = PendingFish;
            Fish = gained > long.MaxValue - Fish ? long.MaxValue : Fish + gained;
            if (Moves < int.MaxValue) Moves++;
            Apply();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Loads saved fish and moves; negative values become 0.</summary>
        public void Restore(long fish, int moves)
        {
            Fish = Math.Max(0, fish);
            Moves = Math.Max(0, moves);
            Apply();
            Changed?.Invoke();
        }

        private void Apply() => _economy.PrestigeMultiplier = Multiplier;
    }
}
