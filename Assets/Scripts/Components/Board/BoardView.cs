using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour, IBoardView
    {
        [SerializeField] private BoardDefinition board;
        [SerializeField] private Transform generatedRoot;
        [SerializeField, HideInInspector] private string generatedSignature;
        [SerializeField, HideInInspector] private Transform generatedGridPlaceableRoot;
        [SerializeField, HideInInspector] private string generatedGridPlaceableSignature;

        public BoardDefinition Board => board;
        public Vector3 WorldOrigin => transform.position;
        public Transform GeneratedRoot => generatedRoot;
        public Transform GeneratedGridPlaceableRoot => generatedGridPlaceableRoot;

        public void ApplyVisibility(bool visible)
        {
            if (generatedRoot == null)
            {
                return;
            }

            MeshRenderer[] renderers = generatedRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].enabled = visible;
            }
        }
    }
}
