using System;

namespace CatMerge.Core
{
    /// <summary>The player's coins.</summary>
    public sealed class Wallet
    {
        public double Coins { get; private set; }

        /// <summary>Fired after every change with the new balance.</summary>
        public event Action<double> OnChanged;

        public Wallet(double startCoins = 0)
        {
            Validate(startCoins, nameof(startCoins));
            Coins = startCoins;
        }

        public bool CanAfford(double amount) => amount <= Coins;

        public void Add(double amount)
        {
            Validate(amount, nameof(amount));
            if (amount == 0) return;
            Coins += amount;
            OnChanged?.Invoke(Coins);
        }

        /// <summary>Spends <paramref name="amount"/> if there is enough; otherwise changes nothing.</summary>
        public bool TrySpend(double amount)
        {
            Validate(amount, nameof(amount));
            if (!CanAfford(amount)) return false;
            if (amount == 0) return true;
            Coins -= amount;
            OnChanged?.Invoke(Coins);
            return true;
        }

        private static void Validate(double amount, string name)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0)
                throw new ArgumentOutOfRangeException(name, amount, "Must be a finite non-negative number.");
        }
    }
}
