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
        private readonly Dictionary<long, float> speedBonusesByEnemyId =
            new Dictionary<long, float>();
        private readonly List<PendingSummon> pendingSummons = new List<PendingSummon>();
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
            return SpawnAt(ReserveEnemyId(), definition, roadPath.Start, 1);
        }

        internal EnemyInstance Spawn(long enemyId, EnemyDefinition definition)
        {
            return SpawnAt(enemyId, definition, roadPath.Start, 1);
        }

        internal long ReserveEnemyId()
        {
            if (nextEnemyId == long.MaxValue)
            {
                throw new InvalidOperationException("Enemy identifier range has been exhausted.");
            }

            return nextEnemyId++;
        }

        public void Step(float stepSeconds)
        {
            pendingSummons.Clear();
            speedBonusesByEnemyId.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (enemy.IsAlive)
                {
                    speedBonusesByEnemyId.Add(enemy.Id, FindStrongestSpeedBonus(enemy));
                }
            }

            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    activeEnemies.RemoveAt(index);
                    continue;
                }

                UpdateReveal(enemy, stepSeconds);
                QueueBossSummons(enemy, stepSeconds);
                enemy.PreviousPosition = enemy.Position;
                float speedMultiplier = 1f + speedBonusesByEnemyId[enemy.Id];
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

            }

            SpawnPendingSummons();
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

        public void RevealFromDirectHit(long enemyId)
        {
            EnemyInstance enemy = enemiesById[enemyId];
            if (enemy.Definition is StealthEnemyDefinition stealth)
            {
                enemy.RevealRemainingSeconds = stealth.RevealDurationSeconds;
            }
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
            speedBonusesByEnemyId.Clear();
            pendingSummons.Clear();
            nextEnemyId = 1L;
        }

        private EnemyInstance SpawnAt(
            long enemyId,
            EnemyDefinition definition,
            Vector3 position,
            int targetPointIndex,
            bool isSummoned = false)
        {
            var enemy = new EnemyInstance(enemyId, definition, position)
            {
                IsSummoned = isSummoned,
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
                if (ReferenceEquals(source, target)
                    || !source.IsAlive
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

        private void QueueBossSummons(EnemyInstance boss, float stepSeconds)
        {
            if (!(boss.Definition is SummonerBossEnemyDefinition definition))
            {
                return;
            }

            int phaseIndex = FindSummonPhase(definition, boss.HealthFraction);
            if (phaseIndex != boss.SummonPhaseIndex)
            {
                boss.SummonPhaseIndex = phaseIndex;
                boss.SummonElapsedSeconds = 0f;
            }

            SummonerBossEnemyDefinition.SummonPhase phase = definition.SummonPhases[phaseIndex];
            boss.SummonElapsedSeconds += stepSeconds;
            while (boss.SummonElapsedSeconds >= phase.SummonIntervalSeconds)
            {
                boss.SummonElapsedSeconds -= phase.SummonIntervalSeconds;
                for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
                {
                    SummonerBossEnemyDefinition.SummonedEnemyEntry entry = phase.Entries[entryIndex];
                    for (int count = 0; count < entry.Count; count++)
                    {
                        pendingSummons.Add(new PendingSummon(
                            entry.Definition,
                            boss.Position,
                            boss.TargetPointIndex));
                    }
                }
            }
        }

        private void SpawnPendingSummons()
        {
            for (int index = 0; index < pendingSummons.Count; index++)
            {
                PendingSummon summon = pendingSummons[index];
                SpawnAt(
                    ReserveEnemyId(),
                    summon.Definition,
                    summon.Position,
                    summon.TargetPointIndex,
                    isSummoned: true);
            }
        }

        private static int FindSummonPhase(
            SummonerBossEnemyDefinition definition,
            float healthFraction)
        {
            int selectedPhase = 0;
            for (int index = 1; index < definition.SummonPhases.Count; index++)
            {
                if (healthFraction > definition.SummonPhases[index].StartHealthFraction)
                {
                    break;
                }

                selectedPhase = index;
            }

            return selectedPhase;
        }

        private static void UpdateReveal(EnemyInstance enemy, float stepSeconds)
        {
            enemy.RevealRemainingSeconds = Mathf.Max(
                0f,
                enemy.RevealRemainingSeconds - stepSeconds);
        }

        private static EnemySnapshot CreateSnapshot(EnemyInstance enemy)
        {
            return new EnemySnapshot(
                enemy.Id,
                enemy.Definition,
                enemy.PreviousPosition,
                enemy.Position,
                enemy.Health,
                enemy.IsHidden,
                enemy.IsSummoned);
        }

        private readonly struct PendingSummon
        {
            public PendingSummon(
                EnemyDefinition definition,
                Vector3 position,
                int targetPointIndex)
            {
                Definition = definition;
                Position = position;
                TargetPointIndex = targetPointIndex;
            }

            public EnemyDefinition Definition { get; }
            public Vector3 Position { get; }
            public int TargetPointIndex { get; }
        }
    }
}
