using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemySystem
    {
        private readonly RoadPath roadPath;
        private readonly List<EnemyInstance> activeEnemies = new List<EnemyInstance>();
        private readonly Dictionary<long, EnemyInstance> enemiesById =
            new Dictionary<long, EnemyInstance>();
        private readonly List<EnemyMotionSnapshot> motionSnapshots =
            new List<EnemyMotionSnapshot>();
        private long nextEnemyId = 1L;

        public EnemySystem(RoadPath roadPath)
        {
            this.roadPath = roadPath ?? throw new ArgumentNullException(nameof(roadPath));
        }

        public event Action<EnemySnapshot> EnemySpawned;
        public event Action<EnemySnapshot> EnemyKilled;
        public event Action<EnemySnapshot> EnemyLeaked;

        public int LivingCount => enemiesById.Count;

        public EnemyInstance Spawn(EnemyDefinition definition)
        {
            return SpawnAt(definition, roadPath.Start, 1);
        }

        public void Step(float stepSeconds)
        {
            motionSnapshots.Clear();
            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    activeEnemies.RemoveAt(index);
                    continue;
                }

                enemy.PreviousPosition = enemy.Position;
                float speedMultiplier = 1f + FindStrongestSpeedBonus(enemy);
                float distance = enemy.Definition.BaseMoveSpeed * speedMultiplier * stepSeconds;
                Vector3 position = enemy.Position;
                int targetPointIndex = enemy.TargetPointIndex;
                bool reachedEnd = roadPath.Move(ref targetPointIndex, ref position, distance);
                enemy.Position = position;
                enemy.TargetPointIndex = targetPointIndex;

                if (reachedEnd)
                {
                    enemiesById.Remove(enemy.Id);
                    activeEnemies.RemoveAt(index);
                    EnemyLeaked?.Invoke(CreateSnapshot(enemy));
                    continue;
                }

                motionSnapshots.Add(new EnemyMotionSnapshot(
                    enemy.Id,
                    enemy.PreviousPosition,
                    enemy.Position,
                    enemy.Definition.BaseHitRadius));
            }
        }

        public bool TryGetEnemy(long enemyId, out EnemyInstance enemy)
        {
            return enemiesById.TryGetValue(enemyId, out enemy);
        }

        public bool ApplyDamage(long enemyId, float damage)
        {
            if (!enemiesById.TryGetValue(enemyId, out EnemyInstance enemy))
            {
                return false;
            }

            enemy.Health = Mathf.Max(0f, enemy.Health - damage);
            if (enemy.IsAlive)
            {
                return false;
            }

            enemiesById.Remove(enemyId);
            EnemyKilled?.Invoke(CreateSnapshot(enemy));
            return true;
        }

        public void CopyMotionSnapshotsTo(List<EnemyMotionSnapshot> destination)
        {
            destination.Clear();
            destination.AddRange(motionSnapshots);
        }

        public void CopySnapshotsTo(List<EnemySnapshot> destination)
        {
            destination.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (enemy.IsAlive)
                {
                    destination.Add(CreateSnapshot(enemy));
                }
            }
        }

        public void Reset()
        {
            activeEnemies.Clear();
            enemiesById.Clear();
            motionSnapshots.Clear();
            nextEnemyId = 1L;
        }

        private EnemyInstance SpawnAt(
            EnemyDefinition definition,
            Vector3 position,
            int targetPointIndex)
        {
            var enemy = new EnemyInstance(nextEnemyId++, definition, position)
            {
                TargetPointIndex = targetPointIndex
            };
            activeEnemies.Add(enemy);
            enemiesById.Add(enemy.Id, enemy);
            EnemySpawned?.Invoke(CreateSnapshot(enemy));
            return enemy;
        }

        private float FindStrongestSpeedBonus(EnemyInstance target)
        {
            if (target.Definition.Rank == EnemyRank.Boss)
            {
                return 0f;
            }

            float strongestBonus = 0f;
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance source = activeEnemies[index];
                if (!source.IsAlive
                    || !(source.Definition is SpeedSupportEnemyDefinition support))
                {
                    continue;
                }

                Vector2 offset = new Vector2(
                    source.Position.x - target.Position.x,
                    source.Position.z - target.Position.z);
                if (offset.sqrMagnitude > support.AuraRadiusMeters * support.AuraRadiusMeters)
                {
                    continue;
                }

                float bonus = target.Definition.Rank == EnemyRank.MiniBoss
                    ? support.MiniBossSpeedBonusFraction
                    : support.RegularSpeedBonusFraction;
                strongestBonus = Mathf.Max(strongestBonus, bonus);
            }

            return strongestBonus;
        }

        private static EnemySnapshot CreateSnapshot(EnemyInstance enemy)
        {
            return new EnemySnapshot(
                enemy.Id,
                enemy.Definition,
                enemy.PreviousPosition,
                enemy.Position,
                enemy.Health);
        }
    }
}
