using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectilePresenter : MonoBehaviour
    {
        [SerializeField, Min(1)] private int defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 128;

        private readonly Dictionary<long, TowerProjectilePresentationTrack> presentationTracks =
            new Dictionary<long, TowerProjectilePresentationTrack>();
        private readonly Dictionary<long, TowerProjectileView> activeViews =
            new Dictionary<long, TowerProjectileView>();
        private readonly HashSet<long> capturedProjectileIds = new HashSet<long>();
        private readonly List<TowerProjectileSnapshot> snapshotBuffer = new List<TowerProjectileSnapshot>();
        private readonly List<long> pendingProjectileIds = new List<long>();

        private TowerNetworkManager manager;
        private TowerSimulationDriver simulationDriver;
        private ObjectPool<TowerProjectileView> pool;
        private Transform presentationRoot;
        private Material projectileMaterial;

        public bool IsInitialized => manager != null && simulationDriver != null;
        public int ActiveViewCount => activeViews.Count;
        public int InactiveViewCount => pool?.CountInactive ?? 0;

        public void Initialize(TowerNetworkManager towerNetworkManager, TowerSimulationDriver towerSimulationDriver)
        {
            if (towerNetworkManager == null)
            {
                throw new ArgumentNullException(nameof(towerNetworkManager));
            }

            if (towerSimulationDriver == null)
            {
                throw new ArgumentNullException(nameof(towerSimulationDriver));
            }

            if (!towerSimulationDriver.IsInitialized)
            {
                throw new InvalidOperationException(
                    "TowerSimulationDriver must be initialized before TowerProjectilePresenter.");
            }

            if (IsInitialized)
            {
                throw new InvalidOperationException("TowerProjectilePresenter is already initialized.");
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Tower projectile presentation requires the Sprites/Default shader.");
            }

            presentationRoot = new GameObject("Tower Projectile Visuals").transform;
            presentationRoot.SetParent(transform, false);
            projectileMaterial = new Material(shader)
            {
                name = "Tower Projectile Runtime Material"
            };
            pool = new ObjectPool<TowerProjectileView>(
                CreateView,
                OnGetView,
                OnReleaseView,
                OnDestroyView,
                true,
                defaultPoolCapacity,
                Math.Max(defaultPoolCapacity, maximumPoolSize));

            if (snapshotBuffer.Capacity < defaultPoolCapacity)
            {
                snapshotBuffer.Capacity = defaultPoolCapacity;
            }

            manager = towerNetworkManager;
            simulationDriver = towerSimulationDriver;
            manager.StateChanged += HandleManagerStateChanged;
            simulationDriver.TickCompleted += HandleTickCompleted;
        }

        public void Shutdown()
        {
            if (!IsInitialized)
            {
                return;
            }

            manager.StateChanged -= HandleManagerStateChanged;
            simulationDriver.TickCompleted -= HandleTickCompleted;
            ClearPresentation();
            pool.Clear();
            pool = null;
            manager = null;
            simulationDriver = null;

            if (presentationRoot != null)
            {
                DestroyRuntimeObject(presentationRoot.gameObject);
                presentationRoot = null;
            }

            if (projectileMaterial != null)
            {
                DestroyRuntimeObject(projectileMaterial);
                projectileMaterial = null;
            }
        }

        public void RefreshPresentation()
        {
            if (!IsInitialized)
            {
                return;
            }

            if (!manager.IsRunning)
            {
                ClearPresentation();
                return;
            }

            RenderPresentation(simulationDriver.InterpolationAlpha);
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

                bool hasView = activeViews.TryGetValue(pair.Key, out TowerProjectileView view);
                if (track.ReleaseAfterRender && !hasView)
                {
                    pendingProjectileIds.Add(pair.Key);
                    continue;
                }

                if (!hasView)
                {
                    view = pool.Get();
                    activeViews.Add(pair.Key, view);
                }

                view.Show(
                    track.ProjectileId,
                    track.Payload.Kind,
                    track.CalculateRenderedPosition(interpolationAlpha));

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
            foreach (TowerProjectileView view in activeViews.Values)
            {
                pool.Release(view);
            }

            activeViews.Clear();
            presentationTracks.Clear();
            capturedProjectileIds.Clear();
            snapshotBuffer.Clear();
            pendingProjectileIds.Clear();
        }

        private void ReleaseProjectile(long projectileId)
        {
            if (activeViews.TryGetValue(projectileId, out TowerProjectileView view))
            {
                pool.Release(view);
                activeViews.Remove(projectileId);
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
            if (manager != null && !manager.IsRunning)
            {
                ClearPresentation();
            }
        }

        private void LateUpdate()
        {
            RefreshPresentation();
        }

        private TowerProjectileView CreateView()
        {
            GameObject instance = new GameObject("Tower Projectile");
            instance.transform.SetParent(presentationRoot, false);
            TowerProjectileView view = instance.AddComponent<TowerProjectileView>();
            view.Initialize(projectileMaterial);
            return view;
        }

        private static void OnGetView(TowerProjectileView view)
        {
            _ = view;
        }

        private static void OnReleaseView(TowerProjectileView view)
        {
            view.ResetForPool();
        }

        private static void OnDestroyView(TowerProjectileView view)
        {
            if (view != null)
            {
                DestroyRuntimeObject(view.gameObject);
            }
        }

        private static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
