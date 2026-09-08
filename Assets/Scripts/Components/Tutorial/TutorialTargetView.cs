using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    public sealed class TutorialTargetView : MonoBehaviour
    {
        [SerializeField] private string targetId;
        public string TargetId => targetId;
        public RectTransform RectTransform => transform as RectTransform;

        public void SetTargetId(string id)
        {
            targetId = id ?? string.Empty;
        }
    }
}
