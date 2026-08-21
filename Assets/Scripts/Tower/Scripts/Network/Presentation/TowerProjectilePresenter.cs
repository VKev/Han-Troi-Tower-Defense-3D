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

        private readonly Dictionary<long, TowerProjectileView> activeViews =
            new Dictionary<long, TowerProjectileView>();
        private readonly HashSet<long> visibleProjectileIds = new HashSet<long>();

        private TowerNetworkManager manager;
        private ObjectPool<TowerProjectileView> pool;
        private Transform presentationRoot;
        private Material projectileMaterial;

        public bool IsInitialized => manager != null;
        public int ActiveViewCount => activeViews.Count;
        public int InactiveViewCount => pool?.CountInactive ?? 0;

        public void Initialize(TowerNetworkManager towerNetworkManager)
        {
            if (towerNetworkManager == null)
            {
                throw new ArgumentNullException(nameof(towerNetworkManager));
            }

            if (manager != null)
            {
                throw new InvalidOperationException("TowerProjectilePresenter is already initialized.");
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("Tower projectile presentation requires the Sprites/Default shader.");
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
            manager = towerNetworkManager;
        }

        public void Shutdown()
        {
            if (manager == null)
            {
                return;
            }

            foreach (TowerProjectileView view in activeViews.Values)
            {
                pool.Release(view);
            }

            activeViews.Clear();
            visibleProjectileIds.Clear();
            pool.Clear();
            pool = null;
            manager = null;

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
            if (manager == null)
            {
                return;
            }

            IReadOnlyList<TowerProjectileSnapshot> snapshots = manager.CreateProjectileSnapshot();
            visibleProjectileIds.Clear();

            for (int index = 0; index < snapshots.Count; index++)
            {
                TowerProjectileSnapshot snapshot = snapshots[index];
                visibleProjectileIds.Add(snapshot.ProjectileId);

                if (!activeViews.TryGetValue(snapshot.ProjectileId, out TowerProjectileView view))
                {
                    view = pool.Get();
                    activeViews.Add(snapshot.ProjectileId, view);
                }

                view.Show(snapshot);
            }

            if (activeViews.Count == visibleProjectileIds.Count)
            {
                return;
            }

            List<long> releasedIds = new List<long>();
            foreach (KeyValuePair<long, TowerProjectileView> pair in activeViews)
            {
                if (!visibleProjectileIds.Contains(pair.Key))
                {
                    pool.Release(pair.Value);
                    releasedIds.Add(pair.Key);
                }
            }

            for (int index = 0; index < releasedIds.Count; index++)
            {
                activeViews.Remove(releasedIds[index]);
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
            view.gameObject.SetActive(true);
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
