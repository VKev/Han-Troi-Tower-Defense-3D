using System;
using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using TowerDefense3D.Vfx;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectilePoolView : MonoBehaviour, ITowerProjectileViewPool
    {
        [SerializeField, Min(1)] private int defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 128;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private GlobalEffectEmitterView hitEffectEmitter;

        private readonly Dictionary<long, ActiveProjectileView> activeViews =
            new Dictionary<long, ActiveProjectileView>();
        private readonly Dictionary<GameObject, ComponentPool<TowerProjectileView>> poolsByPrefab =
            new Dictionary<GameObject, ComponentPool<TowerProjectileView>>();
        private readonly List<RetiringProjectileView> retiringViews = new List<RetiringProjectileView>();

        public int ActiveViewCount => activeViews.Count;
        public int InactiveViewCount
        {
            get
            {
                int count = 0;
                foreach (ComponentPool<TowerProjectileView> pool in poolsByPrefab.Values)
                {
                    count += pool.CountInactive;
                }

                return count;
            }
        }


        public void Initialize()
        {
            if (presentationRoot == null || hitEffectEmitter == null)
            {
                throw new MissingReferenceException(
                    "TowerProjectilePoolView requires authored presentation and hit-effect roots.");
            }
        }

        public void Show(long projectileId, GameObject projectilePrefab, Vector3 position)
        {
            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }

            if (!activeViews.TryGetValue(projectileId, out ActiveProjectileView activeView))
            {
                ComponentPool<TowerProjectileView> pool = GetPool(projectilePrefab);
                activeView = new ActiveProjectileView(projectilePrefab, pool, pool.Get());
                activeViews.Add(projectileId, activeView);
                activeView.View.Show(projectileId, position);
                return;
            }

            if (activeView.ProjectilePrefab != projectilePrefab)
            {
                throw new InvalidOperationException(
                    $"Projectile '{projectileId}' cannot change its visual prefab.");
            }

            activeView.View.SetPosition(position);
        }

        public void PlayHitEffect(GameObject hitEffectPrefab, Vector3 position)
        {
            if (hitEffectPrefab == null)
            {
                throw new ArgumentNullException(nameof(hitEffectPrefab));
            }

            hitEffectEmitter.Play(hitEffectPrefab, position);
        }

        public void Release(long projectileId)
        {
            if (activeViews.TryGetValue(projectileId, out ActiveProjectileView activeView))
            {
                activeViews.Remove(projectileId);
                float releaseDelaySeconds = activeView.View.BeginRetirement();
                if (releaseDelaySeconds > 0f)
                {
                    retiringViews.Add(new RetiringProjectileView(
                        activeView.Pool,
                        activeView.View,
                        releaseDelaySeconds));
                }
                else
                {
                    activeView.Pool.Release(activeView.View);
                }
            }
        }

        public void AdvanceReleaseDelays(float deltaTime)
        {
            for (int index = retiringViews.Count - 1; index >= 0; index--)
            {
                RetiringProjectileView retiringView = retiringViews[index];
                retiringView.RemainingSeconds -= deltaTime;
                if (retiringView.RemainingSeconds > 0f)
                {
                    continue;
                }

                retiringView.Pool.Release(retiringView.View);
                retiringViews.RemoveAt(index);
            }

        }

        public void Clear()
        {
            foreach (ActiveProjectileView activeView in activeViews.Values)
            {
                activeView.Pool.Release(activeView.View);
            }

            activeViews.Clear();
            for (int index = 0; index < retiringViews.Count; index++)
            {
                RetiringProjectileView retiringView = retiringViews[index];
                retiringView.Pool.Release(retiringView.View);
            }

            retiringViews.Clear();
            if (hitEffectEmitter != null)
            {
                hitEffectEmitter.Clear();
            }
        }

        private ComponentPool<TowerProjectileView> GetPool(GameObject projectilePrefab)
        {
            if (!poolsByPrefab.TryGetValue(
                projectilePrefab,
                out ComponentPool<TowerProjectileView> pool))
            {
                pool = new ComponentPool<TowerProjectileView>(
                    () => CreateView(projectilePrefab),
                    view => view.ResetForPool(),
                    defaultPoolCapacity,
                    maximumPoolSize);
                poolsByPrefab.Add(projectilePrefab, pool);
            }

            return pool;
        }

        private TowerProjectileView CreateView(GameObject projectilePrefab)
        {
            GameObject instance = Instantiate(projectilePrefab, presentationRoot);
            instance.name = projectilePrefab.name;
            TowerProjectileView view = instance.GetComponent<TowerProjectileView>();
            if (view == null)
            {
                throw new MissingReferenceException(
                    $"Projectile prefab '{projectilePrefab.name}' requires an authored TowerProjectileView.");
            }

            view.Initialize();
            return view;
        }


        private void OnDestroy()
        {
            Clear();
            foreach (ComponentPool<TowerProjectileView> pool in poolsByPrefab.Values)
            {
                pool.Clear();
            }

            poolsByPrefab.Clear();
        }

        private readonly struct ActiveProjectileView
        {
            public ActiveProjectileView(
                GameObject projectilePrefab,
                ComponentPool<TowerProjectileView> pool,
                TowerProjectileView view)
            {
                ProjectilePrefab = projectilePrefab;
                Pool = pool;
                View = view;
            }

            public GameObject ProjectilePrefab { get; }
            public ComponentPool<TowerProjectileView> Pool { get; }
            public TowerProjectileView View { get; }
        }


        private sealed class RetiringProjectileView
        {
            public RetiringProjectileView(
                ComponentPool<TowerProjectileView> pool,
                TowerProjectileView view,
                float remainingSeconds)
            {
                Pool = pool;
                View = view;
                RemainingSeconds = remainingSeconds;
            }

            public ComponentPool<TowerProjectileView> Pool { get; }
            public TowerProjectileView View { get; }
            public float RemainingSeconds { get; set; }
        }
    }
}
