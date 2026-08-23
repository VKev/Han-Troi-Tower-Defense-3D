using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Temporary scene compatibility surface while Gameplay UI moves to direct system injection.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerNetworkSceneAdapter : MonoBehaviour, ILevelSceneParticipant
    {
        private TowerNetworkSystem towerNetworkSystem;

        public event Action StateChanged
        {
            add => RequireSystem().StateChanged += value;
            remove
            {
                if (towerNetworkSystem != null)
                {
                    towerNetworkSystem.StateChanged -= value;
                }
            }
        }

        public bool IsInitialized { get; private set; }
        public TowerRuntimeView SelectedTower => towerNetworkSystem?.SelectedTower as TowerRuntimeView;
        public string LastFeedback => towerNetworkSystem?.LastFeedback ?? string.Empty;
        public bool HasValidChain => towerNetworkSystem?.HasValidChain == true;
        public int ValidChainCount => towerNetworkSystem?.ValidChainCount ?? 0;
        public bool IsRunning => towerNetworkSystem?.IsRunning == true;
        public bool CanEditTopology => towerNetworkSystem?.CanEditTopology == true;
        public int RegisteredTowerCount => towerNetworkSystem?.RegisteredTowerCount ?? 0;

        public void Bind(TowerNetworkSystem system)
        {
            if (towerNetworkSystem != null)
            {
                throw new InvalidOperationException("TowerNetworkSceneAdapter is already bound.");
            }

            towerNetworkSystem = system ?? throw new ArgumentNullException(nameof(system));
        }

        public void Initialize(LevelSceneRuntimeContext context)
        {
            _ = context;
            RequireSystem();
            IsInitialized = true;
        }

        public void Shutdown()
        {
            IsInitialized = false;
        }

        public IReadOnlyList<TowerRuntimeView> CreateTowerViewSnapshot()
        {
            if (towerNetworkSystem == null)
            {
                return Array.Empty<TowerRuntimeView>();
            }

            IReadOnlyList<ITowerRuntimeView> systemViews = towerNetworkSystem.CreateTowerViewSnapshot();
            var views = new List<TowerRuntimeView>(systemViews.Count);
            for (int index = 0; index < systemViews.Count; index++)
            {
                views.Add((TowerRuntimeView)systemViews[index]);
            }

            return views;
        }

        public bool TryGetTowerView(TowerNodeId nodeId, out TowerRuntimeView view)
        {
            if (towerNetworkSystem != null
                && towerNetworkSystem.TryGetTowerView(nodeId, out ITowerRuntimeView systemView))
            {
                view = (TowerRuntimeView)systemView;
                return true;
            }

            view = null;
            return false;
        }

        public bool TryRewire(TowerRuntimeView source, TowerRuntimeView target, out string error)
        {
            return RequireSystem().TryRewire(source, target, out error);
        }

        public bool BeginTowerPlacementDrag(TowerCombatDefinition definition, int pointerId)
        {
            return RequireSystem().BeginTowerPlacementDrag(definition, pointerId);
        }

        public void UpdateTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            RequireSystem().UpdateTowerPlacementDrag(pointerId, screenPosition, pointerOverUi);
        }

        public bool EndTowerPlacementDrag(int pointerId, Vector2 screenPosition, bool pointerOverUi)
        {
            return RequireSystem().EndTowerPlacementDrag(pointerId, screenPosition, pointerOverUi);
        }

        public void CancelTowerPlacementDrag(int pointerId)
        {
            RequireSystem().CancelTowerPlacementDrag(pointerId);
        }

        public void CancelPlacement()
        {
            towerNetworkSystem?.CancelPlacement();
        }

        public bool TryUnlinkSelected(out string error)
        {
            return RequireSystem().TryUnlinkSelected(out error);
        }

        public bool TryStartSimulation(out string error)
        {
            return RequireSystem().TryStartSimulation(out error);
        }

        public bool TryCreateSelectedQueueSummary(out TowerQueueSummary summary)
        {
            return RequireSystem().TryCreateSelectedQueueSummary(out summary);
        }

        private TowerNetworkSystem RequireSystem()
        {
            return towerNetworkSystem
                ?? throw new InvalidOperationException(
                    "LevelLifetimeScope must bind TowerNetworkSceneAdapter before scene participants initialize.");
        }
    }
}
