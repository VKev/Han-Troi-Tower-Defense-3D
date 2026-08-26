using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public static class TrajectoryHitCalculator
    {
        public static bool TryFindFirstIntersectionTimeXZ(
            Vector3 firstPosition,
            Vector3 firstVelocity,
            Vector3 secondPosition,
            Vector3 secondVelocity,
            float durationSeconds,
            float combinedRadius,
            out float intersectionTimeSeconds)
        {
            Vector2 relativePosition = new Vector2(
                firstPosition.x - secondPosition.x,
                firstPosition.z - secondPosition.z);
            Vector2 relativeVelocity = new Vector2(
                firstVelocity.x - secondVelocity.x,
                firstVelocity.z - secondVelocity.z);
            double radiusSquared = combinedRadius * combinedRadius;
            double constant = relativePosition.sqrMagnitude - radiusSquared;
            if (constant <= 0d)
            {
                intersectionTimeSeconds = 0f;
                return true;
            }

            double quadratic = relativeVelocity.sqrMagnitude;
            if (quadratic <= double.Epsilon)
            {
                intersectionTimeSeconds = 0f;
                return false;
            }

            double linear = 2d * Vector2.Dot(relativePosition, relativeVelocity);
            double discriminant = linear * linear - 4d * quadratic * constant;
            if (discriminant < 0d)
            {
                intersectionTimeSeconds = 0f;
                return false;
            }

            double squareRoot = System.Math.Sqrt(discriminant);
            double entryTime = (-linear - squareRoot) / (2d * quadratic);
            double exitTime = (-linear + squareRoot) / (2d * quadratic);
            if (exitTime < 0d || entryTime > durationSeconds)
            {
                intersectionTimeSeconds = 0f;
                return false;
            }

            intersectionTimeSeconds = (float)System.Math.Max(0d, entryTime);
            return intersectionTimeSeconds <= durationSeconds;
        }
    }
}
