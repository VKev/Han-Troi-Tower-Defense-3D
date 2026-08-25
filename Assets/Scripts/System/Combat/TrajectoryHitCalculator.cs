using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public static class TrajectoryHitCalculator
    {
        public static bool IntersectsXZ(
            Vector3 projectileStart,
            Vector3 projectileEnd,
            Vector3 enemyStart,
            Vector3 enemyEnd,
            float combinedRadius)
        {
            Vector2 relativeStart = new Vector2(
                projectileStart.x - enemyStart.x,
                projectileStart.z - enemyStart.z);
            Vector2 relativeDelta = new Vector2(
                (projectileEnd.x - projectileStart.x) - (enemyEnd.x - enemyStart.x),
                (projectileEnd.z - projectileStart.z) - (enemyEnd.z - enemyStart.z));

            float relativeLengthSquared = relativeDelta.sqrMagnitude;
            float closestTime = relativeLengthSquared <= float.Epsilon
                ? 0f
                : Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeDelta) / relativeLengthSquared);
            Vector2 closestOffset = relativeStart + relativeDelta * closestTime;
            return closestOffset.sqrMagnitude <= combinedRadius * combinedRadius;
        }
    }
}
