using UnityEngine;

namespace TowerDefense3D.Towers
{
    public interface ITowerProjectileViewPool
    {
        int ActiveViewCount { get; }
        int InactiveViewCount { get; }

        void Initialize();
        void Show(long projectileId, GameObject projectilePrefab, Vector3 position);
        void Release(long projectileId);
        void AdvanceReleaseDelays(float deltaTime);
        void Clear();
    }
}
