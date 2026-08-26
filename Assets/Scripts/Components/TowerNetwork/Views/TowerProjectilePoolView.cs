using System;
using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectilePoolView : MonoBehaviour, ITowerProjectileViewPool
    {
        [SerializeField, Min(1)] private int defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 128;

        private readonly Dictionary<long, ActiveProjectileView> activeViews =
            new Dictionary<long, ActiveProjectileView>();
        private readonly Dictionary<GameObject, ComponentPool<TowerProjectileView>> poolsByPrefab =
            new Dictionary<GameObject, ComponentPool<TowerProjectileView>>();
        private readonly Dictionary<GameObject, ComponentPool<TowerProjectileHitView>>
            hitEffectPoolsByPrefab = new Dictionary<GameObject, ComponentPool<TowerProjectileHitView>>();
        private readonly List<RetiringProjectileView> retiringViews = new List<RetiringProjectileView>();
        private readonly List<ActiveHitEffectView> activeHitEffects = new List<ActiveHitEffectView>();
        private Transform presentationRoot;

        public int ActiveViewCount => activeViews.Count;
        public int ActiveHitEffectCount => activeHitEffects.Count;
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

        public int InactiveHitEffectCount
        {
            get
            {
                int count = 0;
                foreach (ComponentPool<TowerProjectileHitView> pool in hitEffectPoolsByPrefab.Values)
                {
                    count += pool.CountInactive;
                }

                return count;
            }
        }

        public void Initialize()
        {
            if (presentationRoot != null)
            {
                return;
            }

            presentationRoot = new GameObject("Tower Projectile Visuals").transform;
            presentationRoot.SetParent(transform, false);
        }

        public void Show(long projectileId, GameObject projectilePrefab, Vector3 position)
        {
            if (projectilePrefab == null)
            {
                throw new ArgumentNullException(nameof(projectilePrefab));
            }

            if (presentationRoot == null)
            {
                Initialize();
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

            if (presentationRoot == null)
            {
                Initialize();
            }

            ComponentPool<TowerProjectileHitView> pool = GetHitEffectPool(hitEffectPrefab);
            TowerProjectileHitView view = pool.Get();
            activeHitEffects.Add(new ActiveHitEffectView(
                pool,
                view,
                view.Play(position)));
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

            for (int index = activeHitEffects.Count - 1; index >= 0; index--)
            {
                ActiveHitEffectView hitEffect = activeHitEffects[index];
                hitEffect.RemainingSeconds -= deltaTime;
                if (hitEffect.RemainingSeconds > 0f)
                {
                    continue;
                }

                hitEffect.Pool.Release(hitEffect.View);
                activeHitEffects.RemoveAt(index);
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
            for (int index = 0; index < activeHitEffects.Count; index++)
            {
                ActiveHitEffectView hitEffect = activeHitEffects[index];
                hitEffect.Pool.Release(hitEffect.View);
            }

            activeHitEffects.Clear();
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
                view = instance.AddComponent<TowerProjectileView>();
            }

            view.Initialize();
            return view;
        }

        private ComponentPool<TowerProjectileHitView> GetHitEffectPool(GameObject hitEffectPrefab)
        {
            if (!hitEffectPoolsByPrefab.TryGetValue(
                hitEffectPrefab,
                out ComponentPool<TowerProjectileHitView> pool))
            {
                pool = new ComponentPool<TowerProjectileHitView>(
                    () => CreateHitEffectView(hitEffectPrefab),
                    view => view.ResetForPool(),
                    defaultPoolCapacity,
                    maximumPoolSize);
                hitEffectPoolsByPrefab.Add(hitEffectPrefab, pool);
            }

            return pool;
        }

        private TowerProjectileHitView CreateHitEffectView(GameObject hitEffectPrefab)
        {
            GameObject instance = Instantiate(hitEffectPrefab, presentationRoot);
            instance.name = hitEffectPrefab.name;
            TowerProjectileHitView view = instance.GetComponent<TowerProjectileHitView>();
            if (view == null)
            {
                view = instance.AddComponent<TowerProjectileHitView>();
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
            foreach (ComponentPool<TowerProjectileHitView> pool in hitEffectPoolsByPrefab.Values)
            {
                pool.Clear();
            }

            hitEffectPoolsByPrefab.Clear();
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

        private sealed class ActiveHitEffectView
        {
            public ActiveHitEffectView(
                ComponentPool<TowerProjectileHitView> pool,
                TowerProjectileHitView view,
                float remainingSeconds)
            {
                Pool = pool;
                View = view;
                RemainingSeconds = remainingSeconds;
            }

            public ComponentPool<TowerProjectileHitView> Pool { get; }
            public TowerProjectileHitView View { get; }
            public float RemainingSeconds { get; set; }
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
