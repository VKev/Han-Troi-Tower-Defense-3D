using System;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [Serializable]
    public struct GridPlaceablePlacement
    {
        [SerializeField] private GridCell coordinate;
        [SerializeField] private GameObject prefab;

        public GridPlaceablePlacement(GridCell coordinate, GameObject prefab)
        {
            this.coordinate = coordinate;
            this.prefab = prefab;
        }

        public GridCell Coordinate => coordinate;
        public GameObject Prefab => prefab;
    }
}
