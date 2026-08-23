using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Unity prefab-instantiation boundary for placed tower instances.
    /// </summary>
    public sealed class TowerInstanceFactory : MonoBehaviour, ITowerInstanceFactory
    {
        [SerializeField] private Transform placedObjectsRoot;

        public bool TryCreate(
            TowerDefinition definition,
            Vector3 position,
            out GameObject instance)
        {
            if (definition.Prefab == null || placedObjectsRoot == null)
            {
                instance = null;
                return false;
            }

            try
            {
                instance = Instantiate(
                    definition.Prefab,
                    position,
                    definition.Prefab.transform.rotation,
                    placedObjectsRoot);
                return true;
            }
            catch (Exception exception)
            {
                instance = null;
                Debug.LogException(exception, this);
                return false;
            }
        }

        public void Destroy(GameObject instance)
        {
            UnityEngine.Object.Destroy(instance);
        }
    }
}
