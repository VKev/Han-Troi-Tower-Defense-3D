using System;
using DG.Tweening;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameFlow;
using TowerDefense3D.Simulation;
using UnityEngine;

namespace TowerDefense3D.Tutorials
{
    /// <summary>
    /// Freezes the level simulation and dollies the board camera toward one world point, so a
    /// tutorial beat can point at something that happens in the middle of a running wave.
    /// </summary>
    public sealed class TutorialFocusSystem : IDisposable
    {
        private const float DollySeconds = 0.5f;

        // How far toward the focus point the camera travels. Only the position moves: the board
        // camera re-frames itself whenever it sees its field of view or rotation change, so
        // tweening either of those would be counter-framed away the very next frame.
        private const float DollyFactor = 0.35f;

        private readonly Camera camera;
        private readonly GameplaySimulationSystem simulation;
        private readonly CombatTimelineSystem combatTimeline;
        private readonly IGameplayUIView gameplayUi;

        private Tween dolly;
        private Vector3 restPosition;
        private bool isEntered;
        private bool captureFirstFireHit;

        // Stays true through the dolly back, so a level torn down mid-beat cannot leave the
        // simulation paused or the camera parked off its framing.
        private bool isHoldingBoard;

        public TutorialFocusSystem(
            Camera camera,
            GameplaySimulationSystem simulation,
            CombatTimelineSystem combatTimeline,
            IGameplayUIView gameplayUi)
        {
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.combatTimeline = combatTimeline ?? throw new ArgumentNullException(nameof(combatTimeline));
            this.gameplayUi = gameplayUi ?? throw new ArgumentNullException(nameof(gameplayUi));
            combatTimeline.FireHitResolved += HandleFireHitResolved;
        }

        /// <summary>
        /// Whether this beat currently owns the camera. Player camera gestures stand down while
        /// it does: the dolly writes the transform directly and a gesture would reframe it away.
        /// </summary>
        public bool IsHoldingBoard => isHoldingBoard;
        public long FirstFireHitEnemyId { get; private set; }

        public void SetFirstFireHitCaptureEnabled(bool enabled)
        {
            captureFirstFireHit = enabled;
            if (!enabled)
            {
                FirstFireHitEnemyId = 0L;
            }
        }

        public void Enter(Vector3 worldFocusPoint)
        {
            if (isEntered || camera == null)
            {
                return;
            }

            isEntered = true;
            isHoldingBoard = true;
            restPosition = camera.transform.position;
            simulation.SetPaused(true);
            gameplayUi.SetTutorialFocusVisible(false);
            dolly?.Kill();
            dolly = camera.transform
                .DOMove(Vector3.Lerp(restPosition, worldFocusPoint, DollyFactor), DollySeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        public void Exit()
        {
            if (!isHoldingBoard)
            {
                return;
            }

            bool hadCameraFocus = isEntered;
            isEntered = false;
            captureFirstFireHit = false;
            combatTimeline.ReleaseHeldEnemyFrame();
            gameplayUi.SetTutorialFocusVisible(true);
            dolly?.Kill();
            if (!hadCameraFocus || camera == null)
            {
                isHoldingBoard = false;
                simulation.SetPaused(false);
                return;
            }

            // The simulation resumes only once the camera is home again, so the player watches the
            // frozen board pull back before it starts moving.
            dolly = camera.transform
                .DOMove(restPosition, DollySeconds)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    isHoldingBoard = false;
                    simulation.SetPaused(false);
                });
        }

        public void Dispose()
        {
            combatTimeline.FireHitResolved -= HandleFireHitResolved;
            dolly?.Kill();
            dolly = null;
            isEntered = false;
            captureFirstFireHit = false;
            if (!isHoldingBoard)
            {
                return;
            }

            isHoldingBoard = false;
            if (camera != null)
            {
                camera.transform.position = restPosition;
            }

            simulation.SetPaused(false);
        }

        private void HandleFireHitResolved(FireHitEvent hit)
        {
            if (!captureFirstFireHit || FirstFireHitEnemyId != 0L)
            {
                return;
            }

            FirstFireHitEnemyId = hit.EnemyId;
            isHoldingBoard = true;
            simulation.SetPaused(true);
            combatTimeline.HoldLethalFrame(hit.EnemyId);
        }
    }
}
