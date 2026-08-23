using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Interpolates deterministic projectile snapshots and projects them into a pooled Unity view.
    /// </summary>
    public sealed class TowerProjectilePresentationSystem : IDisposable
    {
        private readonly Dictionary<long, TowerProjectilePresentationTrack> presentationTracks =
            new Dictionary<long, TowerProjectilePresentationTrack>();
        private readonly HashSet<long> activeProjectileIds = new HashSet<long>();
        private readonly HashSet<long> capturedProjectileIds = new HashSet<long>();
        private readonly List<TowerProjectileSnapshot> snapshotBuffer = new List<TowerProjectileSnapshot>();
        private readonly List<long> pendingProjectileIds = new List<long>();
        private readonly TowerNetworkManager manager;
        private readonly TowerSimulationSystem simulationSystem;
        private readonly ITowerProjectileViewPool viewPool;

        private bool isStarted;

        public TowerProjectilePresentationSystem(
            TowerNetworkManager manager,
            TowerSimulationSystem simulationSystem,
            ITowerProjectileViewPool viewPool)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.simulationSystem = simulationSystem ?? throw new ArgumentNullException(nameof(simulationSystem));
            this.viewPool = viewPool ?? throw new ArgumentNullException(nameof(viewPool));
        }

        public int ActiveViewCount => viewPool.ActiveViewCount;
        public int InactiveViewCount => viewPool.InactiveViewCount;

        public void Start()
        {
            viewPool.Initialize();
            manager.StateChanged += HandleManagerStateChanged;
            simulationSystem.TickCompleted += HandleTickCompleted;
            isStarted = true;
        }

        public void LateTick()
        {
            if (!manager.IsRunning)
            {
                ClearPresentation();
                return;
            }

            RenderPresentation(simulationSystem.InterpolationAlpha);
        }

        public void Dispose()
        {
            if (isStarted)
            {
                manager.StateChanged -= HandleManagerStateChanged;
                simulationSystem.TickCompleted -= HandleTickCompleted;
                isStarted = false;
            }

            ClearPresentation();
        }

        private void CaptureSimulationState()
        {
            manager.CopyProjectileSnapshotTo(snapshotBuffer);
            capturedProjectileIds.Clear();

            for (int index = 0; index < snapshotBuffer.Count; index++)
            {
                TowerProjectileSnapshot snapshot = snapshotBuffer[index];
                capturedProjectileIds.Add(snapshot.ProjectileId);

                if (presentationTracks.TryGetValue(
                    snapshot.ProjectileId,
                    out TowerProjectilePresentationTrack track))
                {
                    track.Advance(snapshot);
                    presentationTracks[snapshot.ProjectileId] = track;
                }
                else
                {
                    presentationTracks.Add(snapshot.ProjectileId, TowerProjectilePresentationTrack.Create(snapshot));
                }
            }

            pendingProjectileIds.Clear();
            foreach (KeyValuePair<long, TowerProjectilePresentationTrack> pair in presentationTracks)
            {
                if (!capturedProjectileIds.Contains(pair.Key))
                {
                    pendingProjectileIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < pendingProjectileIds.Count; index++)
            {
                long projectileId = pendingProjectileIds[index];
                TowerProjectilePresentationTrack track = presentationTracks[projectileId];

                if (track.IsRetiring)
                {
                    track.PrepareReleaseAfterRender();
                }
                else if (manager.TryGetNodePosition(track.Target, out TowerWorldPosition targetPosition))
                {
                    track.BeginRetirement(targetPosition);
                }
                else
                {
                    track.PrepareReleaseAfterRender();
                }

                presentationTracks[projectileId] = track;
            }
        }

        private void RenderPresentation(float interpolationAlpha)
        {
            pendingProjectileIds.Clear();

            foreach (KeyValuePair<long, TowerProjectilePresentationTrack> pair in presentationTracks)
            {
                TowerProjectilePresentationTrack track = pair.Value;
                if (!track.IsVisible)
                {
                    continue;
                }

                bool hasView = activeProjectileIds.Contains(pair.Key);
                if (track.ReleaseAfterRender && !hasView)
                {
                    pendingProjectileIds.Add(pair.Key);
                    continue;
                }

                viewPool.Show(
                    track.ProjectileId,
                    track.Payload.Kind,
                    track.CalculateRenderedPosition(interpolationAlpha));
                activeProjectileIds.Add(pair.Key);

                if (track.ReleaseAfterRender)
                {
                    pendingProjectileIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < pendingProjectileIds.Count; index++)
            {
                ReleaseProjectile(pendingProjectileIds[index]);
            }
        }

        private void ClearPresentation()
        {
            viewPool.Clear();
            activeProjectileIds.Clear();
            presentationTracks.Clear();
            capturedProjectileIds.Clear();
            snapshotBuffer.Clear();
            pendingProjectileIds.Clear();
        }

        private void ReleaseProjectile(long projectileId)
        {
            if (activeProjectileIds.Remove(projectileId))
            {
                viewPool.Release(projectileId);
            }

            presentationTracks.Remove(projectileId);
        }

        private void HandleTickCompleted(long completedTick)
        {
            _ = completedTick;
            CaptureSimulationState();
        }

        private void HandleManagerStateChanged()
        {
            if (!manager.IsRunning)
            {
                ClearPresentation();
            }
        }
    }
}
