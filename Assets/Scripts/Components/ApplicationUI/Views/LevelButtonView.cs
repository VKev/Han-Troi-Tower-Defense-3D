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

        private Action<int> onSelected;
        private int levelNumber;
        private LevelNodeProgress progress;
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
            Show(unlockedNode, unlocked && !isSelected);
            Show(unlockedSelectedNode, unlocked && isSelected);
            Show(clearedNode, cleared && !isSelected);
            Show(clearedSelectedNode, cleared && isSelected);
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
