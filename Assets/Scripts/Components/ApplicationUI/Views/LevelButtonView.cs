using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private GameObject lockedIndicator;
        [SerializeField] private GameObject unlockedIndicator;

        private Action<int> onSelected;
        private int levelNumber;

        private void Awake()
        {
            button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Bind(LevelMenuItemState state, Action<int> selected)
        {
            levelNumber = state.LevelNumber;
            onSelected = selected;

            string verb = state.IsUnlocked ? "Play" : "Unlock";
            label.text = $"{verb} {state.DisplayName}";
            lockedIndicator.SetActive(!state.IsUnlocked);
            unlockedIndicator.SetActive(state.IsUnlocked);
            button.interactable = !state.IsBusy;
        }

        public void Unbind()
        {
            onSelected = null;
            levelNumber = 0;
        }

        private void HandleClick()
        {
            onSelected(levelNumber);
        }
    }
}
