using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace TowerDefense3D.Towers
{
    [DisallowMultipleComponent]
    public sealed class TowerProjectilePoolView : MonoBehaviour, ITowerProjectileViewPool
    {
        [SerializeField, Min(1)] private int defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 128;

        private readonly Dictionary<long, TowerProjectileView> activeViews =
            new Dictionary<long, TowerProjectileView>();
        private ObjectPool<TowerProjectileView> pool;
        private Transform presentationRoot;
        private Material projectileMaterial;

        public int ActiveViewCount => activeViews.Count;
        public int InactiveViewCount => pool?.CountInactive ?? 0;

        public void Initialize()
        {
            if (pool != null)
            {
                return;
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
                actionOnGet: null,
                OnReleaseView,
                OnDestroyView,
                true,
                defaultPoolCapacity,
                Math.Max(defaultPoolCapacity, maximumPoolSize));
        }

        public void Show(long projectileId, ProjectilePayloadKind payloadKind, Vector3 position)
        {
            if (!activeViews.TryGetValue(projectileId, out TowerProjectileView view))
            {
                view = pool.Get();
                activeViews.Add(projectileId, view);
            }

            view.Show(projectileId, payloadKind, position);
        }

        public void Release(long projectileId)
        {
            if (activeViews.TryGetValue(projectileId, out TowerProjectileView view))
            {
                activeViews.Remove(projectileId);
                if (view != null)
                {
                    pool.Release(view);
                }
            }
        }

        public void Clear()
        {
            if (pool == null)
            {
                return;
            }

            foreach (TowerProjectileView view in activeViews.Values)
            {
                if (view != null)
                {
                    pool.Release(view);
                }
            }

            activeViews.Clear();
        }

        private TowerProjectileView CreateView()
        {
            GameObject instance = new GameObject("Tower Projectile");
            instance.transform.SetParent(presentationRoot, false);
            TowerProjectileView view = instance.AddComponent<TowerProjectileView>();
            view.Initialize(projectileMaterial);
            return view;
        }

        private static void OnReleaseView(TowerProjectileView view)
        {
            if (view != null)
            {
                view.ResetForPool();
            }
        }

        private static void OnDestroyView(TowerProjectileView view)
        {
            if (view != null)
            {
                DestroyRuntimeObject(view.gameObject);
            }
        }

        private void OnDestroy()
        {
            Clear();
            pool?.Clear();
            pool = null;
            DestroyRuntimeObject(projectileMaterial);
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
