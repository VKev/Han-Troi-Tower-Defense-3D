using UnityEngine;

namespace TowerDefense3D.Components.Core
{
    internal static class RuntimeObjectDestroyer
    {
        public static void Destroy(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
