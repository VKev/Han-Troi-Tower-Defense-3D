using System;
using System.Collections.Generic;
using TowerDefense3D.Audio;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.Tutorials;
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
        private readonly ISoundPlayer soundPlayer;
        private readonly ITutorialInputGate tutorialInputGate;

        private int pointerId;
        private Vector2 pressPosition;
        private Vector2 currentPosition;
        private ITowerRuntimeView pressedTower;
        private ITowerRuntimeView previewTarget;

        public TowerInteractionSystem(
            GameplayInputSystem inputSystem,
            TowerNetworkSystem towerNetworkSystem,
            Camera worldCamera,
            ISoundPlayer soundPlayer = null,
            ITutorialInputGate tutorialInputGate = null)
        {
            this.soundPlayer = soundPlayer;
            this.tutorialInputGate = tutorialInputGate;
            this.inputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
            this.worldCamera = worldCamera != null
                ? worldCamera
                : throw new ArgumentNullException(nameof(worldCamera));
        }

        public bool IsDraggingLink { get; private set; }

        /// <summary>
        /// Tower the current gesture started on. The link preview is drawn from here rather than
        /// from the selection, because pressing a tower to drag a link does not select it.
        /// </summary>
        public ITowerRuntimeView LinkSource => pressedTower;
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
                // Pressing bare ground dismisses the selected tower's actions.
                towerNetworkSystem.ClearSelection();
                return;
            }

            pointerId = startedPointerId;
            pressPosition = screenPosition;
            currentPosition = screenPosition;
            pressedTower = tower;
            soundPlayer?.Play(SoundId.TowerTouched);
            previewTarget = null;
            IsDraggingLink = false;
            inputSystem.SetMode(GameplayInputMode.TowerInteraction);
            towerNetworkSystem.CancelPlacement();
        }

        private void MovePointer(Vector2 screenPosition)
        {
            currentPosition = screenPosition;
            float dragThresholdSquared = LinkDragThresholdPixels * LinkDragThresholdPixels;

            // The gesture only becomes a link drag if a link could actually come of it. A wave
            // already running, or a hero under the finger, used to let the player drag a line
            // right across the board and only find out on release that nothing would attach.
            if (!IsDraggingLink
                && (tutorialInputGate == null || tutorialInputGate.Allows("link_towers"))
                && towerNetworkSystem.CanStartLinkFrom(pressedTower)
                && (screenPosition - pressPosition).sqrMagnitude >= dragThresholdSquared)
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
            else
            {
                // A press that never became a drag is a tap: that is what selects a tower and
                // brings up its actions.
                towerNetworkSystem.Select(pressedTower);
                towerNetworkSystem.ReportFeedback($"Selected {GetDisplayName(pressedTower)}.");
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
                soundPlayer?.Play(SoundId.LinkConnected);
                towerNetworkSystem.ReportFeedback(
                    $"Linked {GetDisplayName(pressedTower)} to {GetDisplayName(previewTarget)}.");
            }
            else
            {
                towerNetworkSystem.ReportFeedback(error);
            }
        }

        /// <summary>
        /// Whether a press here would land on a tower. Towers are picked by screen-space
        /// proximity and carry no colliders, so a camera gesture cannot ask physics whether it
        /// started on empty ground and has to consult the same radius the picker uses.
        /// </summary>
        internal bool IsOverTower(Vector2 screenPosition) =>
            TryPickTower(screenPosition, out _);

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
