using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelMenuScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Transform levelList;
        [SerializeField] private LevelButtonView levelButtonPrefab;

        private readonly List<LevelButtonView> spawnedButtons = new List<LevelButtonView>();

        public void Show(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            ClearButtons();

            if (levels != null && levelList != null && levelButtonPrefab != null)
            {
                for (int index = 0; index < levels.Count; index++)
                {
                    LevelButtonView view = Instantiate(levelButtonPrefab, levelList);
                    view.Bind(levels[index], onLevelSelected);
                    spawnedButtons.Add(view);
                }
            }

            SetVisible(true);
        }

        public void Hide()
        {
            ClearButtons();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            ClearButtons();
        }

        private void ClearButtons()
        {
            for (int index = 0; index < spawnedButtons.Count; index++)
            {
                LevelButtonView view = spawnedButtons[index];
                if (view != null)
                {
                    view.Unbind();
                    Destroy(view.gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private void SetVisible(bool visible)
        {
            GameObject target = root != null ? root : gameObject;
            if (target.activeSelf != visible)
            {
                target.SetActive(visible);
            }
        }
    }
}
