using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyViewPool : MonoBehaviour, IEnemyViewPool
    {
        private readonly Dictionary<long, ActiveEnemyView> activeViews =
            new Dictionary<long, ActiveEnemyView>();
        private readonly Dictionary<EnemyDefinition, Stack<EnemyView>> availableViews =
            new Dictionary<EnemyDefinition, Stack<EnemyView>>();

        public void Spawn(EnemySnapshot enemy)
        {
            Stack<EnemyView> definitionViews = GetAvailableViews(enemy.Definition);
            EnemyView view = definitionViews.Count > 0
                ? definitionViews.Pop()
                : CreateView(enemy.Definition);
            activeViews.Add(
                enemy.EnemyId,
                new ActiveEnemyView(enemy.Definition, view));
            view.Bind(enemy);
        }

        public void Despawn(long enemyId)
        {
            ActiveEnemyView activeView = activeViews[enemyId];
            activeViews.Remove(enemyId);
            activeView.View.Release();
            GetAvailableViews(activeView.Definition).Push(activeView.View);
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
                activeView.View.Release();
                GetAvailableViews(activeView.Definition).Push(activeView.View);
            }

            activeViews.Clear();
        }

        private Stack<EnemyView> GetAvailableViews(EnemyDefinition definition)
        {
            if (!availableViews.TryGetValue(definition, out Stack<EnemyView> views))
            {
                views = new Stack<EnemyView>();
                availableViews.Add(definition, views);
            }

            return views;
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

        private readonly struct ActiveEnemyView
        {
            public ActiveEnemyView(EnemyDefinition definition, EnemyView view)
            {
                Definition = definition;
                View = view;
            }

            public EnemyDefinition Definition { get; }
            public EnemyView View { get; }
        }
    }
}
