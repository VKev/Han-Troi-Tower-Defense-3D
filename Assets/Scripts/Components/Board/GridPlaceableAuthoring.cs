using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [DisallowMultipleComponent]
    public sealed class GridPlaceableAuthoring : MonoBehaviour
    {
        [SerializeField] private string displayName = "Grid Placeable";
        [SerializeField] private Vector3 cellOffset;
        [SerializeField] private Vector3 baseEulerAngles;
        [SerializeField] private Vector3 scaleMultiplier = Vector3.one;
        [SerializeField] private GridPlaceableRotationMode rotationMode;
        [SerializeField] private GridPlaceableAxis isolatedAxis;
        [SerializeField] private bool hideAtCornerOrJunction = true;
        [SerializeField] private GameObject cornerPrefab;
        [SerializeField] private GameObject threeWayPrefab;
        [SerializeField] private GameObject fourWayPrefab;
        [SerializeField] private int rendererSortingOrder;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? gameObject.name
            : displayName;
        public Vector3 CellOffset => cellOffset;
        public Vector3 BaseEulerAngles => baseEulerAngles;
        public Vector3 ScaleMultiplier => scaleMultiplier;
        public GridPlaceableRotationMode RotationMode => rotationMode;
        public GridPlaceableAxis IsolatedAxis => isolatedAxis;
        public bool HideAtCornerOrJunction => hideAtCornerOrJunction;
        public GameObject CornerPrefab => cornerPrefab;
        public GameObject ThreeWayPrefab => threeWayPrefab;
        public GameObject FourWayPrefab => fourWayPrefab;
        public int RendererSortingOrder => rendererSortingOrder;

        public GridPlaceableDefinition Definition =>
            new GridPlaceableDefinition(
                gameObject,
                cornerPrefab,
                threeWayPrefab,
                fourWayPrefab,
                hideAtCornerOrJunction);

        public GameObject GetVisualPrefab(GridPlaceableTopology topology) =>
            Definition.GetVisualPrefab(topology);
    }
}
