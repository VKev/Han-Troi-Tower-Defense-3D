using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {

        private readonly List<ProjectileState> activeProjectiles = new List<ProjectileState>();
        private long nextProjectileId = 1L;

        public int ProjectileCount => activeProjectiles.Count;

        public IReadOnlyList<TowerProjectileSnapshot> CreateProjectileSnapshot()
        {
            TowerProjectileSnapshot[] snapshot = new TowerProjectileSnapshot[activeProjectiles.Count];

            for (int index = 0; index < activeProjectiles.Count; index++)
            {
                snapshot[index] = CreateProjectileSnapshot(activeProjectiles[index]);
            }

            return snapshot;
        }

        public void CopyProjectileSnapshotTo(List<TowerProjectileSnapshot> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            if (destination.Capacity < activeProjectiles.Count)
            {
                destination.Capacity = activeProjectiles.Count;
            }

            for (int index = 0; index < activeProjectiles.Count; index++)
            {
                destination.Add(CreateProjectileSnapshot(activeProjectiles[index]));
            }
        }

        private void StepActiveProjectiles()
        {
            float travelDistancePerTick = projectileSpeedMetersPerSecond * tickSeconds;
            int projectileIndex = 0;

            while (projectileIndex < activeProjectiles.Count)
            {
                ProjectileState projectile = activeProjectiles[projectileIndex];

                if (projectile.RemainingLaunchDelayTicks > 0)
                {
                    projectile.RemainingLaunchDelayTicks--;
                    projectileIndex++;
                    continue;
                }

                NodeState target = nodes[projectile.Target];

                projectile.Position = TowerWorldPosition.MoveTowards(
                    projectile.Position, target.Position, travelDistancePerTick);

                if (!HasReachedTarget(projectile.Position, target.Position))
                {
                    projectileIndex++;
                    continue;
                }

                CommitProjectileArrival(projectile, target);
                activeProjectiles.RemoveAt(projectileIndex);
            }
        }
        private static bool HasReachedTarget(
            TowerWorldPosition projectilePosition,
            TowerWorldPosition targetPosition)
        {
            return projectilePosition.X == targetPosition.X
                && projectilePosition.Y == targetPosition.Y
                && projectilePosition.Z == targetPosition.Z;
        }

        private void CommitProjectileArrival(ProjectileState projectile, NodeState target)
        {
            ProjectileQueueEntry queueEntry = new ProjectileQueueEntry(
                projectile.ProjectileId,
                CurrentTick,
                projectile.Payload);
            target.InputBuffer.CommitArrival(projectile.TargetInputPort, queueEntry);
        }


        private void ClearProjectileRuntimeState()
        {
            activeProjectiles.Clear();
            nextProjectileId = 1L;
        }

        private static TowerProjectileSnapshot CreateProjectileSnapshot(ProjectileState projectile)
        {
            return new TowerProjectileSnapshot(
                projectile.ProjectileId, projectile.Source, projectile.Target, projectile.Position,
                projectile.Payload, projectile.RemainingLaunchDelayTicks);
        }

        private void ValidateProjectileBatchCreation(TowerRuntimeSpec spec)
        {
            int batchSize = spec.OutputProjectileCount;

            if (nextProjectileId > long.MaxValue - batchSize)
            {
                throw new InvalidOperationException("Projectile identifier range has been exhausted.");
            }

            _ = checked((batchSize - 1) * spec.SequenceSpacingTicks);
        }

        private bool TryEmitProjectileBatch(NodeState source)
        {
            LinkState link = outgoingLinks[source.Id];
            NodeState target = nodes[link.Target];

            TowerRuntimeSpec spec = source.Spec;
            ValidateProjectileBatchCreation(spec);

            int reservationCount = spec.RequiredDownstreamReservationCount;
            if (!target.InputBuffer.TryReserve(link.TargetInputPort, reservationCount))
            {
                return false;
            }

            int firstProjectileIndex = activeProjectiles.Count;
            long firstProjectileId = nextProjectileId;

            try
            {
                for (int projectileIndex = 0; projectileIndex < spec.OutputProjectileCount; projectileIndex++)
                {
                    int launchDelayTicks = checked(projectileIndex * spec.SequenceSpacingTicks);

                    activeProjectiles.Add(new ProjectileState(
                        nextProjectileId,
                        source.Id,
                        link.Target,
                        link.TargetInputPort,
                        source.Position,
                        spec.OutputPayload,
                        launchDelayTicks));

                    nextProjectileId++;
                }

                return true;
            }
            catch
            {
                int createdProjectileCount = activeProjectiles.Count - firstProjectileIndex;
                if (createdProjectileCount > 0)
                {
                    activeProjectiles.RemoveRange(firstProjectileIndex, createdProjectileCount);
                }

                target.InputBuffer.CancelReservation(link.TargetInputPort, reservationCount);
                nextProjectileId = firstProjectileId;
                throw;
            }
        }
    }
}
