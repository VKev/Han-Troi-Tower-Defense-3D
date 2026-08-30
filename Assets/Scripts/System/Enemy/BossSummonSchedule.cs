using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    /// <summary>
    /// One summon the boss will place during a single cast: which enemy, how long after the cast
    /// began, and how far ahead of or behind the boss it appears.
    /// </summary>
    public readonly struct ScheduledSummon
    {
        public ScheduledSummon(
            EnemyDefinition definition,
            float dueSeconds,
            float forwardOffsetMeters)
        {
            Definition = definition;
            DueSeconds = dueSeconds;
            ForwardOffsetMeters = forwardOffsetMeters;
        }

        public EnemyDefinition Definition { get; }
        public float DueSeconds { get; }
        public float ForwardOffsetMeters { get; }
    }

    /// <summary>
    /// Turns a summon phase into a running order: the enemies arrive one at a time, spread over
    /// the cast rather than appearing together at the end of it, and each one steps in front of or
    /// behind the boss instead of materialising inside it.
    ///
    /// Both the combat timeline planner and the live simulation build the schedule through this
    /// one method. They never share state, so the only way their answers can agree is for the
    /// order and the offsets to be derived from values both of them already hold - the boss id and
    /// the cast number - rather than drawn from an ambient random source.
    /// </summary>
    public static class BossSummonSchedule
    {
        public static void Build(
            SummonerBossEnemyDefinition definition,
            SummonerBossEnemyDefinition.SummonPhase phase,
            long bossId,
            int castVersion,
            List<ScheduledSummon> into)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            if (phase == null)
            {
                return;
            }

            var order = new List<EnemyDefinition>();
            for (int entryIndex = 0; entryIndex < phase.Entries.Count; entryIndex++)
            {
                SummonerBossEnemyDefinition.SummonedEnemyEntry entry = phase.Entries[entryIndex];
                for (int count = 0; count < entry.Count; count++)
                {
                    if (entry.Definition != null)
                    {
                        order.Add(entry.Definition);
                    }
                }
            }

            if (order.Count == 0)
            {
                return;
            }

            var random = new System.Random(CombineSeed(bossId, castVersion));
            for (int index = order.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                EnemyDefinition held = order[index];
                order[index] = order[swap];
                order[swap] = held;
            }

            // Sides are dealt out evenly rather than rolled per summon, so four summons always
            // come out two in front and two behind instead of occasionally all landing on one
            // side. Only which summon takes which side is shuffled. An odd count leaves the
            // spare one behind the boss.
            var facing = new List<int>(order.Count);
            int aheadCount = order.Count / 2;
            for (int index = 0; index < order.Count; index++)
            {
                facing.Add(index < aheadCount ? 1 : -1);
            }

            for (int index = facing.Count - 1; index > 0; index--)
            {
                int swap = random.Next(index + 1);
                int held = facing[index];
                facing[index] = facing[swap];
                facing[swap] = held;
            }

            // Each side stacks outwards from the edge of the boss rather than everyone standing
            // at one fixed distance, so a summon is never hidden under the boss and the second
            // summon on a side is never hidden under the first.
            float clearance = definition.SummonSpawnClearanceMeters;
            float aheadEdge = definition.BaseHitRadius + clearance;
            float behindEdge = aheadEdge;
            var sides = new List<float>(order.Count);
            for (int index = 0; index < order.Count; index++)
            {
                float radius = order[index].BaseHitRadius;
                if (facing[index] > 0)
                {
                    sides.Add(aheadEdge + radius);
                    aheadEdge += radius * 2f + clearance;
                }
                else
                {
                    sides.Add(-(behindEdge + radius));
                    behindEdge += radius * 2f + clearance;
                }
            }

            // The window runs from the authored start delay to the end of the cast, and the last
            // summon lands strictly inside it, so every enemy is on the board before the boss
            // stops casting.
            float castDuration = definition.SummonSkillDurationSeconds;
            float start = Mathf.Clamp(definition.SummonSpawnStartDelaySeconds, 0f, castDuration);
            float window = castDuration - start;
            for (int index = 0; index < order.Count; index++)
            {
                float due = start + window * (index + 1) / (order.Count + 1);
                into.Add(new ScheduledSummon(order[index], due, sides[index]));
            }
        }

        /// <summary>
        /// Walks the road from the boss to place a summon, forwards or backwards, and reports the
        /// waypoint the summon should then head for. Stepping along the road matters: its points
        /// are one cell apart, so anything that stopped at the next corner would land back on top
        /// of the boss.
        /// </summary>
        public static void GetSpawnPlacement(
            RoadPath route,
            int targetPointIndex,
            Vector3 bossPosition,
            float signedDistanceMeters,
            out Vector3 position,
            out int summonTargetPointIndex)
        {
            position = bossPosition;
            summonTargetPointIndex = targetPointIndex;
            if (route == null || targetPointIndex < 0 || targetPointIndex >= route.PointCount)
            {
                return;
            }

            if (signedDistanceMeters >= 0f)
            {
                route.Move(ref summonTargetPointIndex, ref position, signedDistanceMeters);
                summonTargetPointIndex = Mathf.Min(summonTargetPointIndex, route.PointCount - 1);
                return;
            }

            float remaining = -signedDistanceMeters;
            while (remaining > 0f && summonTargetPointIndex > 0)
            {
                Vector3 previous = route.GetPoint(summonTargetPointIndex - 1);
                float step = Vector3.Distance(position, previous);
                if (step > remaining)
                {
                    position = Vector3.MoveTowards(position, previous, remaining);
                    return;
                }

                position = previous;
                remaining -= step;
                summonTargetPointIndex--;
            }

            // Walking back off the start of the road is not possible, so the summon simply
            // appears at the spawn point.
            summonTargetPointIndex = Mathf.Max(summonTargetPointIndex, 1);
        }

        private static int CombineSeed(long bossId, int castVersion)
        {
            unchecked
            {
                return (int)((bossId * 397L) ^ (castVersion * 31L));
            }
        }
    }
}
