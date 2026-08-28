using System.Collections.Generic;
using TowerDefense3D.Components.Core;
using TowerDefense3D.Vfx;
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
        private readonly Dictionary<long, ActiveEnemyView> pendingDeaths =
            new Dictionary<long, ActiveEnemyView>();
        private readonly Dictionary<EnemyDefinition, ComponentPool<EnemyView>> poolsByDefinition =
            new Dictionary<EnemyDefinition, ComponentPool<EnemyView>>();
        private readonly List<ActiveEnemyView> pendingDeathSnapshot = new List<ActiveEnemyView>();
        private Camera worldCamera;
        private GlobalEffectEmitterView reactionEffectEmitter;

        public void Configure(Camera camera)
        {
            worldCamera = camera;
        }

        /// <summary>
        /// One rig set shared by every enemy, so a reaction firing on ten enemies at once still
        /// costs one set of particle systems. Parked at the world origin with an identity
        /// transform, which is what keeps Local-space systems behaving as authored.
        /// </summary>
        private GlobalEffectEmitterView EnsureReactionEffectEmitter()
        {
            if (reactionEffectEmitter != null)
            {
                return reactionEffectEmitter;
            }

            var root = new GameObject("Enemy Reaction Effect Emitter");
            root.transform.SetParent(null, false);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            reactionEffectEmitter = root.AddComponent<GlobalEffectEmitterView>();
            return reactionEffectEmitter;
        }

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
            pendingDeaths[enemyId] = activeView;
            activeView.View.BeginDeath(() => CompleteDeath(enemyId, activeView));
        }

        public void ShowReaction(long enemyId, ElementReactionEvent reaction)
        {
            if (activeViews.TryGetValue(enemyId, out ActiveEnemyView activeView))
            {
                activeView.View.ShowReaction(reaction);
            }
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

            TickLifecycle(Time.deltaTime);
        }

        public void TickLifecycle(float deltaTime)
        {
            pendingDeathSnapshot.Clear();
            foreach (ActiveEnemyView pendingDeath in pendingDeaths.Values)
            {
                pendingDeathSnapshot.Add(pendingDeath);
            }

            for (int index = 0; index < pendingDeathSnapshot.Count; index++)
            {
                pendingDeathSnapshot[index].View.TickLifecycle(deltaTime);
            }
        }

        public void ReleaseAll()
        {
            foreach (ActiveEnemyView activeView in activeViews.Values)
            {
                activeView.Pool.Release(activeView.View);
            }

            activeViews.Clear();
            foreach (ActiveEnemyView pendingDeath in pendingDeaths.Values)
            {
                pendingDeath.Pool.Release(pendingDeath.View);
            }

            pendingDeaths.Clear();
            pendingDeathSnapshot.Clear();
        }

        private void CompleteDeath(long enemyId, ActiveEnemyView activeView)
        {
            if (!pendingDeaths.TryGetValue(enemyId, out ActiveEnemyView pendingDeath)
                || pendingDeath.View != activeView.View)
            {
                return;
            }

            pendingDeaths.Remove(enemyId);
            activeView.Pool.Release(activeView.View);
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

            view.Configure(worldCamera, EnsureReactionEffectEmitter());
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
