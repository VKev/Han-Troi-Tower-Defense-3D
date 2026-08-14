using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Small scene adapter that keeps tower selection wiring Inspector-friendly.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class TowerSelectionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GridPlacementController controller;
        [SerializeField] private TowerDefinition towerDefinition;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            button?.onClick.AddListener(SelectTower);
        }

        private void OnDisable()
        {
            button?.onClick.RemoveListener(SelectTower);
        }

        public void SelectTower()
        {
            controller?.SelectTower(towerDefinition);
        }
    }
}
