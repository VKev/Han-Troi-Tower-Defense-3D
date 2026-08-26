using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyViewPool : MonoBehaviour, IEnemyViewPool
    {
        [SerializeField, Min(1)] private int defaultPoolCapacity = 16;
        [SerializeField, Min(1)] private int maximumPoolSize = 128;

        private readonly Dictionary<long, ActiveEnemyView> activeViews =
            new Dictionary<long, ActiveEnemyView>();
        private readonly Dictionary<EnemyDefinition, ComponentPool<EnemyView>> poolsByDefinition =
            new Dictionary<EnemyDefinition, ComponentPool<EnemyView>>();

        public void Spawn(EnemySnapshot enemy)
        {
            ComponentPool<EnemyView> pool = GetPool(enemy.Definition);
            EnemyView view = pool.Get();
            activeViews.Add(enemy.EnemyId, new ActiveEnemyView(pool, view));
            view.Bind(enemy);
        }

        public void Despawn(long enemyId)
        {
            ActiveEnemyView activeView = activeViews[enemyId];
            activeViews.Remove(enemyId);
            activeView.Pool.Release(activeView.View);
        }

        public void Render(
            IReadOnlyList<EnemySnapshot> enemies,
            float interpolationAlpha)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                EnemySnapshot enemy = enemies[index];
                activeViews[enemy.EnemyId].View.Render(enemy, interpolationAlpha);
            }
        }

        public void ReleaseAll()
        {
            foreach (ActiveEnemyView activeView in activeViews.Values)
            {
                activeView.Pool.Release(activeView.View);
            }

            activeViews.Clear();
        }

        private ComponentPool<EnemyView> GetPool(EnemyDefinition definition)
        {
            if (!poolsByDefinition.TryGetValue(definition, out ComponentPool<EnemyView> pool))
            {
                pool = new ComponentPool<EnemyView>(
                    () => CreateView(definition),
                    view => view.Release(),
                    defaultPoolCapacity,
                    maximumPoolSize);
                poolsByDefinition.Add(definition, pool);
            }

            return pool;
        }

        private EnemyView CreateView(EnemyDefinition definition)
        {
            if (definition.ViewPrefab == null)
            {
                throw new MissingReferenceException(
                    $"Enemy '{definition.DisplayName}' requires a View Prefab.");
            }

            GameObject instance = Instantiate(definition.ViewPrefab, transform);
            EnemyView view = instance.GetComponent<EnemyView>();
            if (view == null)
            {
                Destroy(instance);
                throw new MissingComponentException(
                    $"Enemy View Prefab '{definition.ViewPrefab.name}' must have an EnemyView on its root.");
            }

            view.Release();
            return view;
        }

        private void OnDestroy()
        {
            ReleaseAll();
            foreach (ComponentPool<EnemyView> pool in poolsByDefinition.Values)
            {
                pool.Clear();
            }

            poolsByDefinition.Clear();
        }

        private readonly struct ActiveEnemyView
        {
            public ActiveEnemyView(ComponentPool<EnemyView> pool, EnemyView view)
            {
                Pool = pool;
                View = view;
            }

            public ComponentPool<EnemyView> Pool { get; }
            public EnemyView View { get; }
        }
    }
}
