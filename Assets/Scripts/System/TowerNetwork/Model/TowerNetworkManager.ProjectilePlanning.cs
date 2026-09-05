using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        private TowerProjectileSpawnOrder[] projectileSpawnPlan =
            Array.Empty<TowerProjectileSpawnOrder>();
        private int nextProjectileSpawnOrderIndex;
        private long projectilePlanEndTick;
        private bool usesProjectileSpawnPlan;

        internal IReadOnlyList<TowerProjectileSpawnOrder> EnsureProjectileSpawnPlanThrough(
            long endTick)
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException(
                    "Projectile planning requires a running tower simulation.");
            }

            if (endTick < CurrentTick)
            {
                throw new ArgumentOutOfRangeException(nameof(endTick));
            }

            if (usesProjectileSpawnPlan && endTick <= projectilePlanEndTick)
            {
                return projectileSpawnPlan;
            }

            TowerNetworkManager simulation = CreatePlanningSimulation();
            var spawnOrders = new List<TowerProjectileSpawnOrder>();
            simulation.ProjectileCreated += projectile =>
                spawnOrders.Add(new TowerProjectileSpawnOrder(
                    simulation.CurrentTick,
                    projectile));

            while (simulation.CurrentTick < endTick)
            {
                simulation.StepOneTick();
            }

            projectileSpawnPlan = spawnOrders.ToArray();
            projectilePlanEndTick = endTick;
            usesProjectileSpawnPlan = true;
            nextProjectileSpawnOrderIndex = FindNextProjectileSpawnOrderIndex(CurrentTick);
            return projectileSpawnPlan;
        }

        private TowerNetworkManager CreatePlanningSimulation()
        {
            var simulation = new TowerNetworkManager(catalog);
            simulation.BeginLevelSession(activeLevelNumber);

            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                NodeState node = nodes[nodeId];

                // The clone copies the live spec, so a tower that has been upgraded plans with
                // its upgraded numbers. Its definition and level ride along to keep the copy a
                // faithful one; planning never upgrades anything itself.
                var clone = new NodeState(nodeId, node.Spec, node.Position, node.Definition)
                {
                    UpgradeLevel = node.UpgradeLevel
                };
                simulation.nodes.Add(nodeId, clone);
                simulation.orderedNodeIds.Add(nodeId);
            }

            simulation.nextNodeId = nextNodeId;
            foreach (KeyValuePair<TowerNodeId, LinkState> pair in outgoingLinks)
            {
                LinkState link = pair.Value;
                simulation.outgoingLinks.Add(
                    pair.Key,
                    new LinkState(link.Source, link.Target, link.TargetInputPort));
            }

            simulation.RebuildValidChains();
            if (!simulation.TryStartSimulation(out string error))
            {
                throw new InvalidOperationException(
                    "Could not start projectile planning simulation: " + error);
            }

            return simulation;
        }

        private TowerProjectileSpawnOrder TakeNextProjectileSpawnOrder()
        {
            if (nextProjectileSpawnOrderIndex >= projectileSpawnPlan.Length)
            {
                throw new InvalidOperationException(
                    $"Projectile plan ends at tick {projectilePlanEndTick}.");
            }

            return projectileSpawnPlan[nextProjectileSpawnOrderIndex++];
        }

        private int FindNextProjectileSpawnOrderIndex(long currentTick)
        {
            int index = 0;
            while (index < projectileSpawnPlan.Length
                && projectileSpawnPlan[index].SpawnTick <= currentTick)
            {
                index++;
            }

            return index;
        }

        private void ClearProjectileSpawnPlan()
        {
            projectileSpawnPlan = Array.Empty<TowerProjectileSpawnOrder>();
            nextProjectileSpawnOrderIndex = 0;
            projectilePlanEndTick = 0L;
            usesProjectileSpawnPlan = false;
        }
    }
}
