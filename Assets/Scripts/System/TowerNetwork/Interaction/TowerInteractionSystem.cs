using System;
using System.Collections.Generic;
using TowerDefense3D.GameplayInput;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Consumes the shared gameplay pointer snapshot for tower selection and link gestures.
    /// </summary>
    public sealed class TowerInteractionSystem
    {
        private const float SelectionRadiusPixels = 96f;
        private const float LinkDragThresholdPixels = 24f;

        private readonly GameplayInputSystem inputSystem;
        private readonly TowerNetworkSystem towerNetworkSystem;
        private readonly Camera worldCamera;

        private int pointerId;
        private Vector2 pressPosition;
        private Vector2 currentPosition;
        private ITowerRuntimeView pressedTower;
        private ITowerRuntimeView previewTarget;

        public TowerInteractionSystem(
            GameplayInputSystem inputSystem,
            TowerNetworkSystem towerNetworkSystem,
            Camera worldCamera)
        {
            this.inputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.worldCamera = worldCamera != null
                ? worldCamera
                : throw new ArgumentNullException(nameof(worldCamera));
        }

        public bool IsDraggingLink { get; private set; }
        public ITowerRuntimeView PreviewTarget => previewTarget;
        public Vector3 PreviewWorldPosition => CalculatePreviewWorldPosition();

        public void Tick()
        {
            GameplayInputSnapshot input = inputSystem.Current;
            if (input.WasInterrupted)
            {
                ResetPointer();
                inputSystem.ClearMode(GameplayInputMode.TowerInteraction);
                return;
            }

            if (inputSystem.Mode == GameplayInputMode.GridPlacement)
            {
                ResetPointer();
                return;
            }

            if (!input.HasPointerInput)
            {
                return;
            }

            if (input.WasPressed
                && inputSystem.Mode == GameplayInputMode.None
                && !input.IsPointerOverUi)
            {
                BeginPointer(input.PointerId, input.ScreenPosition);
            }

            if (pressedTower != null
                && input.PointerId == pointerId
                && input.IsPressed)
            {
                MovePointer(input.ScreenPosition);
            }

            if (pressedTower != null
                && input.PointerId == pointerId
                && input.WasReleased)
            {
                EndPointer(input.ScreenPosition);
            }
        }

        private void BeginPointer(int startedPointerId, Vector2 screenPosition)
        {
            if (!TryPickTower(screenPosition, out ITowerRuntimeView tower))
            {
                return;
            }

            pointerId = startedPointerId;
            pressPosition = screenPosition;
            currentPosition = screenPosition;
            pressedTower = tower;
            previewTarget = null;
            IsDraggingLink = false;
            inputSystem.SetMode(GameplayInputMode.TowerInteraction);
            towerNetworkSystem.CancelPlacement();
            towerNetworkSystem.Select(tower);
            towerNetworkSystem.ReportFeedback($"Selected {GetDisplayName(tower)}.");
        }

        private void MovePointer(Vector2 screenPosition)
        {
            currentPosition = screenPosition;
            float dragThresholdSquared = LinkDragThresholdPixels * LinkDragThresholdPixels;
            if (!IsDraggingLink && (screenPosition - pressPosition).sqrMagnitude >= dragThresholdSquared)
            {
                IsDraggingLink = true;
            }

            previewTarget = IsDraggingLink
                && TryPickTower(screenPosition, out ITowerRuntimeView target)
                && !ReferenceEquals(target, pressedTower)
                    ? target
                    : null;
        }

        private void EndPointer(Vector2 screenPosition)
        {
            MovePointer(screenPosition);
            if (IsDraggingLink)
            {
                CompleteLinkGesture();
            }

            ResetPointer();
            inputSystem.ClearMode(GameplayInputMode.TowerInteraction);
        }

        private void CompleteLinkGesture()
        {
            if (previewTarget == null)
            {
                towerNetworkSystem.ReportFeedback("Link cancelled: release over another tower.");
                return;
            }

            if (towerNetworkSystem.TryRewire(pressedTower, previewTarget, out string error))
            {
                towerNetworkSystem.ReportFeedback(
                    $"Linked {GetDisplayName(pressedTower)} to {GetDisplayName(previewTarget)}.");
            }
            else
            {
                towerNetworkSystem.ReportFeedback(error);
            }
        }

        private bool TryPickTower(Vector2 screenPosition, out ITowerRuntimeView closestTower)
        {
            IReadOnlyList<ITowerRuntimeView> towers = towerNetworkSystem.CreateTowerViewSnapshot();
            float closestDistanceSquared = SelectionRadiusPixels * SelectionRadiusPixels;
            closestTower = null;

            for (int index = 0; index < towers.Count; index++)
            {
                ITowerRuntimeView tower = towers[index];
                Vector3 towerScreenPosition = worldCamera.WorldToScreenPoint(tower.PresentationAnchor);
                if (towerScreenPosition.z <= 0f)
                {
                    continue;
                }

                float distanceSquared = ((Vector2)towerScreenPosition - screenPosition).sqrMagnitude;
                if (distanceSquared <= closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestTower = tower;
                }
            }

            return closestTower != null;
        }

        private Vector3 CalculatePreviewWorldPosition()
        {
            if (previewTarget != null)
            {
                return previewTarget.PresentationAnchor;
            }

            float depth = worldCamera.WorldToScreenPoint(pressedTower.PresentationAnchor).z;
            return worldCamera.ScreenToWorldPoint(new Vector3(currentPosition.x, currentPosition.y, depth));
        }

        private void ResetPointer()
        {
            pointerId = 0;
            pressPosition = default;
            currentPosition = default;
            pressedTower = null;
            previewTarget = null;
            IsDraggingLink = false;
        }

        private static string GetDisplayName(ITowerRuntimeView tower)
        {
            return tower.CombatDefinition.Core.DisplayName;
        }
    }
}
