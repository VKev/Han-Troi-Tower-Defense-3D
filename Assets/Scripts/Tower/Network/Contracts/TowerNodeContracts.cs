using System;

namespace TowerDefense3D.Towers
{
    public readonly struct TowerNodeId : IEquatable<TowerNodeId>
    {
        public TowerNodeId(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public bool Equals(TowerNodeId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is TowerNodeId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }

    public readonly struct TowerWorldPosition
    {
        public TowerWorldPosition(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public static float Distance(TowerWorldPosition first, TowerWorldPosition second)
        {
            float deltaX = second.X - first.X;
            float deltaY = second.Y - first.Y;
            float deltaZ = second.Z - first.Z;
            float squaredDistance = (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);

            return (float)Math.Sqrt(squaredDistance);
        }

        public static TowerWorldPosition MoveTowards(
            TowerWorldPosition current, TowerWorldPosition target, float maximumDistanceDelta)
        {
            if (maximumDistanceDelta < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDistanceDelta), "Movement distance cannot be negative.");
            }

            float deltaX = target.X - current.X;
            float deltaY = target.Y - current.Y;
            float deltaZ = target.Z - current.Z;
            float distance = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));

            if (distance <= maximumDistanceDelta || distance <= float.Epsilon)
            {
                return target;
            }

            float scale = maximumDistanceDelta / distance;
            return new TowerWorldPosition(current.X + (deltaX * scale), current.Y + (deltaY * scale),
                current.Z + (deltaZ * scale));
        }
    }
}
