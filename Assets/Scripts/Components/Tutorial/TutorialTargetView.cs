using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public sealed class TutorialTargetView : MonoBehaviour
    {
        [SerializeField] private string targetId;
        [SerializeField] private Vector3 worldBoundsSize;

        public string TargetId => targetId;
        public RectTransform RectTransform => transform as RectTransform;

        public void SetTargetId(string id)
        {
            targetId = id ?? string.Empty;
        }

        public void SetWorldBoundsSize(Vector3 size)
        {
            worldBoundsSize = new Vector3(
                Mathf.Max(0f, size.x),
                Mathf.Max(0f, size.y),
                Mathf.Max(0f, size.z));
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            if (worldBoundsSize.x <= 0f || worldBoundsSize.y <= 0f || worldBoundsSize.z <= 0f)
            {
                bounds = default;
                return false;
            }

            bounds = new Bounds(transform.position, worldBoundsSize);
            return true;
        }
    }
}
