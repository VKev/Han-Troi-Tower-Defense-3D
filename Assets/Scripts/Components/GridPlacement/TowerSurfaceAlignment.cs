using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Shared seating rule for tower visuals. Tower meshes are authored around their pivot
    /// instead of on top of it, so a tower dropped straight onto a board cell sinks by half
    /// its height. Both the runtime placement path and the board authoring path measure the
    /// same renderer bottom through here so a pre-placed tower stands exactly like one the
    /// player builds from a card.
    /// </summary>
    public static class TowerSurfaceAlignment
    {
        /// <summary>
        /// Distance the visual has to rise for the bottom of its renderers to meet the surface
        /// its pivot currently rests on. Works for a scene instance and for a prefab asset,
        /// whose root sits at its own local position.
        /// </summary>
        public static bool TryResolveSurfaceLift(GameObject root, out float lift)
        {
            lift = 0f;
            if (root == null)
            {
                return false;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds combinedBounds = default;

            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!renderer.enabled || !IsDrawn(renderer.transform, root.transform))
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

            if (!hasBounds)
            {
                return false;
            }

            lift = root.transform.position.y - combinedBounds.min.y;
            return true;
        }

        /// <summary>
        /// Walks the active flags by hand rather than reading <c>activeInHierarchy</c>, which is
        /// always false inside a prefab asset.
        /// </summary>
        private static bool IsDrawn(Transform renderer, Transform root)
        {
            for (Transform current = renderer; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }

                if (current == root)
                {
                    break;
                }
            }

            return true;
        }
    }
}
