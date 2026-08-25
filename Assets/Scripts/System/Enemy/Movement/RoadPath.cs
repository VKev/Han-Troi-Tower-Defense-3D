using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public sealed class RoadPath
    {
        private readonly Vector3[] points;

        public RoadPath(IReadOnlyList<Vector3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (points.Count < 2)
            {
                throw new ArgumentException("A road path requires at least two points.", nameof(points));
            }

            this.points = new Vector3[points.Count];
            for (int index = 0; index < points.Count; index++)
            {
                this.points[index] = points[index];
            }
        }

        public Vector3 Start => points[0];
        public Vector3 End => points[points.Length - 1];
        public int PointCount => points.Length;

        public bool Move(
            ref int targetPointIndex,
            ref Vector3 position,
            float distance)
        {
            while (distance > 0f && targetPointIndex < points.Length)
            {
                Vector3 target = points[targetPointIndex];
                float remaining = Vector3.Distance(position, target);
                if (remaining > distance)
                {
                    position = Vector3.MoveTowards(position, target, distance);
                    return false;
                }

                position = target;
                distance -= remaining;
                targetPointIndex++;
            }

            return targetPointIndex >= points.Length;
        }

        internal Vector3 GetPoint(int index)
        {
            return points[index];
        }
    }
}
