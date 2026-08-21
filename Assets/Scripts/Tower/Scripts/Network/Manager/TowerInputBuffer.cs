using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public sealed class TowerInputBuffer
    {
        private readonly Queue<ProjectileQueueEntry>[] queues;
        private readonly int[] reservedSlotCounts;

        public TowerInputBuffer(int inputPortCount, int capacityPerInput)
        {
            if (inputPortCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(inputPortCount));
            }

            if (capacityPerInput < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacityPerInput));
            }

            if (inputPortCount == 0 && capacityPerInput != 0)
            {
                throw new ArgumentException(
                    "A node without input ports must have zero queue capacity.", nameof(capacityPerInput));
            }

            if (inputPortCount > 0 && capacityPerInput <= 0)
            {
                throw new ArgumentException(
                    "A node with input ports requires positive queue capacity.", nameof(capacityPerInput));
            }

            InputPortCount = inputPortCount;
            CapacityPerInput = capacityPerInput;
            queues = new Queue<ProjectileQueueEntry>[inputPortCount];
            reservedSlotCounts = new int[inputPortCount];

            for (int inputPort = 0; inputPort < inputPortCount; inputPort++)
            {
                queues[inputPort] = new Queue<ProjectileQueueEntry>();
            }
        }

        public int InputPortCount { get; }
        public int CapacityPerInput { get; }

        public int TotalQueuedProjectileCount
        {
            get
            {
                int total = 0;
                for (int inputPort = 0; inputPort < queues.Length; inputPort++)
                {
                    total += queues[inputPort].Count;
                }

                return total;
            }
        }

        public int TotalReservedSlotCount
        {
            get
            {
                int total = 0;
                for (int inputPort = 0; inputPort < reservedSlotCounts.Length; inputPort++)
                {
                    total += reservedSlotCounts[inputPort];
                }

                return total;
            }
        }

        public bool IsValidPort(int inputPort)
        {
            return inputPort >= 0 && inputPort < InputPortCount;
        }

        public int GetQueuedProjectileCount(int inputPort)
        {
            ValidateInputPort(inputPort);
            return queues[inputPort].Count;
        }

        public int GetReservedSlotCount(int inputPort)
        {
            ValidateInputPort(inputPort);
            return reservedSlotCounts[inputPort];
        }

        public int GetOccupiedSlotCount(int inputPort)
        {
            ValidateInputPort(inputPort);
            return queues[inputPort].Count + reservedSlotCounts[inputPort];
        }

        public int GetAvailableSlotCount(int inputPort)
        {
            return CapacityPerInput - GetOccupiedSlotCount(inputPort);
        }

        public bool CanReserve(int inputPort, int slotCount)
        {
            ValidateInputPort(inputPort);
            ValidatePositiveSlotCount(slotCount);
            return slotCount <= GetAvailableSlotCount(inputPort);
        }

        public bool TryReserve(int inputPort, int slotCount)
        {
            if (!CanReserve(inputPort, slotCount))
            {
                return false;
            }

            reservedSlotCounts[inputPort] += slotCount;
            return true;
        }

        public void CancelReservation(int inputPort, int slotCount)
        {
            ValidateInputPort(inputPort);
            ValidatePositiveSlotCount(slotCount);

            if (reservedSlotCounts[inputPort] < slotCount)
            {
                throw new InvalidOperationException("Cannot cancel more reserved slots than the port currently owns.");
            }

            reservedSlotCounts[inputPort] -= slotCount;
        }

        public void CommitArrival(int inputPort, ProjectileQueueEntry entry)
        {
            ValidateInputPort(inputPort);

            if (reservedSlotCounts[inputPort] <= 0)
            {
                throw new InvalidOperationException("A projectile cannot enter the queue without a reserved slot.");
            }

            reservedSlotCounts[inputPort]--;
            queues[inputPort].Enqueue(entry);
        }

        public bool TryPeek(int inputPort, out ProjectileQueueEntry entry)
        {
            ValidateInputPort(inputPort);
            Queue<ProjectileQueueEntry> queue = queues[inputPort];

            if (queue.Count == 0)
            {
                entry = default;
                return false;
            }

            entry = queue.Peek();
            return true;
        }

        public bool TryDequeue(int inputPort, out ProjectileQueueEntry entry)
        {
            ValidateInputPort(inputPort);
            Queue<ProjectileQueueEntry> queue = queues[inputPort];

            if (queue.Count == 0)
            {
                entry = default;
                return false;
            }

            entry = queue.Dequeue();
            return true;
        }

        public void Clear()
        {
            for (int inputPort = 0; inputPort < queues.Length; inputPort++)
            {
                queues[inputPort].Clear();
                reservedSlotCounts[inputPort] = 0;
            }
        }

        private void ValidateInputPort(int inputPort)
        {
            if (!IsValidPort(inputPort))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(inputPort), $"Input port {inputPort} is outside the valid range.");
            }
        }

        private static void ValidatePositiveSlotCount(int slotCount)
        {
            if (slotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "Slot count must be positive.");
            }
        }
    }
}
