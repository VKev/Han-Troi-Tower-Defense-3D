using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public readonly struct EnemyMotionSnapshot
    {
        public EnemyMotionSnapshot(
            long enemyId,
            Vector3 previousPosition,
            Vector3 position,
            float hitRadius)
        {
            EnemyId = enemyId;
            PreviousPosition = previousPosition;
            Position = position;
            HitRadius = hitRadius;
        }

        public long EnemyId { get; }
        public Vector3 PreviousPosition { get; }
        public Vector3 Position { get; }
        public float HitRadius { get; }
    }

    public readonly struct EnemySnapshot
    {
        public EnemySnapshot(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position,
            float health)
        {
            EnemyId = enemyId;
            Definition = definition;
            Position = position;
            Health = health;
        }

        public long EnemyId { get; }
        public EnemyDefinition Definition { get; }
        public Vector3 Position { get; }
        public float Health { get; }
    }
}
