using System;

namespace CatMerge.Core
{
    /// <summary>
    /// Ties coins to cats: kitten prices, buying with coins and total income. Income itself is paid out
    /// by the view layer (IncomeSystem) so each cat can show its own "+X" popup.
    /// </summary>
    public sealed class Economy
    {
        private readonly GameState _state;
        private readonly Func<int, double> _incomeForLevel;
        private readonly Func<int, long, double> _kittenPrice;
        private double _incomeMultiplier = 1;
        private double _prestigeMultiplier = 1;

        public Wallet Wallet { get; }

        /// <summary>Applied to every cat's income (income upgrade). 1 = no bonus.</summary>
        public double IncomeMultiplier
        {
            get => _incomeMultiplier;
            set => SetMultiplier(ref _incomeMultiplier, value);
        }

        /// <summary>Permanent bonus from fish (see <see cref="Prestige"/>), multiplied with <see cref="IncomeMultiplier"/>.</summary>
        public double PrestigeMultiplier
        {
            get => _prestigeMultiplier;
            set => SetMultiplier(ref _prestigeMultiplier, value);
        }

        /// <summary>Sum of all cats' income per second.</summary>
        public double IncomePerSec { get; private set; }

        /// <summary>Coins earned by cats (online and offline) since the last move; drives the fish reward.</summary>
        public double RunEarned { get; private set; }

        /// <summary>Fired with the new <see cref="IncomePerSec"/> whenever cats are added or merged or the multiplier changes.</summary>
        public event Action<double> IncomeChanged;

        /// <param name="incomeForLevel">Coins per second of one cat of the given level.</param>
        /// <param name="kittenPrice">(level, purchased so far) -> price.</param>
        public Economy(GameState state, Wallet wallet, Func<int, double> incomeForLevel, Func<int, long, double> kittenPrice)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            Wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _incomeForLevel = incomeForLevel ?? throw new ArgumentNullException(nameof(incomeForLevel));
            _kittenPrice = kittenPrice ?? throw new ArgumentNullException(nameof(kittenPrice));

            _state.CatAdded += _ => RecalculateIncome();
            _state.CatsMerged += (a, b, c) => RecalculateIncome();
            RecalculateIncome();
        }

        public double IncomeOf(CatData cat) => _incomeForLevel(cat.Level) * _incomeMultiplier * _prestigeMultiplier;

        /// <summary>Income payout: adds to the wallet and counts toward <see cref="RunEarned"/> (start coins and saves don't).</summary>
        public void Earn(double amount)
        {
            Wallet.Add(amount);
            RunEarned += amount;
        }

        /// <summary>Loads the saved <see cref="RunEarned"/>; bad values become 0.</summary>
        public void RestoreRunEarned(double earned)
        {
            RunEarned = double.IsNaN(earned) || double.IsInfinity(earned) || earned < 0 ? 0 : earned;
        }

        /// <summary>Price of the next kitten of this level.</summary>
        public double GetPrice(int level) => _kittenPrice(level, _state.GetPurchaseCount(level));

        /// <summary>The shop sells this level, the room has space and there are enough coins.</summary>
        public bool CanBuy(int level) => _state.CanBuy(level) && Wallet.CanAfford(GetPrice(level));

        public bool TryBuy(int level, out CatData cat)
        {
            cat = null;
            if (!CanBuy(level)) return false;

            Wallet.TrySpend(GetPrice(level));
            return _state.TryBuyCat(level, out cat);
        }

        private void SetMultiplier(ref double field, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Must be a finite non-negative number.");
            if (value == field) return;
            field = value;
            RecalculateIncome();
        }

        private void RecalculateIncome()
        {
            double sum = 0;
            foreach (var cat in _state.Cats) sum += IncomeOf(cat);
            if (sum == IncomePerSec) return;
            IncomePerSec = sum;
            IncomeChanged?.Invoke(sum);
        }
    }
}
