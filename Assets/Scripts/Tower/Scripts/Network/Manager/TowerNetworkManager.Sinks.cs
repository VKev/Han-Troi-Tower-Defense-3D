using System;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        private void StepSinks()
        {
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];

                if (!nodes.TryGetValue(nodeId, out NodeState node) ||
                    node.Spec.NetworkRole != TowerNetworkRole.Sink ||
                    !nodesInValidChains.Contains(nodeId))
                {
                    continue;
                }

                StepSink(node);
            }
        }

        private void StepSink(NodeState sink)
        {
            if (sink.InputBuffer.TotalQueuedProjectileCount == 0)
            {
                sink.CycleProgressTicks = 0;
                return;
            }

            if (sink.CycleProgressTicks < sink.Spec.CycleTicks)
            {
                sink.CycleProgressTicks++;
            }

            if (sink.CycleProgressTicks < sink.Spec.CycleTicks)
            {
                return;
            }

            int consumedCount = 0;

            while (consumedCount < sink.Spec.ConsumeBatchSize && TryConsumeNextInput(sink, out _))
            {
                consumedCount++;
            }

            if (consumedCount == 0)
            {
                throw new InvalidOperationException("A ready sink could not consume its queued projectile.");
            }

            sink.CycleProgressTicks = 0;
        }

        private static bool TryConsumeNextInput(NodeState sink, out ProjectileQueueEntry consumedInput)
        {
            if (sink.Spec.ConsumeOrder != SoulConsumeOrder.OldestArrivalThenInputPortThenProjectileId)
            {
                throw new InvalidOperationException("The sink consume order is not supported.");
            }

            return TryDequeueOldestInput(sink, out consumedInput);
        }

        private static bool TryDequeueOldestInput(NodeState sink, out ProjectileQueueEntry consumedInput)
        {
            int selectedInputPort = -1;
            ProjectileQueueEntry selectedInput = default;

            for (int inputPort = 0; inputPort < sink.InputBuffer.InputPortCount; inputPort++)
            {
                if (!sink.InputBuffer.TryPeek(inputPort, out ProjectileQueueEntry candidate))
                {
                    continue;
                }

                if (selectedInputPort < 0 ||
                    IsEarlierSinkInput(inputPort, candidate, selectedInputPort, selectedInput))
                {
                    selectedInputPort = inputPort;
                    selectedInput = candidate;
                }
            }

            if (selectedInputPort < 0)
            {
                consumedInput = default;
                return false;
            }

            if (!sink.InputBuffer.TryDequeue(selectedInputPort, out consumedInput) ||
                consumedInput.ProjectileId != selectedInput.ProjectileId)
            {
                throw new InvalidOperationException("The selected sink input changed during a simulation tick.");
            }

            return true;
        }

        private static bool IsEarlierSinkInput(
            int candidateInputPort,
            ProjectileQueueEntry candidate,
            int selectedInputPort,
            ProjectileQueueEntry selected)
        {
            if (candidate.ArrivalTick != selected.ArrivalTick)
            {
                return candidate.ArrivalTick < selected.ArrivalTick;
            }

            if (candidateInputPort != selectedInputPort)
            {
                return candidateInputPort < selectedInputPort;
            }

            return candidate.ProjectileId < selected.ProjectileId;
        }
    }
}
