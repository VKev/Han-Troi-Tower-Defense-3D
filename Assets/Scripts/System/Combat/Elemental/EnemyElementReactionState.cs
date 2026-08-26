using System;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    internal sealed class EnemyElementReactionState
    {
        private readonly ElementReactionCatalog catalog;
        private readonly float tickSeconds;
        private long phaseEndTick;

        public EnemyElementReactionState(
            ElementReactionCatalog catalog,
            float tickSeconds)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (tickSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tickSeconds));
            }

            this.tickSeconds = tickSeconds;
        }

        public EnemyElementPhase Phase { get; private set; }
        public ElementType Element { get; private set; }

        public void Advance(long tick)
        {
            if (Phase != EnemyElementPhase.Ready && tick >= phaseEndTick)
            {
                Phase = EnemyElementPhase.Ready;
                Element = default;
            }
        }

        public bool TryReceive(
            ElementType incoming,
            float statusDurationMultiplier,
            long tick,
            out ElementReactionDefinition reaction)
        {
            reaction = null;
            if (Phase == EnemyElementPhase.ReactionCooldown)
            {
                return false;
            }

            if (Phase == EnemyElementPhase.Ready)
            {
                if (statusDurationMultiplier <= 0f)
                {
                    return false;
                }

                Element = incoming;
                Phase = EnemyElementPhase.Marked;
                phaseEndTick = tick + SecondsToTicks(
                    catalog.ElementMarkDurationSeconds * statusDurationMultiplier);
                return false;
            }

            reaction = catalog.Get(Element, incoming);
            Element = default;
            Phase = EnemyElementPhase.ReactionCooldown;
            phaseEndTick = tick + SecondsToTicks(catalog.ReactionCooldownSeconds);
            return true;
        }

        public float GetRemainingSeconds(long tick)
        {
            return Phase == EnemyElementPhase.Ready
                ? 0f
                : Math.Max(0L, phaseEndTick - tick) * tickSeconds;
        }

        private int SecondsToTicks(float seconds)
        {
            return Math.Max(1, (int)Math.Ceiling(seconds / tickSeconds));
        }
    }
}
