using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelMenuView : MonoBehaviour
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
            if (requiredCount <= levelButtons.Length)
            {
                return;
            }

            LevelButtonView template = FindTemplate();
            int authoredCount = levelButtons.Length;
            Array.Resize(ref levelButtons, requiredCount);
            for (int index = authoredCount; index < requiredCount; index++)
            {
                LevelButtonView copy = Instantiate(
                    template,
                    template.transform.parent,
                    false);
                copy.name = $"{template.name} {index + 1}";
                levelButtons[index] = copy;
            }
        }

        private LevelButtonView FindTemplate()
        {
            for (int index = levelButtons.Length - 1; index >= 0; index--)
            {
                if (levelButtons[index] != null)
                {
                    return levelButtons[index];
                }
            }

            throw new InvalidOperationException(
                "LevelMenuView requires at least one authored LevelButtonView template.");
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
