using System;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Level-scoped coordinator for gameplay HUD views and level navigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUIManager : MonoBehaviour, ILevelSceneParticipant
    {
        [SerializeField] private GridPlacementPresenter placementPresenter;
        [SerializeField] private PlacementHudView placementHud;
        [SerializeField] private TowerSelectionButton[] towerSelectionButtons = Array.Empty<TowerSelectionButton>();
        [SerializeField] private GameObject navigationRoot;
        [SerializeField] private TowerNetworkHudView towerNetworkHud;

        private TowerNetworkSceneAdapter towerNetworkAdapter;
        private TowerNetworkHudPresenter hudPresenter;

        public bool IsInitialized { get; private set; }

        public void Initialize(LevelSceneRuntimeContext context)
        {
            towerNetworkAdapter = placementPresenter.GetComponent<TowerNetworkSceneAdapter>();
            SetLegacyTowerSelectionVisible(false);
            towerNetworkHud.Initialize();
            hudPresenter = new TowerNetworkHudPresenter(
                towerNetworkAdapter,
                towerNetworkHud,
                context.RequestReturnToMenu);
            hudPresenter.Connect();
            IsInitialized = true;
            placementHud.Show();
            towerNetworkHud.Show();
            navigationRoot.SetActive(true);
            hudPresenter.Refresh();
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            hudPresenter.Shutdown();
            placementPresenter.CancelPlacement();
            towerNetworkHud.Shutdown();
            hudPresenter = null;
            towerNetworkAdapter = null;
            IsInitialized = false;
            placementHud.Hide();
            navigationRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private void Update()
        {
            if (IsInitialized)
            {
                hudPresenter.Tick();
            }
        }

        private void SetLegacyTowerSelectionVisible(bool visible)
        {
            for (int index = 0; index < towerSelectionButtons.Length; index++)
            {
                towerSelectionButtons[index].gameObject.SetActive(visible);
            }
        }
    }
}
