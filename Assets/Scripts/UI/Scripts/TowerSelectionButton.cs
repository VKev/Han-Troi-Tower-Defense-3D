using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Focused view that publishes one authored tower-selection request.
    /// GameplayUIManager owns the controller binding.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class TowerSelectionButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TowerDefinition towerDefinition;

        public event Action<TowerDefinition> TowerRequested;

        public TowerDefinition Definition => towerDefinition;

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
            TowerRequested?.Invoke(towerDefinition);
        }
    }
}
