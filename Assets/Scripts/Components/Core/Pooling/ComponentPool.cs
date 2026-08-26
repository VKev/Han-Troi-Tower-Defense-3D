using System;
using UnityEngine;
using UnityEngine.Pool;

namespace TowerDefense3D.Components.Core
{
    internal sealed class ComponentPool<TComponent> where TComponent : Component
    {
        private readonly ObjectPool<TComponent> pool;

        public ComponentPool(
            Func<TComponent> createComponent,
            Action<TComponent> resetComponent,
            int defaultCapacity,
            int maximumInactiveCount)
        {
            pool = new ObjectPool<TComponent>(
                createComponent,
                actionOnGet: null,
                resetComponent,
                DestroyComponent,
                collectionCheck: true,
                defaultCapacity,
                Math.Max(defaultCapacity, maximumInactiveCount));
        }

        public int CountInactive => pool.CountInactive;

        public TComponent Get()
        {
            return pool.Get();
        }

        public void Release(TComponent component)
        {
            pool.Release(component);
        }

        public void Clear()
        {
            pool.Clear();
        }

        private static void DestroyComponent(TComponent component)
        {
            RuntimeObjectDestroyer.Destroy(component.gameObject);
        }
    }
}
