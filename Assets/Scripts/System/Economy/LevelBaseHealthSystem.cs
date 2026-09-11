using System;

namespace TowerDefense3D.Economy
{
    /// <summary>
    /// Owns mutable Base HP for one loaded level scope.
    /// </summary>
    public sealed class LevelBaseHealthSystem
    {
        private readonly int startingHealth;
        private int currentHealth;
        private bool acceptsDamage = true;

        public LevelBaseHealthSystem(int startingHealth)
        {
            if (startingHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingHealth));
            }

            this.startingHealth = startingHealth;
            currentHealth = startingHealth;
        }

        public event Action<int, int> HealthChanged;

        public int CurrentHealth => currentHealth;
        public int MaximumHealth => startingHealth;
        public bool IsDepleted => currentHealth == 0;

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (acceptsDamage)
            {
                SetCurrentHealth(Math.Max(0, currentHealth - amount));
            }
        }

        public void Reset()
        {
            SetCurrentHealth(startingHealth);
        }

        internal void Restore(int value)
        {
            if (value < 0 || value > startingHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            SetCurrentHealth(value);
        }

        internal void SetDamageEnabled(bool enabled)
        {
            acceptsDamage = enabled;
        }

        private void SetCurrentHealth(int nextHealth)
        {
            if (currentHealth == nextHealth)
            {
                return;
            }

            currentHealth = nextHealth;
            HealthChanged?.Invoke(currentHealth, startingHealth);
        }
    }
}
