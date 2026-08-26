using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    public enum EnemyElementPhase
    {
        Ready,
        Marked,
        ReactionCooldown
    }

    public readonly struct EnemyElementState
    {
        public EnemyElementState(
            EnemyElementPhase phase,
            ElementType element,
            float remainingSeconds)
        {
            Phase = phase;
            Element = element;
            RemainingSeconds = remainingSeconds;
        }

        public EnemyElementPhase Phase { get; }
        public ElementType Element { get; }
        public float RemainingSeconds { get; }
    }
}
