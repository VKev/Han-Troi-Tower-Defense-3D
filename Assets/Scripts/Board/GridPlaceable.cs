using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public enum GridPlaceableRotationMode
    {
        Fixed,
        StraightAlongMatchingNeighbors,
    }

    public enum GridPlaceableAxis
    {
        X,
        Z,
    }

    public enum GridPlaceableTopology
    {
        Isolated,
        End,
        Straight,
        Corner,
        ThreeWay,
        FourWay,
    }

    [DisallowMultipleComponent]
    public sealed class GridPlaceable : MonoBehaviour
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

        public GameObject GetVisualPrefab(GridPlaceableTopology topology)
        {
            GameObject variant;
            switch (topology)
            {
                case GridPlaceableTopology.Corner:
                    variant = cornerPrefab;
                    break;
                case GridPlaceableTopology.ThreeWay:
                    variant = threeWayPrefab;
                    break;
                case GridPlaceableTopology.FourWay:
                    variant = fourWayPrefab;
                    break;
                default:
                    return gameObject;
            }

            return variant != null || !hideAtCornerOrJunction
                ? variant != null ? variant : gameObject
                : null;
        }
    }
}
