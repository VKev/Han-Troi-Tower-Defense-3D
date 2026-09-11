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

        /// <summary>
        /// How slowly the board runs while a beat is being watched rather than frozen.
        /// </summary>
        private const float SlowMotionScale = 0.25f;

        /// <summary>
        /// How far ahead of a planned reaction the beat opens, in simulation ticks.
        /// </summary>
        /// <remarks>
        /// Long enough at 20 ticks per second to cover the dolly-in and leave the reaction still
        /// to come, which is the whole point: the player is meant to watch it land, not be told
        /// about it afterwards.
        /// </remarks>
        private const long ReactionLeadTicks = 30L;

        private Tween dolly;
        private Vector3 restPosition;
        private bool isEntered;
        private bool captureFirstFireHit;
        private bool isSlowMotion;
        private bool captureFirstThermalShock;
        private bool hasSeenThermalShock;
        private bool loggedShockFound;

        /// <summary>
        /// Temporary: traces why the thermal-shock beat does or does not open.
        /// </summary>
        /// <remarks>
        /// Public rather than internal because the level scope reads it, and that lives in
        /// TowerDefense3D.Application.Runtime - a different assembly, which internal does not
        /// reach. Only TowerDefense3D.EditModeTests is granted internals access here.
        /// </remarks>
        public const bool LogThermalShockDiagnostics = true;

        // Stays true through the dolly back, so a level torn down mid-beat cannot leave the
        // simulation paused or the camera parked off its framing.
        private bool isHoldingBoard;

        public TutorialFocusSystem(
            Camera camera,
            GameplaySimulationSystem simulation,
            CombatTimelineSystem combatTimeline,
            IGameplayUIView gameplayUi)
        {
            // Subscribed before the field assignments below so the handler is wired against the
            // same instance the constructor is validating.
            this.camera = camera ?? throw new ArgumentNullException(nameof(camera));
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            this.combatTimeline = combatTimeline ?? throw new ArgumentNullException(nameof(combatTimeline));
            this.gameplayUi = gameplayUi ?? throw new ArgumentNullException(nameof(gameplayUi));
            combatTimeline.FireHitResolved += HandleFireHitResolved;
            combatTimeline.ReactionTriggered += HandleReactionTriggered;
        }

        /// <summary>
        /// Whether this beat currently owns the camera. Player camera gestures stand down while
        /// it does: the dolly writes the transform directly and a gesture would reframe it away.
        /// </summary>
        public bool IsHoldingBoard => isHoldingBoard;
        public long FirstFireHitEnemyId { get; private set; }

        /// <summary>
        /// The enemy the first thermal shock of this wave is planned to land on, once that
        /// reaction is close enough to stage a beat around. Zero until then.
        /// </summary>
        public long PendingThermalShockEnemyId { get; private set; }

        /// <summary>Whether the planned thermal shock has now actually happened.</summary>
        public bool HasSeenThermalShock => hasSeenThermalShock;

        public void SetThermalShockCaptureEnabled(bool enabled)
        {
            captureFirstThermalShock = enabled;
            if (!enabled)
            {
                PendingThermalShockEnemyId = 0L;
                hasSeenThermalShock = false;
            }
        }

        /// <summary>
        /// Looks ahead in the planned wave and reports when the first thermal shock is close
        /// enough to open the beat on.
        /// </summary>
        /// <remarks>
        /// Polled rather than driven by an event because the reaction has not happened yet. The
        /// timeline is planned before the wave is played, so the beat can be opened in front of
        /// the reaction instead of chasing it.
        /// </remarks>
        public bool TryBeginThermalShockBeat(out Vector3 worldPoint)
        {
            worldPoint = default;
            if (!captureFirstThermalShock || PendingThermalShockEnemyId != 0L || hasSeenThermalShock)
            {
                return false;
            }

            bool found = combatTimeline.TryFindUpcomingReaction(
                ElementReactionId.ThermalShock,
                out long ticksUntil,
                out long enemyId,
                out Vector3 position);
            if (LogThermalShockDiagnostics && found != loggedShockFound)
            {
                loggedShockFound = found;
                Debug.Log($"[ThermalShock] planned={found} ticksUntil={ticksUntil} enemyId={enemyId}");
            }

            if (!found || ticksUntil > ReactionLeadTicks || enemyId == 0L)
            {
                return false;
            }

            PendingThermalShockEnemyId = enemyId;
            worldPoint = position;
            if (LogThermalShockDiagnostics)
            {
                Debug.Log($"[ThermalShock] beat opened on enemy {enemyId} at {position}");
            }

            return true;
        }

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
            Enter(worldFocusPoint, freezeBoard: true);
        }

        /// <summary>
        /// Dollies onto a point, either stopping the board or merely slowing it.
        /// </summary>
        /// <remarks>
        /// Slowing is for a beat whose subject is still to come: a frozen board can never show the
        /// reaction the text is describing, so that beat has to keep time moving while it reads.
        /// </remarks>
        public void Enter(Vector3 worldFocusPoint, bool freezeBoard)
        {
            if (isEntered || camera == null)
            {
                return;
            }

            if (LogThermalShockDiagnostics && !freezeBoard)
            {
                Debug.Log($"[ThermalShock] focus entered, slow motion at {worldFocusPoint}");
            }

            isEntered = true;
            isHoldingBoard = true;
            isSlowMotion = !freezeBoard;
            restPosition = camera.transform.position;
            if (freezeBoard)
            {
                simulation.SetPaused(true);
            }
            else
            {
                // Double speed is dropped first. The beat exists to be watched, and slowing a
                // board that is already running at twice the rate would only bring it back to
                // something near normal - the player would be shown nothing they could follow.
                simulation.SetFastForward(false);
                simulation.SetTutorialTimeScale(SlowMotionScale);
            }
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
            bool wasSlowMotion = isSlowMotion;
            isEntered = false;
            isSlowMotion = false;
            captureFirstFireHit = false;
            captureFirstThermalShock = false;
            simulation.SetTutorialTimeScale(1f);
            combatTimeline.ReleaseHeldEnemyFrame();
            gameplayUi.SetTutorialFocusVisible(true);
            dolly?.Kill();
            if (!hadCameraFocus || camera == null)
            {
                isHoldingBoard = false;
                simulation.SetPaused(false);
                return;
            }

            // A slowed beat never paused, so it has nothing to resume and can let go of the board
            // as soon as the camera starts home. Unpausing here would be harmless but untrue.
            if (wasSlowMotion)
            {
                dolly = camera.transform
                    .DOMove(restPosition, DollySeconds)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .OnComplete(() => isHoldingBoard = false);
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
            combatTimeline.ReactionTriggered -= HandleReactionTriggered;
            dolly?.Kill();
            dolly = null;
            isEntered = false;
            isSlowMotion = false;
            captureFirstFireHit = false;
            captureFirstThermalShock = false;
            simulation.SetTutorialTimeScale(1f);
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

        private void HandleReactionTriggered(ElementReactionEvent reaction)
        {
            if (LogThermalShockDiagnostics && reaction.ReactionId == ElementReactionId.ThermalShock)
            {
                Debug.Log($"[ThermalShock] reaction fired on enemy {reaction.EnemyId}"
                    + $" (pending={PendingThermalShockEnemyId})");
            }

            if (reaction.ReactionId == ElementReactionId.ThermalShock
                && PendingThermalShockEnemyId != 0L
                && reaction.EnemyId == PendingThermalShockEnemyId)
            {
                hasSeenThermalShock = true;
            }
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
