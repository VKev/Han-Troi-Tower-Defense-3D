using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// How far along the journey a node sits. The menu works this out from the unlock state alone:
    /// the highest unlocked level is the one the player is on, everything unlocked before it has
    /// been beaten, and the rest is still shut.
    /// </summary>
    public enum LevelNodeProgress
    {
        Locked,
        Current,
        Completed
    }

    /// <summary>
    /// One node on the journey map. The node body carries the level number; the trimmings around it
    /// say which of the three states it is in - beaten, current, or locked.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Image nodeImage;
        [SerializeField] private GameObject lockedIndicator;
        [SerializeField] private GameObject unlockedIndicator;
        [SerializeField] private GameObject ringIndicator;
        [SerializeField] private GameObject currentBadge;
        [SerializeField] private Text starsLabel;
        [SerializeField] private Text requirementLabel;

        [SerializeField] private Color completedColor = new(0.16f, 0.52f, 0.40f, 1f);
        [SerializeField] private Color currentColor = new(0.92f, 0.66f, 0.16f, 1f);
        [SerializeField] private Color lockedColor = new(0.24f, 0.23f, 0.21f, 1f);
        [SerializeField] private Color selectedColor = new(0.96f, 0.80f, 0.30f, 1f);

        private Action<int> onSelected;
        private int levelNumber;
        private LevelNodeProgress progress;

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

        public void Bind(LevelMenuItemState state, LevelNodeProgress progress, Action<int> selected)
        {
            levelNumber = state.LevelNumber;
            onSelected = selected;
            this.progress = progress;

            label.text = state.LevelNumber.ToString("00");
            if (titleLabel != null)
            {
                titleLabel.text = state.DisplayName.ToUpperInvariant();
            }

            bool isLocked = progress == LevelNodeProgress.Locked;
            lockedIndicator.SetActive(isLocked);
            unlockedIndicator.SetActive(progress == LevelNodeProgress.Completed);
            SetActiveIfPresent(ringIndicator, progress == LevelNodeProgress.Current);
            SetActiveIfPresent(currentBadge, progress == LevelNodeProgress.Current);

            if (starsLabel != null)
            {
                // TODO: show the stars actually earned once level results are saved. Until then the
                // row keeps its place on beaten nodes with an empty score.
                starsLabel.gameObject.SetActive(progress == LevelNodeProgress.Completed);
                starsLabel.text = "☆☆☆";
            }

            if (requirementLabel != null)
            {
                // TODO: show the real star requirement once levels carry one.
                requirementLabel.gameObject.SetActive(isLocked);
                requirementLabel.text = "CHƯA MỞ";
            }

            button.interactable = !state.IsBusy;
            SetSelected(false);
        }

        public void Unbind()
        {
            onSelected = null;
            levelNumber = 0;
            progress = LevelNodeProgress.Locked;
        }

        public void SetSelected(bool isSelected)
        {
            if (nodeImage == null)
            {
                return;
            }

            nodeImage.color = isSelected ? selectedColor : BaseColor();
        }

        private Color BaseColor()
        {
            switch (progress)
            {
                case LevelNodeProgress.Completed:
                    return completedColor;
                case LevelNodeProgress.Current:
                    return currentColor;
                default:
                    return lockedColor;
            }
        }

        private static void SetActiveIfPresent(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }

        private void HandleClick()
        {
            onSelected?.Invoke(levelNumber);
        }
    }
}
