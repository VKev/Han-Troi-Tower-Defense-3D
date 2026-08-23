using System;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        public bool TryStartSimulation(out string error)
        {
            if (!HasLevelSession)
            {
                error = "No active tower-network level session.";
                return false;
            }

            if (IsRunning)
            {
                error = string.Empty;
                return true;
            }

            if (!HasValidChain)
            {
                error = "At least one valid Generator-to-Soul-Nexus chain is required.";
                return false;
            }

            ResetTransientSimulationState();
            IsRunning = true;
            PublishStateChanged();
            error = string.Empty;
            return true;
        }

        public void StopSimulation()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            ResetTransientSimulationState();
            PublishStateChanged();
        }

        public bool TryCreateNodeSimulationSnapshot(TowerNodeId nodeId, out TowerNodeSimulationSnapshot snapshot)
        {
            if (!nodes.TryGetValue(nodeId, out NodeState node))
            {
                snapshot = default;
                return false;
            }

            snapshot = new TowerNodeSimulationSnapshot(
                node.Id, node.CycleProgressTicks, node.Spec.CycleTicks, nodesInValidChains.Contains(node.Id));

            return true;
        }

        public bool StepOneTick()
        {
            if (!IsRunning)
            {
                return false;
            }

            if (CurrentTick == long.MaxValue)
            {
                throw new InvalidOperationException("Tower simulation tick range has been exhausted.");
            }

            CurrentTick++;
            StepActiveProjectiles();
            StepSinks();
            StepProcessors();
            StepGeneratorSources();
            return true;
        }

        private void StepGeneratorSources()
        {
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                NodeState node = nodes[nodeId];

                if (!IsGeneratorSource(node) || !nodesInValidChains.Contains(nodeId))
                {
                    continue;
                }

                StepGenerator(node);
            }
        }

        private void StepGenerator(NodeState source)
        {
            if (source.CycleProgressTicks < source.Spec.CycleTicks)
            {
                source.CycleProgressTicks++;
            }

            if (source.CycleProgressTicks < source.Spec.CycleTicks)
            {
                return;
            }

            if (!TryEmitProjectileBatch(source))
            {
                return;
            }

            source.CycleProgressTicks = 0;
        }

        private void ResetTransientSimulationState()
        {
            CurrentTick = 0L;
            ClearProjectileRuntimeState();

            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                NodeState node = nodes[nodeId];
                node.CycleProgressTicks = 0;
                node.InputBuffer.Clear();
            }
        }
    }
}
