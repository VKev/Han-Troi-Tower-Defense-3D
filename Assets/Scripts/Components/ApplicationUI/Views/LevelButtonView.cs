using System;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// How far along the journey a node sits, read from the save rather than inferred.
    /// </summary>
    public enum LevelNodeProgress
    {
        /// <summary>Not reachable yet.</summary>
        Locked,

        /// <summary>Open but not beaten.</summary>
        Unlocked,

        /// <summary>Beaten.</summary>
        Cleared
    }

    /// <summary>
    /// One node on the journey map: five states, each its own child object, exactly one of them
    /// shown at a time.
    /// </summary>
    /// <remarks>
    /// The five could have been one Image swapping between five sprites, and were at first. Five
    /// children is the friendlier shape to author against: every state is visible in the
    /// hierarchy, can be toggled by hand to see what it looks like, and can be restyled or
    /// resized on its own - the ringed states are half again as wide as the plain ones, so the
    /// gold ring reaches outside the body instead of squeezing it.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LevelButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        [Header("Node states - exactly one is shown at a time")]
        [Tooltip("Grey body with the padlock: the level is not reachable yet.")]
        [SerializeField] private GameObject lockedNode;

        [Tooltip("Red body: reachable but not beaten.")]
        [SerializeField] private GameObject unlockedNode;

        [Tooltip("Red body inside the gold ring: picked, not beaten.")]
        [SerializeField] private GameObject unlockedSelectedNode;

        [Tooltip("Green body: beaten.")]
        [SerializeField] private GameObject clearedNode;

        [Tooltip("Green body inside the gold ring: picked and beaten.")]
        [SerializeField] private GameObject clearedSelectedNode;

        [Header("Score")]
        [Tooltip("The row of stars under the node. Hidden outright on a level not yet beaten.")]
        [SerializeField] private GameObject starRow;

        [Tooltip("The three star slots, left to right. Each is filled or hollow, never hidden.")]
        [SerializeField] private Image[] starSlots = Array.Empty<Image>();

        [Tooltip("Drawn in a slot the run earned.")]
        [SerializeField] private Sprite earnedStar;

        [Tooltip("Drawn in a slot the run did not earn, so the score reads out of three.")]
        [SerializeField] private Sprite unearnedStar;

        private Action<int> onSelected;
        private int levelNumber;
        private LevelNodeProgress progress;
        private int stars;
        private bool isSelected;

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
            stars = state.Stars;

            // The number is the only thing the node says; the level's name is left to the
            // selection panel, which has the room to set it properly.
            label.text = state.LevelNumber.ToString("00");

            // A locked node is not selectable, so its ringed states are never reachable.
            button.interactable = !state.IsBusy && progress != LevelNodeProgress.Locked;
            SetSelected(false);
        }

        public void Unbind()
        {
            onSelected = null;
            levelNumber = 0;
            progress = LevelNodeProgress.Locked;
            stars = LevelStarRating.NoStars;
            isSelected = false;
        }

        public void SetSelected(bool isSelected)
        {
            this.isSelected = isSelected;
            ApplyState();
        }

        private void ApplyState()
        {
            bool locked = progress == LevelNodeProgress.Locked;
            bool unlocked = progress == LevelNodeProgress.Unlocked;
            bool cleared = progress == LevelNodeProgress.Cleared;

            // A locked node has no selected state of its own: it cannot be picked, so showing a
            // ring around it would promise something the button refuses to do.
            Show(lockedNode, locked);

            // The number goes with it. The padlock sits dead centre where the digits do, and both
            // are drawn in the same cream, so leaving the number on turns a locked node into a
            // smear rather than into a node that says which level it is.
            Show(label.gameObject, !locked);
            Show(unlockedNode, unlocked && !isSelected);
            Show(unlockedSelectedNode, unlocked && isSelected);
            Show(clearedNode, cleared && !isSelected);
            Show(clearedSelectedNode, cleared && isSelected);
            ApplyStars(cleared);
        }

        /// <summary>
        /// Fills the row out of three, and shows the row only on a level actually beaten.
        /// </summary>
        /// <remarks>
        /// An unbeaten level hides the row rather than showing three hollow stars. Three hollows
        /// under every locked node turns the map into a wall of empty score and buries the one
        /// thing the row is for, which is seeing at a glance where a star is still to be had.
        /// </remarks>
        private void ApplyStars(bool cleared)
        {
            Show(starRow, cleared && starSlots.Length > 0);
            if (!cleared)
            {
                return;
            }

            for (int index = 0; index < starSlots.Length; index++)
            {
                if (starSlots[index] == null)
                {
                    continue;
                }

                starSlots[index].sprite = index < stars ? earnedStar : unearnedStar;
            }
        }

        private static void Show(GameObject node, bool visible)
        {
            if (node != null && node.activeSelf != visible)
            {
                node.SetActive(visible);
            }
        }

        private void HandleClick()
        {
            onSelected?.Invoke(levelNumber);
        }
    }
}
