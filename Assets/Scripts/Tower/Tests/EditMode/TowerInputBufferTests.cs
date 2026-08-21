using System;
using NUnit.Framework;

namespace TowerDefense3D.Towers.Tests.EditMode
{
    public sealed class TowerInputBufferTests
    {
        [Test]
        public void SourceBuffer_HasNoInputQueue()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 0,
                    capacityPerInput: 0);

            Assert.That(
                buffer.InputPortCount,
                Is.Zero);

            Assert.That(
                buffer.CapacityPerInput,
                Is.Zero);

            Assert.That(
                buffer.TotalQueuedProjectileCount,
                Is.Zero);

            Assert.That(
                buffer.TotalReservedSlotCount,
                Is.Zero);

            Assert.That(
                buffer.IsValidPort(0),
                Is.False);
        }

        [Test]
        public void InputPorts_OwnIndependentQueues()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 2,
                    capacityPerInput: 2);

            Assert.That(
                buffer.TryReserve(0, 2),
                Is.True);

            Assert.That(
                buffer.GetAvailableSlotCount(0),
                Is.Zero);

            Assert.That(
                buffer.GetAvailableSlotCount(1),
                Is.EqualTo(2));

            Assert.That(
                buffer.TryReserve(1, 1),
                Is.True);

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.EqualTo(2));

            Assert.That(
                buffer.GetReservedSlotCount(1),
                Is.EqualTo(1));
        }

        [Test]
        public void BatchReservation_IsAtomic()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 1,
                    capacityPerInput: 3);

            Assert.That(
                buffer.TryReserve(0, 1),
                Is.True);

            Assert.That(
                buffer.TryReserve(0, 3),
                Is.False);

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.EqualTo(1));

            Assert.That(
                buffer.GetAvailableSlotCount(0),
                Is.EqualTo(2));

            buffer.Clear();

            Assert.That(
                buffer.TryReserve(0, 3),
                Is.True);

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.EqualTo(3));

            Assert.That(
                buffer.GetAvailableSlotCount(0),
                Is.Zero);
        }

        [Test]
        public void Arrival_ConvertsReservationIntoQueuedItem()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 1,
                    capacityPerInput: 3);

            Assert.That(
                buffer.TryReserve(0, 1),
                Is.True);

            int occupiedBefore =
                buffer.GetOccupiedSlotCount(0);

            buffer.CommitArrival(
                0,
                Entry(
                    projectileId: 10,
                    arrivalTick: 7));

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.Zero);

            Assert.That(
                buffer.GetQueuedProjectileCount(0),
                Is.EqualTo(1));

            Assert.That(
                buffer.GetOccupiedSlotCount(0),
                Is.EqualTo(occupiedBefore));
        }

        [Test]
        public void Queue_DequeuesInFifoOrder()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 1,
                    capacityPerInput: 3);

            Assert.That(
                buffer.TryReserve(0, 2),
                Is.True);

            buffer.CommitArrival(
                0,
                Entry(
                    projectileId: 100,
                    arrivalTick: 5));

            buffer.CommitArrival(
                0,
                Entry(
                    projectileId: 200,
                    arrivalTick: 6));

            Assert.That(
                buffer.TryDequeue(
                    0,
                    out ProjectileQueueEntry first),
                Is.True);

            Assert.That(
                buffer.TryDequeue(
                    0,
                    out ProjectileQueueEntry second),
                Is.True);

            Assert.That(
                first.ProjectileId,
                Is.EqualTo(100));

            Assert.That(
                second.ProjectileId,
                Is.EqualTo(200));

            Assert.That(
                buffer.TryDequeue(
                    0,
                    out _),
                Is.False);
        }

        [Test]
        public void ArrivalWithoutReservation_IsRejected()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 1,
                    capacityPerInput: 3);

            Assert.Throws<InvalidOperationException>(
                () => buffer.CommitArrival(
                    0,
                    Entry(
                        projectileId: 1,
                        arrivalTick: 1)));

            Assert.That(
                buffer.GetQueuedProjectileCount(0),
                Is.Zero);
        }

        [Test]
        public void CancelAndClear_ReleaseReservedCapacity()
        {
            var buffer =
                new TowerInputBuffer(
                    inputPortCount: 1,
                    capacityPerInput: 3);

            Assert.That(
                buffer.TryReserve(0, 3),
                Is.True);

            buffer.CancelReservation(0, 1);

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.EqualTo(2));

            Assert.That(
                buffer.GetAvailableSlotCount(0),
                Is.EqualTo(1));

            buffer.Clear();

            Assert.That(
                buffer.GetReservedSlotCount(0),
                Is.Zero);

            Assert.That(
                buffer.GetQueuedProjectileCount(0),
                Is.Zero);

            Assert.That(
                buffer.GetAvailableSlotCount(0),
                Is.EqualTo(3));
        }

        private static ProjectileQueueEntry Entry(
            long projectileId,
            long arrivalTick)
        {
            return new ProjectileQueueEntry(
                projectileId,
                arrivalTick,
                new ProjectilePayload(
                    ProjectilePayloadKind.Fire,
                    5f,
                    DamageType.Magic));
        }
    }
}