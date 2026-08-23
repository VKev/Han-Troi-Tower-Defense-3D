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

    /// <summary>
    /// Immutable data projected from one authored grid-placeable component.
    /// </summary>
    public readonly struct GridPlaceableDefinition
    {
        private readonly GameObject defaultPrefab;
        private readonly GameObject cornerPrefab;
        private readonly GameObject threeWayPrefab;
        private readonly GameObject fourWayPrefab;
        private readonly bool hideAtCornerOrJunction;

        public GridPlaceableDefinition(
            GameObject defaultPrefab,
            GameObject cornerPrefab,
            GameObject threeWayPrefab,
            GameObject fourWayPrefab,
            bool hideAtCornerOrJunction)
        {
            this.defaultPrefab = defaultPrefab;
            this.cornerPrefab = cornerPrefab;
            this.threeWayPrefab = threeWayPrefab;
            this.fourWayPrefab = fourWayPrefab;
            this.hideAtCornerOrJunction = hideAtCornerOrJunction;
        }

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
                    return defaultPrefab;
            }

            if (variant != null)
            {
                return variant;
            }

            return hideAtCornerOrJunction ? null : defaultPrefab;
        }
    }
}
