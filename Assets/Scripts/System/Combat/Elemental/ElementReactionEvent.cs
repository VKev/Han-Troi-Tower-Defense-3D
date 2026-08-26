using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public readonly struct ElementReactionEvent
    {
        public ElementReactionEvent(
            long enemyId,
            ElementReactionId reactionId,
            Vector3 position)
        {
            EnemyId = enemyId;
            ReactionId = reactionId;
            Position = position;
        }

        public long EnemyId { get; }
        public ElementReactionId ReactionId { get; }
        public Vector3 Position { get; }
    }
}
