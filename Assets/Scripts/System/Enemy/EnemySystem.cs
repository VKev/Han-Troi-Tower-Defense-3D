using System;
using System.Collections.Generic;
using TowerDefense3D.Economy;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class EnemySystem
    {
        private readonly RoadPathSet roadPaths;
        private readonly LevelGoldSystem goldSystem;
        private readonly LevelBaseHealthSystem healthSystem;
        private readonly List<EnemyInstance> activeEnemies = new List<EnemyInstance>();
        private readonly Dictionary<long, EnemyInstance> enemiesById =
            new Dictionary<long, EnemyInstance>();
        private readonly Dictionary<long, float> speedBonusesByEnemyId =
            new Dictionary<long, float>();
        private readonly List<PendingSummon> pendingSummons = new List<PendingSummon>();
        private long nextEnemyId = 1L;

        public EnemySystem(
            RoadPath roadPath,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem)
            : this(
                new RoadPathSet(new[] { roadPath }),
                goldSystem,
                healthSystem)
        {
        }

        public EnemySystem(
            RoadPathSet roadPaths,
            LevelGoldSystem goldSystem,
            LevelBaseHealthSystem healthSystem)
        {
            this.roadPaths = roadPaths ?? throw new ArgumentNullException(nameof(roadPaths));
            this.goldSystem = goldSystem ?? throw new ArgumentNullException(nameof(goldSystem));
            this.healthSystem = healthSystem ?? throw new ArgumentNullException(nameof(healthSystem));
        }

        public event Action<EnemySnapshot> EnemySpawned;
        public event Action<EnemySnapshot> EnemyKilled;
        public event Action<EnemySnapshot> EnemyLeaked;

        public int LivingCount => enemiesById.Count;

        public EnemyInstance Spawn(EnemyDefinition definition)
        {
            long enemyId = ReserveEnemyId();
            RoadPath route = roadPaths.GetForEnemy(enemyId);
            return SpawnAt(enemyId, definition, route.Start, 1, route);
        }

        internal EnemyInstance Spawn(long enemyId, EnemyDefinition definition)
        {
            RoadPath route = roadPaths.GetForEnemy(enemyId);
            return SpawnAt(enemyId, definition, route.Start, 1, route);
        }

        internal long ReserveEnemyId()
        {
            if (nextEnemyId == long.MaxValue)
            {
                throw new InvalidOperationException("Enemy identifier range has been exhausted.");
            }

            return nextEnemyId++;
        }

        internal void SpawnPlannedSummon(PlannedEnemySpawn spawn)
        {
            long enemyId = ReserveEnemyId();
            if (enemyId != spawn.EnemyId)
            {
                throw new InvalidOperationException(
                    $"Planned summon expected Enemy {spawn.EnemyId}, but reserved {enemyId}.");
            }

            SpawnAt(
                enemyId,
                spawn.Definition,
                spawn.Position,
                spawn.TargetPointIndex,
                roadPaths.Get(spawn.RouteIndex),
                isSummoned: true);
        }

        internal void ApplyPlannedFrame(PlannedEnemyFrame frame)
        {
            EnemyInstance enemy = enemiesById[frame.EnemyId];
            enemy.PreviousPosition = frame.PreviousPosition;
            enemy.Position = frame.Position;
            enemy.Health = frame.Health;
            enemy.RevealRemainingSeconds = frame.RevealRemainingSeconds;
            enemy.SkillCastVersion = frame.SkillCastVersion;
            enemy.IsSpeedBuffed = frame.IsSpeedBuffed;
            enemy.TargetPointIndex = frame.TargetPointIndex;
            enemy.ElementState = new EnemyElementState(
                frame.ElementPhase,
                frame.Element,
                frame.ElementRemainingSeconds);
            enemy.RemainingThermalShieldHits = frame.RemainingThermalShieldHits;
            enemy.LiftHeightMeters = frame.LiftHeightMeters;

            if (frame.Removal == PlannedEnemyRemoval.None)
            {
                return;
            }

            enemiesById.Remove(enemy.Id);
            activeEnemies.Remove(enemy);
            EnemySnapshot snapshot = CreateSnapshot(enemy);
            if (frame.Removal == PlannedEnemyRemoval.Killed)
            {
                PublishEnemyKilled(snapshot);
            }
            else
            {
                PublishEnemyLeaked(snapshot);
            }
        }

        public void Step(float stepSeconds)
        {
            pendingSummons.Clear();
            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    activeEnemies.RemoveAt(index);
                    continue;
                }

                enemy.SkillCastCompletedThisStep = false;
                UpdateReveal(enemy, stepSeconds);
                UpdateSpeedSupport(enemy, stepSeconds);
                QueueBossSummons(enemy, stepSeconds);
            }

            speedBonusesByEnemyId.Clear();
            for (int index = 0; index < activeEnemies.Count; index++)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (!enemy.IsAlive)
                {
                    continue;
                }

                float speedBonus = FindStrongestSpeedBonus(enemy);
                speedBonusesByEnemyId.Add(enemy.Id, speedBonus);
                enemy.IsSpeedBuffed = speedBonus > 0f;
            }

            for (int index = activeEnemies.Count - 1; index >= 0; index--)
            {
                EnemyInstance enemy = activeEnemies[index];
                if (enemy.SkillCastRemainingSeconds > 0f || enemy.SkillCastCompletedThisStep)
                {
                    enemy.PreviousPosition = enemy.Position;
                    continue;
                }

                enemy.PreviousPosition = enemy.Position;
                float speedMultiplier = 1f + speedBonusesByEnemyId[enemy.Id];
                float distance = enemy.Definition.BaseMoveSpeed * speedMultiplier * stepSeconds;
                Vector3 position = enemy.Position;
                int targetPointIndex = enemy.TargetPointIndex;
                bool reachedEnd = enemy.Route.Move(ref targetPointIndex, ref position, distance);
                enemy.Position = position;
                enemy.TargetPointIndex = targetPointIndex;

                if (reachedEnd)
                {
                    enemiesById.Remove(enemy.Id);
                    activeEnemies.RemoveAt(index);
                    PublishEnemyLeaked(CreateSnapshot(enemy));
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
            PublishEnemyKilled(CreateSnapshot(enemy));
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
            RoadPath route,
            bool isSummoned = false)
        {
            var enemy = new EnemyInstance(enemyId, definition, position)
            {
                IsSummoned = isSummoned,
                TargetPointIndex = targetPointIndex,
                Route = route
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
                    || !source.IsSpeedAuraActive
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

            if (boss.SummonCastRemainingSeconds > 0f)
            {
                boss.SummonCastRemainingSeconds = Mathf.Max(
                    0f,
                    boss.SummonCastRemainingSeconds - stepSeconds);
                if (boss.SummonCastRemainingSeconds <= 0f)
                {
                    boss.SkillCastCompletedThisStep = true;
                }
                boss.SkillCastRemainingSeconds = boss.SummonCastRemainingSeconds;
                if (boss.SummonCastRemainingSeconds > 0f)
                {
                    return;
                }

                AddSummons(definition.SummonPhases[boss.SummonPhaseIndex], boss);
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
                boss.SummonCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                boss.SkillCastRemainingSeconds = definition.SummonSkillDurationSeconds;
                boss.SkillCastVersion++;
                break;
            }
        }

        private void AddSummons(SummonerBossEnemyDefinition.SummonPhase phase, EnemyInstance boss)
        {
            for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
            {
                SummonerBossEnemyDefinition.SummonedEnemyEntry entry = phase.Entries[entryIndex];
                for (int count = 0; count < entry.Count; count++)
                {
                    pendingSummons.Add(new PendingSummon(
                        entry.Definition,
                        boss.Position,
                        boss.TargetPointIndex,
                        boss.Route));
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
                    summon.Route,
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

        private static void UpdateSpeedSupport(EnemyInstance enemy, float stepSeconds)
        {
            if (!(enemy.Definition is SpeedSupportEnemyDefinition support))
            {
                return;
            }

            if (enemy.SupportActivationRemainingSeconds > 0f)
            {
                enemy.SupportActivationRemainingSeconds = Mathf.Max(
                    0f,
                    enemy.SupportActivationRemainingSeconds - stepSeconds);
                if (enemy.SupportActivationRemainingSeconds > 0f)
                {
                    return;
                }

            }

            if (!enemy.IsSpeedAuraActive)
            {
                enemy.SkillCastRemainingSeconds = support.SkillDurationSeconds;
                enemy.SkillCastVersion++;
                enemy.IsSpeedAuraActive = true;
                return;
            }

            if (enemy.SkillCastRemainingSeconds <= 0f)
            {
                return;
            }

            enemy.SkillCastRemainingSeconds = Mathf.Max(
                0f,
                enemy.SkillCastRemainingSeconds - stepSeconds);
            if (enemy.SkillCastRemainingSeconds <= 0f)
            {
                enemy.SkillCastCompletedThisStep = true;
            }
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
                enemy.IsSummoned,
                enemy.ElementState,
                enemy.RemainingThermalShieldHits,
                enemy.LiftHeightMeters,
                enemy.SkillCastVersion,
                enemy.IsSpeedBuffed);
        }

        private void PublishEnemyKilled(EnemySnapshot snapshot)
        {
            goldSystem.Add(snapshot.Definition.GoldOnDeath);
            EnemyKilled?.Invoke(snapshot);
        }

        private void PublishEnemyLeaked(EnemySnapshot snapshot)
        {
            healthSystem.TakeDamage(snapshot.Definition.LeakDamage);
            EnemyLeaked?.Invoke(snapshot);
        }

        private readonly struct PendingSummon
        {
            public PendingSummon(
                EnemyDefinition definition,
                Vector3 position,
                int targetPointIndex,
                RoadPath route)
            {
                Definition = definition;
                Position = position;
                TargetPointIndex = targetPointIndex;
                Route = route;
            }

            public EnemyDefinition Definition { get; }
            public Vector3 Position { get; }
            public int TargetPointIndex { get; }
            public RoadPath Route { get; }
        }
    }
}
