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
                AlignRendererBottomWithSurface(instance, position.y);
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

        private static void AlignRendererBottomWithSurface(GameObject instance, float surfaceY)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!renderer.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                instance.transform.position += Vector3.up * (surfaceY - combinedBounds.min.y);
            }
        }
    }
}
