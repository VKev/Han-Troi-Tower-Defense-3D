using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public readonly struct ProjectileHitResult
    {
        public ProjectileHitResult(
            long projectileId,
            long enemyId,
            float damage,
            bool killed)
        {
            ProjectileId = projectileId;
            EnemyId = enemyId;
            Damage = damage;
            Killed = killed;
        }

        public long ProjectileId { get; }
        public long EnemyId { get; }
        public float Damage { get; }
        public bool Killed { get; }
    }

    public sealed class ProjectileHitSystem
    {
        private const float ProjectileHitRadius = 0.2f;
        private readonly TowerNetworkManager towerNetworkManager;
        private readonly EnemySystem enemySystem;
        private readonly List<TowerProjectileMotionSnapshot> projectileMotions =
            new List<TowerProjectileMotionSnapshot>();
        private readonly List<EnemyMotionSnapshot> enemyMotions =
            new List<EnemyMotionSnapshot>();
        private readonly Dictionary<long, HashSet<long>> hitEnemyIdsByProjectile =
            new Dictionary<long, HashSet<long>>();
        private readonly HashSet<long> movingProjectileIds = new HashSet<long>();
        private readonly List<long> finishedProjectileIds = new List<long>();

        public ProjectileHitSystem(
            TowerNetworkManager towerNetworkManager,
            EnemySystem enemySystem)
        {
            this.towerNetworkManager = towerNetworkManager
                ?? throw new ArgumentNullException(nameof(towerNetworkManager));
            this.enemySystem = enemySystem ?? throw new ArgumentNullException(nameof(enemySystem));
        }

        public event Action<ProjectileHitResult> ProjectileHit;

        public void Step()
        {
            towerNetworkManager.CopyProjectileMotionSnapshotTo(projectileMotions);
            enemySystem.CopyMotionSnapshotsTo(enemyMotions);
            movingProjectileIds.Clear();

            for (int projectileIndex = 0;
                 projectileIndex < projectileMotions.Count;
                 projectileIndex++)
            {
                TowerProjectileMotionSnapshot projectile = projectileMotions[projectileIndex];
                movingProjectileIds.Add(projectile.ProjectileId);
                if (!hitEnemyIdsByProjectile.TryGetValue(
                    projectile.ProjectileId,
                    out HashSet<long> hitEnemyIds))
                {
                    hitEnemyIds = new HashSet<long>();
                    hitEnemyIdsByProjectile.Add(projectile.ProjectileId, hitEnemyIds);
                }

                ResolveProjectileHits(projectile, hitEnemyIds);
            }

            RemoveFinishedProjectiles();
        }

        public void Reset()
        {
            projectileMotions.Clear();
            enemyMotions.Clear();
            hitEnemyIdsByProjectile.Clear();
            movingProjectileIds.Clear();
            finishedProjectileIds.Clear();
        }

        private void ResolveProjectileHits(
            TowerProjectileMotionSnapshot projectile,
            ISet<long> hitEnemyIds)
        {
            Vector3 projectileStart = ToVector3(projectile.PreviousPosition);
            Vector3 projectileEnd = ToVector3(projectile.Position);

            for (int enemyIndex = 0; enemyIndex < enemyMotions.Count; enemyIndex++)
            {
                EnemyMotionSnapshot motion = enemyMotions[enemyIndex];
                if (hitEnemyIds.Contains(motion.EnemyId)
                    || !enemySystem.TryGetEnemy(motion.EnemyId, out EnemyInstance enemy)
                    || !TrajectoryHitCalculator.IntersectsXZ(
                        projectileStart,
                        projectileEnd,
                        motion.PreviousPosition,
                        motion.Position,
                        ProjectileHitRadius + motion.HitRadius))
                {
                    continue;
                }

                hitEnemyIds.Add(motion.EnemyId);
                float damage = EnemyDamageResolver.Resolve(
                    projectile.Payload.Damage,
                    projectile.Payload.DamageType,
                    enemy.Definition);
                bool killed = enemySystem.ApplyDamage(enemy.Id, damage);
                ProjectileHit?.Invoke(new ProjectileHitResult(
                    projectile.ProjectileId,
                    enemy.Id,
                    damage,
                    killed));
            }
        }

        private void RemoveFinishedProjectiles()
        {
            finishedProjectileIds.Clear();
            foreach (long projectileId in hitEnemyIdsByProjectile.Keys)
            {
                if (!movingProjectileIds.Contains(projectileId))
                {
                    finishedProjectileIds.Add(projectileId);
                }
            }

            for (int index = 0; index < finishedProjectileIds.Count; index++)
            {
                hitEnemyIdsByProjectile.Remove(finishedProjectileIds[index]);
            }
        }

        private static Vector3 ToVector3(TowerWorldPosition position)
        {
            return new Vector3(position.X, position.Y, position.Z);
        }
    }
}
