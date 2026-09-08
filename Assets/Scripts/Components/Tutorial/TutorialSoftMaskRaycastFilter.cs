using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TutorialSoftMaskRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private Rect[] focuses = System.Array.Empty<Rect>();

        public void SetFocus(Rect[] screenRects)
        {
            focuses = screenRects ?? System.Array.Empty<Rect>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            for (int index = 0; index < focuses.Length; index++)
            {
                Rect focus = focuses[index];
                if (focus.width <= 0f || focus.height <= 0f) continue;
                Vector2 normalized = new Vector2(
                    (screenPoint.x - focus.center.x) / (focus.width * 0.5f),
                    (screenPoint.y - focus.center.y) / (focus.height * 0.5f));
                if (normalized.sqrMagnitude <= 1f) return false;
            }

            return true;
        }
    }
}
