using System;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.Towers;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Level-scoped coordinator for gameplay HUD views and level navigation.
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
        private TowerNetworkSceneAdapter towerNetworkAdapter;
        private TowerNetworkHudView towerNetworkHud;

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
            ResolveTowerNetworkDependencies();
            requestReturnToMenu = context.RequestReturnToMenu;

            try
            {
                SetLegacyTowerSelectionVisible(false);
                towerNetworkHud.Initialize(ResolveSafeArea(), towerNetworkAdapter.Catalog);
                Subscribe();
                IsInitialized = true;
                placementHud?.Show();
                towerNetworkHud.Show();
                if (navigationRoot != null)
                {
                    navigationRoot.SetActive(true);
                }

                RefreshTowerNetworkHud();
            }
            catch
            {
                Unsubscribe();
                towerNetworkHud?.Shutdown();
                requestReturnToMenu = null;
                towerNetworkAdapter = null;
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

            towerNetworkHud?.Shutdown();
            requestReturnToMenu = null;
            towerNetworkAdapter = null;
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

        private void Update()
        {
            if (IsInitialized && towerNetworkAdapter != null && towerNetworkAdapter.IsRunning)
            {
                RefreshTowerNetworkHud();
            }
        }

        private void Subscribe()
        {
            towerNetworkAdapter.StateChanged += RefreshTowerNetworkHud;
            towerNetworkHud.TowerRequested += HandleTowerRequested;
            towerNetworkHud.UnlinkRequested += HandleUnlinkRequested;
            towerNetworkHud.StartWaveRequested += HandleStartWaveRequested;
            cancelPlacementButton.onClick.AddListener(HandleCancelPlacement);
            returnToMenuButton.onClick.AddListener(HandleReturnToMenu);
        }

        private void Unsubscribe()
        {
            if (towerNetworkAdapter != null)
            {
                towerNetworkAdapter.StateChanged -= RefreshTowerNetworkHud;
            }

            if (towerNetworkHud != null)
            {
                towerNetworkHud.TowerRequested -= HandleTowerRequested;
                towerNetworkHud.UnlinkRequested -= HandleUnlinkRequested;
                towerNetworkHud.StartWaveRequested -= HandleStartWaveRequested;
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

        private void ResolveTowerNetworkDependencies()
        {
            towerNetworkAdapter = placementController.GetComponent<TowerNetworkSceneAdapter>();
            if (towerNetworkAdapter == null || !towerNetworkAdapter.IsInitialized)
            {
                throw new InvalidOperationException(
                    "GameplayUIManager requires an initialized TowerNetworkSceneAdapter beside the placement controller.");
            }

            towerNetworkHud = GetComponent<TowerNetworkHudView>();
            if (towerNetworkHud == null)
            {
                towerNetworkHud = gameObject.AddComponent<TowerNetworkHudView>();
            }
        }

        private Transform ResolveSafeArea()
        {
            SafeAreaFitter safeAreaFitter = GetComponentInChildren<SafeAreaFitter>(true);
            if (safeAreaFitter == null)
            {
                throw new InvalidOperationException("GameplayUIManager requires a SafeAreaFitter.");
            }

            return safeAreaFitter.transform;
        }

        private void SetLegacyTowerSelectionVisible(bool visible)
        {
            if (towerSelectionButtons == null)
            {
                towerSelectionButtons = Array.Empty<TowerSelectionButton>();
            }

            for (int index = 0; index < towerSelectionButtons.Length; index++)
            {
                TowerSelectionButton selectionButton = towerSelectionButtons[index];
                if (selectionButton != null)
                {
                    selectionButton.gameObject.SetActive(visible);
                }
            }
        }

        private void HandleTowerRequested(TowerCombatDefinition definition)
        {
            towerNetworkAdapter.SelectTowerForPlacement(definition);
        }

        private void HandleCancelPlacement()
        {
            towerNetworkAdapter.CancelPlacement();
            RefreshTowerNetworkHud();
        }

        private void HandleUnlinkRequested()
        {
            towerNetworkAdapter.TryUnlinkSelected(out _);
        }

        private void HandleStartWaveRequested()
        {
            towerNetworkAdapter.TryStartSimulation(out _);
        }

        private void HandleReturnToMenu()
        {
            towerNetworkAdapter.CancelPlacement();
            requestReturnToMenu?.Invoke();
        }

        private void RefreshTowerNetworkHud()
        {
            if (towerNetworkAdapter == null || towerNetworkHud == null || !towerNetworkHud.IsInitialized)
            {
                return;
            }

            TowerRuntimeView selectedTower = towerNetworkAdapter.SelectedTower;
            string selectedText = selectedTower == null
                ? "Selected: None"
                : $"Selected: {selectedTower.CombatDefinition.Core.DisplayName} ({selectedTower.CombatDefinition.NetworkRole})";
            string chainText = $"Valid chains: {towerNetworkAdapter.ValidChainCount}"
                + $"   Towers: {towerNetworkAdapter.RegisteredTowerCount}";
            string queueText = CreateQueueText(towerNetworkAdapter, selectedTower);
            string feedbackText = string.IsNullOrWhiteSpace(towerNetworkAdapter.LastFeedback)
                ? "Place towers, then drag one tower to another."
                : towerNetworkAdapter.LastFeedback;
            bool simulationRunning = towerNetworkAdapter.IsRunning;

            towerNetworkHud.Render(new TowerNetworkHudState(
                selectedText,
                chainText,
                queueText,
                feedbackText,
                !simulationRunning,
                selectedTower != null && towerNetworkAdapter.CanEditTopology,
                towerNetworkAdapter.HasValidChain && !simulationRunning,
                simulationRunning ? "RUNNING" : "START WAVE"));
            cancelPlacementButton.interactable = !simulationRunning;
        }

        private static string CreateQueueText(
            TowerNetworkSceneAdapter adapter,
            TowerRuntimeView selectedTower)
        {
            if (selectedTower == null)
            {
                return "Queue: select a tower";
            }

            if (!adapter.TryCreateSelectedQueueSummary(out TowerQueueSummary queue))
            {
                return "Queue: unavailable";
            }

            if (queue.Capacity == 0)
            {
                return "Queue: source tower has no input queue";
            }

            return $"Queue: {queue.QueuedProjectileCount} queued + {queue.ReservedProjectileCount} reserved"
                + $" / {queue.Capacity}";
        }
    }
}
