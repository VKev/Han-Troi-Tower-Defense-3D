using System;

namespace TowerDefense3D.Economy
{
    /// <summary>
    /// Owns mutable Gold for one loaded level scope.
    /// </summary>
    public sealed class LevelGoldSystem
    {
        private readonly int startingGold;
        private int balance;

        public LevelGoldSystem(int startingGold)
        {
            ValidateNonNegative(startingGold, nameof(startingGold));
            this.startingGold = startingGold;
            balance = startingGold;
        }

        public event Action<int> BalanceChanged;

        public int Balance => balance;

        public bool CanAfford(int amount)
        {
            ValidateNonNegative(amount, nameof(amount));
            return balance >= amount;
        }

        public bool TrySpend(int amount)
        {
            ValidateNonNegative(amount, nameof(amount));
            if (balance < amount)
            {
                return false;
            }

            SetBalance(balance - amount);
            return true;
        }

        public void Add(int amount)
        {
            ValidateNonNegative(amount, nameof(amount));
            SetBalance(checked(balance + amount));
        }

        public void Reset()
        {
            SetBalance(startingGold);
        }

        private void SetBalance(int nextBalance)
        {
            if (balance == nextBalance)
            {
                return;
            }

            balance = nextBalance;
            BalanceChanged?.Invoke(balance);
        }

        private static void ValidateNonNegative(int amount, string parameterName)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
