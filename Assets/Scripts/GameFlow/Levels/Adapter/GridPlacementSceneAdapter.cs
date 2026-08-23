using System;
using TowerDefense3D.GridPlacement;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Adapts the scene-owned placement presenter to the level participant lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GridPlacementSceneAdapter : MonoBehaviour, ILevelSceneParticipant
    {
        [SerializeField] private GridPlacementPresenter placementPresenter;

        public void Initialize(LevelSceneRuntimeContext context)
        {
            if (!context.IsValid)
            {
                throw new ArgumentException(
                    "Placement received an invalid level runtime context.",
                    nameof(context));
            }

            if (placementPresenter == null)
            {
                throw new InvalidOperationException(
                    "GridPlacementSceneAdapter requires a GridPlacementPresenter.");
            }

            placementPresenter.Initialize();
        }

        public void Shutdown()
        {
            placementPresenter?.Shutdown();
        }
    }
}
