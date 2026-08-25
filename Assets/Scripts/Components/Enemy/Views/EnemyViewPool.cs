using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyViewPool : MonoBehaviour, IEnemyViewPool
    {
        [SerializeField] private EnemyView prefab;
        [SerializeField, Min(0)] private int prewarmCount = 24;
        private readonly Dictionary<long, EnemyView> activeViews =
            new Dictionary<long, EnemyView>();
        private readonly Stack<EnemyView> availableViews = new Stack<EnemyView>();
        private bool isInitialized;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            if (prefab == null)
            {
                throw new MissingReferenceException("EnemyViewPool requires an EnemyView prefab.");
            }

            for (int index = 0; index < prewarmCount; index++)
            {
                availableViews.Push(CreateView());
            }

            isInitialized = true;
        }

        public void Spawn(EnemySnapshot enemy)
        {
            EnemyView view = availableViews.Count > 0
                ? availableViews.Pop()
                : CreateView();
            activeViews.Add(enemy.EnemyId, view);
            view.Bind(enemy);
        }

        public void Despawn(long enemyId)
        {
            EnemyView view = activeViews[enemyId];
            activeViews.Remove(enemyId);
            view.Release();
            availableViews.Push(view);
        }

        public void Render(
            IReadOnlyList<EnemySnapshot> enemies,
            float interpolationAlpha)
        {
            for (int index = 0; index < enemies.Count; index++)
            {
                EnemySnapshot enemy = enemies[index];
                activeViews[enemy.EnemyId].Render(enemy, interpolationAlpha);
            }
        }

        public void ReleaseAll()
        {
            foreach (EnemyView view in activeViews.Values)
            {
                view.Release();
                availableViews.Push(view);
            }

            activeViews.Clear();
        }

        private EnemyView CreateView()
        {
            EnemyView view = Instantiate(prefab, transform);
            view.Release();
            return view;
        }
    }
}
