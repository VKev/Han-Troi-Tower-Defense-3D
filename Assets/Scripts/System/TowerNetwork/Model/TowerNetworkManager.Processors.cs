using System;

namespace TowerDefense3D.Towers
{
    public sealed partial class TowerNetworkManager
    {
        private void StepProcessors()
        {
            for (int index = 0; index < orderedNodeIds.Count; index++)
            {
                TowerNodeId nodeId = orderedNodeIds[index];
                NodeState node = nodes[nodeId];

                if (node.Spec.NetworkRole != TowerNetworkRole.Processor ||
                    !nodesInValidChains.Contains(nodeId))
                {
                    continue;
                }

                StepProcessor(node);
            }
        }

        private void StepProcessor(NodeState processor)
        {
            const int inputPort = 0;

            if (!processor.InputBuffer.TryPeek(inputPort, out ProjectileQueueEntry queuedInput))
            {
                processor.CycleProgressTicks = 0;
                return;
            }

            if (processor.CycleProgressTicks < processor.Spec.CycleTicks)
            {
                processor.CycleProgressTicks++;
            }

            if (processor.CycleProgressTicks < processor.Spec.CycleTicks)
            {
                return;
            }

            if (!TryEmitProjectileBatch(processor))
            {
                return;
            }

            DequeueProcessedInput(processor, inputPort, queuedInput);
            processor.CycleProgressTicks = 0;
        }

        private static void DequeueProcessedInput(
            NodeState processor,
            int inputPort,
            ProjectileQueueEntry expectedInput)
        {
            if (!processor.InputBuffer.TryDequeue(inputPort, out ProjectileQueueEntry dequeuedInput) ||
                dequeuedInput.ProjectileId != expectedInput.ProjectileId)
            {
                throw new InvalidOperationException("Processor input queue changed during a simulation tick.");
            }
        }
    }
}
