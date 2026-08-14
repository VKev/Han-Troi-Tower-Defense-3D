using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    [CreateAssetMenu(fileName = "TowerDefinition", menuName = "Tower Defense/Grid Placement/Tower Definition")]
    public sealed class TowerDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private TowerFootprint footprint = new TowerFootprint(1, 1, 1);

        public GameObject Prefab => prefab;
        public TowerFootprint Footprint => footprint;
    }
}
