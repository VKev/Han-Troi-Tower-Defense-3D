using System;
using TowerDefense3D.GridPlacement;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Level-scoped coordinator for the migrated placement HUD and level navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUIManager : MonoBehaviour, ILevelSceneParticipant
    {
        [SerializeField] private GridPlacementController placementController;
        [SerializeField] private PlacementHudView placementHud;
        [SerializeField] private TowerSelectionButton[] towerSelectionButtons = Array.Empty<TowerSelectionButton>();
        [SerializeField] private Button cancelPlacementButton;
        [SerializeField] private Button returnToMenuButton;
        [SerializeField] private GameObject navigationRoot;

        private Action requestReturnToMenu;

        public bool IsInitialized { get; private set; }

        public void Initialize(LevelSceneRuntimeContext context)
        {
            if (!context.IsValid)
            {
                throw new ArgumentException("Gameplay UI received an invalid level runtime context.", nameof(context));
            }

            if (placementController == null)
            {
                throw new InvalidOperationException("GameplayUIManager requires a GridPlacementController.");
            }

            if (cancelPlacementButton == null || returnToMenuButton == null)
            {
                throw new InvalidOperationException(
                    "GameplayUIManager requires Cancel Placement and Return to Level Menu buttons.");
            }

            Shutdown();
            if (towerSelectionButtons == null)
            {
                towerSelectionButtons = Array.Empty<TowerSelectionButton>();
            }

            requestReturnToMenu = context.RequestReturnToMenu;
            try
            {
                Subscribe();
                IsInitialized = true;
                placementHud?.Show();
                if (navigationRoot != null)
                {
                    navigationRoot.SetActive(true);
                }
            }
            catch
            {
                Unsubscribe();
                requestReturnToMenu = null;
                IsInitialized = false;
                throw;
            }
        }

        public void Shutdown()
        {
            Unsubscribe();
            if (IsInitialized && placementController != null)
            {
                placementController.CancelPlacement();
            }

            requestReturnToMenu = null;
            IsInitialized = false;
            if (placementHud != null)
            {
                placementHud.Hide();
            }

            if (navigationRoot != null)
            {
                navigationRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Subscribe()
        {
            for (int index = 0; index < towerSelectionButtons.Length; index++)
            {
                TowerSelectionButton selectionButton = towerSelectionButtons[index];
                if (selectionButton != null)
                {
                    selectionButton.TowerRequested += HandleTowerRequested;
                }
            }

            cancelPlacementButton.onClick.AddListener(HandleCancelPlacement);
            returnToMenuButton.onClick.AddListener(HandleReturnToMenu);
        }

        private void Unsubscribe()
        {
            if (towerSelectionButtons != null)
            {
                for (int index = 0; index < towerSelectionButtons.Length; index++)
                {
                    TowerSelectionButton selectionButton = towerSelectionButtons[index];
                    if (selectionButton != null)
                    {
                        selectionButton.TowerRequested -= HandleTowerRequested;
                    }
                }
            }

            if (cancelPlacementButton != null)
            {
                cancelPlacementButton.onClick.RemoveListener(HandleCancelPlacement);
            }

            if (returnToMenuButton != null)
            {
                returnToMenuButton.onClick.RemoveListener(HandleReturnToMenu);
            }
        }

        private void HandleTowerRequested(TowerDefinition definition)
        {
            placementController.SelectTower(definition);
        }

        private void HandleCancelPlacement()
        {
            placementController.CancelPlacement();
        }

        private void HandleReturnToMenu()
        {
            placementController.CancelPlacement();
            requestReturnToMenu?.Invoke();
        }
    }
}
