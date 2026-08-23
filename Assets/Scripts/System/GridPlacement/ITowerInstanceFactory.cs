using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface ITowerInstanceFactory
    {
        bool TryCreate(
            TowerDefinition definition,
            Vector3 position,
            out GameObject instance);

        void Destroy(GameObject instance);
    }
}
