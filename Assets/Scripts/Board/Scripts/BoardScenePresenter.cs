using UnityEngine;
using UnityEngine.Serialization;

namespace TowerDefense3D.GridPlacement
{
    public sealed class BoardScenePresenter : MonoBehaviour
    {
        [SerializeField] private BoardDefinition board;
        [SerializeField] private Transform generatedRoot;
        [SerializeField, HideInInspector] private string generatedSignature;
        [FormerlySerializedAs("generatedRoadVisualRoot")]
        [SerializeField, HideInInspector] private Transform generatedGridPlaceableRoot;
        [FormerlySerializedAs("generatedRoadVisualSignature")]
        [SerializeField, HideInInspector] private string generatedGridPlaceableSignature;

        public BoardDefinition Board => board;
        public Transform GeneratedRoot => generatedRoot;
        public Transform GeneratedGridPlaceableRoot => generatedGridPlaceableRoot;

        private void OnEnable()
        {
            ApplyVisibility();
        }

        public void ApplyVisibility()
        {
            if (board == null || generatedRoot == null)
            {
                return;
            }

            MeshRenderer[] renderers = generatedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                MeshRenderer renderer = renderers[index];
                if (renderer != null)
                {
                    renderer.enabled = board.VisualizeInScene;
                }
            }
        }
    }
}
