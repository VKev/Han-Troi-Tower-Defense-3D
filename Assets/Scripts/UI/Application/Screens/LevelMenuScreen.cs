using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelMenuScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private LevelButtonView[] levelButtons = Array.Empty<LevelButtonView>();

        public void Show(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            int levelCount = levels.Count;
            EnsureAuthoredCapacity(levelCount);
            UnbindButtons();

            for (int index = 0; index < levelButtons.Length; index++)
            {
                LevelButtonView view = levelButtons[index];
                bool hasLevel = index < levelCount;
                view.gameObject.SetActive(hasLevel);
                if (hasLevel)
                {
                    view.Bind(levels[index], onLevelSelected);
                }
            }

            SetVisible(true);
        }

        public void Hide()
        {
            UnbindButtons();
            SetVisible(false);
        }

        private void EnsureAuthoredCapacity(int requiredCount)
        {
            if (requiredCount > levelButtons.Length)
            {
                throw new InvalidOperationException(
                    $"LevelMenuScreen has {levelButtons.Length} authored level buttons but requires {requiredCount}.");
            }
        }

        private void UnbindButtons()
        {
            for (int index = 0; index < levelButtons.Length; index++)
            {
                levelButtons[index].Unbind();
            }
        }

        private void SetVisible(bool visible)
        {
            root.SetActive(visible);
        }
    }
}
