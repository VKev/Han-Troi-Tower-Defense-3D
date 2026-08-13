using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public sealed class BoardScenePresenter : MonoBehaviour
    {
        [SerializeField] private BoardDefinition board;
        [SerializeField] private Transform generatedRoot;
        [SerializeField, HideInInspector] private string generatedSignature;

        public BoardDefinition Board => board;
        public Transform GeneratedRoot => generatedRoot;

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
