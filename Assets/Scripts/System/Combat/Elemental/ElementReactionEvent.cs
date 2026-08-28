using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public readonly struct ElementReactionEvent
    {
        public ElementReactionEvent(
            long enemyId,
            ElementReactionId reactionId,
            ElementPair pair,
            Vector3 position,
            float durationSeconds)
        {
            EnemyId = enemyId;
            ReactionId = reactionId;
            Pair = pair;
            Position = position;
            DurationSeconds = durationSeconds;
        }

        public long EnemyId { get; }
        public ElementReactionId ReactionId { get; }
        public ElementPair Pair { get; }
        public Vector3 Position { get; }
        public float DurationSeconds { get; }
    }
}
