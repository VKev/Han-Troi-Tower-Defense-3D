using System;
using TowerDefense3D.GameplayInput;
using TowerDefense3D.Towers;
using TowerDefense3D.Tutorials;
using TowerDefense3D.Waves;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Turns pinch, wheel and drag input into the viewer offset the board camera folds into its
    /// framing pose.
    /// </summary>
    /// <remarks>
    /// Zoom is kept as a scalar in [0,1] rather than as a world position, because the pose zero
    /// stands for is recomputed live: the framing solver answers differently once the screen
    /// rotates, the safe area changes, or play mode switches the runtime safe area on. Zero always
    /// means "the computed board framing", which is also the zoom-out limit, so the player can
    /// only ever zoom in from the opening shot.
    /// </remarks>
    public sealed class BoardCameraGestureSystem : IDisposable
    {
        // Below this the board is close enough to fully framed that panning it would only push
        // the board off-centre for no gain.
        private const float MinZoomForPan = 0.05f;
        private const float ZoomSettleEpsilon = 0.0005f;

        // Desktop Windows reports one wheel notch as 120; other backends report about 1.
        private const float ScrollNotchThreshold = 1.5f;
        private const float ScrollUnitsPerNotch = 120f;

        private readonly GameplayInputSystem inputSystem;
        private readonly IBoardCameraView cameraView;
        private readonly BoardCameraGestureRules rules;
        private readonly TowerInteractionSystem towerInteractionSystem;
        private readonly TutorialFocusSystem tutorialFocusSystem;
        private readonly IWaveSystem waveSystem;

        private float requestedZoom01;
        private float appliedZoom01;
        private Vector2 panOffsetMeters;
        private bool isPinching;
        private float previousPinchDistance;
        private bool isPanCandidate;
        private bool isPanning;
        private int panPointerId;
        private Vector2 panPressPosition;
        private Vector2 panPreviousPosition;
        private bool isLockedByOutcome;
        private float tutorialZoomStart;
        private bool isObservingTutorialZoom;

        public bool HasTutorialZoomed => isObservingTutorialZoom
            && Mathf.Abs(appliedZoom01 - tutorialZoomStart) >= 0.08f;

        public BoardCameraGestureSystem(
            GameplayInputSystem inputSystem,
            IBoardCameraView cameraView,
            BoardCameraGestureRules rules,
            TowerInteractionSystem towerInteractionSystem,
            TutorialFocusSystem tutorialFocusSystem,
            IWaveSystem waveSystem)
        {
            this.inputSystem = inputSystem ?? throw new ArgumentNullException(nameof(inputSystem));
            this.cameraView = cameraView ?? throw new ArgumentNullException(nameof(cameraView));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.towerInteractionSystem = towerInteractionSystem
                ?? throw new ArgumentNullException(nameof(towerInteractionSystem));
            this.tutorialFocusSystem = tutorialFocusSystem
                ?? throw new ArgumentNullException(nameof(tutorialFocusSystem));
            this.waveSystem = waveSystem ?? throw new ArgumentNullException(nameof(waveSystem));
        }

        public void Start()
        {
            waveSystem.StateChanged += OnWaveStateChanged;
            ApplyOffset();
        }

        public void Tick(float deltaTimeSeconds)
        {
            if (isLockedByOutcome || tutorialFocusSystem.IsHoldingBoard)
            {
                CancelGesture();
            }
            else
            {
                GameplayInputSnapshot pointer = inputSystem.Current;
                if (pointer.WasInterrupted)
                {
                    CancelGesture();
                }

                GameplayCameraGestureSnapshot gesture = inputSystem.CameraGesture;
                UpdatePinch(gesture);
                UpdateScroll(gesture);
                UpdatePan(pointer);
            }

            AdvanceZoom(deltaTimeSeconds);
            ApplyOffset();
        }

        public void Dispose()
        {
            waveSystem.StateChanged -= OnWaveStateChanged;
        }

        public void BeginTutorialZoomObservation()
        {
            tutorialZoomStart = appliedZoom01;
            isObservingTutorialZoom = true;
        }

        private void UpdatePinch(GameplayCameraGestureSnapshot gesture)
        {
            if (!gesture.IsPinchActive)
            {
                if (isPinching)
                {
                    isPinching = false;
                    inputSystem.ClearMode(GameplayInputMode.CameraGesture);
                }

                return;
            }

            // A pinch that begins while a tower is being placed or a link dragged belongs to that
            // gesture, not to the camera: claiming the mode here would strand its owner.
            if (!isPinching && inputSystem.Mode != GameplayInputMode.None)
            {
                return;
            }

            float distance = Vector2.Distance(gesture.PinchPositionA, gesture.PinchPositionB);
            if (!isPinching)
            {
                // A second finger is never an ordinary tap, so the gesture is claimed outright
                // rather than waiting for a drag threshold the way panning does.
                isPinching = true;
                CancelPan();
                inputSystem.SetMode(GameplayInputMode.CameraGesture);
                previousPinchDistance = distance;
                return;
            }

            float screenHeight = Mathf.Max(1f, Screen.height);
            RequestZoom(
                requestedZoom01
                + (distance - previousPinchDistance)
                    / screenHeight
                    * rules.PinchZoomPerScreenHeight);
            previousPinchDistance = distance;
        }

        private void UpdateScroll(GameplayCameraGestureSnapshot gesture)
        {
            float scroll = gesture.ScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f))
            {
                return;
            }

            float notches = Mathf.Abs(scroll) > ScrollNotchThreshold
                ? scroll / ScrollUnitsPerNotch
                : scroll;
            RequestZoom(requestedZoom01 + notches * rules.ScrollZoomPerNotch);
        }

        private void UpdatePan(GameplayInputSnapshot pointer)
        {
            if (isPinching)
            {
                return;
            }

            if (pointer.WasPressed
                && !isPanCandidate
                && !isPanning
                && !pointer.IsPointerOverUi
                && inputSystem.Mode == GameplayInputMode.None
                && appliedZoom01 > MinZoomForPan
                && !towerInteractionSystem.IsOverTower(pointer.ScreenPosition))
            {
                isPanCandidate = true;
                panPointerId = pointer.PointerId;
                panPressPosition = pointer.ScreenPosition;
                panPreviousPosition = pointer.ScreenPosition;
            }

            if (!isPanCandidate && !isPanning)
            {
                return;
            }

            if (pointer.PointerId != panPointerId || pointer.WasReleased || !pointer.IsPressed)
            {
                CancelPan();
                return;
            }

            if (isPanCandidate
                && (pointer.ScreenPosition - panPressPosition).sqrMagnitude
                    >= rules.PanDragThresholdPixels * rules.PanDragThresholdPixels)
            {
                // Claimed only once the press has clearly become a drag, so a plain tap on bare
                // ground still reaches tower interaction and dismisses the selection.
                isPanCandidate = false;
                isPanning = true;
                inputSystem.SetMode(GameplayInputMode.CameraGesture);
            }

            if (!isPanning)
            {
                return;
            }

            Vector2 dragDelta = pointer.ScreenPosition - panPreviousPosition;
            panPreviousPosition = pointer.ScreenPosition;

            // Opposite the drag, so the board travels with the finger rather than away from it.
            float metersPerPixel = rules.PanMetersPerScreenHeight / Mathf.Max(1f, Screen.height);
            panOffsetMeters -= dragDelta * metersPerPixel;
        }

        private void AdvanceZoom(float deltaTimeSeconds)
        {
            float smoothing = 1f - Mathf.Exp(
                -rules.ZoomSmoothingPerSecond * Mathf.Max(0f, deltaTimeSeconds));
            appliedZoom01 = Mathf.Lerp(appliedZoom01, requestedZoom01, smoothing);
            if (Mathf.Abs(requestedZoom01 - appliedZoom01) <= ZoomSettleEpsilon)
            {
                appliedZoom01 = requestedZoom01;
            }

            // The pan allowance grows with the zoom, so zooming out drags the board back to
            // centre and full framing is always centred.
            float maxPanMeters = rules.MaxPanDistanceMeters * appliedZoom01;
            if (panOffsetMeters.sqrMagnitude > maxPanMeters * maxPanMeters)
            {
                panOffsetMeters = panOffsetMeters.normalized * maxPanMeters;
            }
        }

        private void RequestZoom(float zoom01)
        {
            requestedZoom01 = Mathf.Clamp01(zoom01);
        }

        private void ApplyOffset()
        {
            cameraView.SetViewerPositionOffset(new Vector3(
                panOffsetMeters.x,
                panOffsetMeters.y,
                appliedZoom01 * rules.MaxZoomInDistanceMeters));
        }

        private void CancelPan()
        {
            if (!isPanCandidate && !isPanning)
            {
                return;
            }

            isPanCandidate = false;
            isPanning = false;
            inputSystem.ClearMode(GameplayInputMode.CameraGesture);
        }

        private void CancelGesture()
        {
            CancelPan();
            if (!isPinching)
            {
                return;
            }

            isPinching = false;
            inputSystem.ClearMode(GameplayInputMode.CameraGesture);
        }

        /// <summary>
        /// Snaps back to full board framing once the level resolves, and keeps it there.
        /// </summary>
        /// <remarks>
        /// The victory frog decides it has escaped by frustum-testing itself against this camera.
        /// A zoomed-in frustum is smaller, so it would clear the edge of the view while still over
        /// the board and the escape would finish early.
        /// </remarks>
        private void OnWaveStateChanged()
        {
            WavePhase phase = waveSystem.CreateState().Phase;
            if (phase != WavePhase.Victory && phase != WavePhase.Defeat)
            {
                // Retrying a wave leaves the outcome behind - the wave 3 defeat on levels 1 and 2
                // returns to preparation on its own - so the lock has to lift with it, or zoom
                // would stay dead for the rest of the run.
                isLockedByOutcome = false;
                return;
            }

            isLockedByOutcome = true;
            CancelGesture();
            requestedZoom01 = 0f;
            appliedZoom01 = 0f;
            panOffsetMeters = Vector2.zero;
            ApplyOffset();
        }
    }
}
