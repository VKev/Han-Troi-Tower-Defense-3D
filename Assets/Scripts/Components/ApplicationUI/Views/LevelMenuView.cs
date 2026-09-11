using System;
using System.Collections.Generic;
using TMPro;
using TowerDefense3D.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// The journey screen: a scrollable trail of level nodes, the standing of the run above it, and
    /// the level the player has picked below it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        /// <summary>
        /// The backdrop and the journey map, which live outside the safe area so they run edge to
        /// edge - under the notch included - instead of leaving a border of whatever is behind the
        /// screen. Only the chrome is inset, because only the chrome has to stay readable and
        /// reachable. Shown and hidden with <see cref="root"/>.
        /// </summary>
        [SerializeField] private GameObject backdrop;
        [SerializeField] private LevelButtonView[] levelButtons = Array.Empty<LevelButtonView>();
        [SerializeField] private GameObject selectionPanel;

        // The selection panel is the one part of this screen on TextMeshPro: its lines are the
        // largest type on the menu, where uGUI's bitmap glyphs showed their edges. Typed as
        // TMP_Text rather than TextMeshProUGUI so a swap to the non-Canvas variant needs no edit
        // here. The rest of the screen is still uGUI Text, on purpose - see subtitleLabel below.
        //
        // The panel's third line, "Selected Details", is deliberately absent: it reads the same
        // whatever the run has done, so it is authored once in the prefab and no field here
        // points at it. Holding a reference the view never writes only invites someone to start
        // writing it. The star panel's label used to be in the same boat and no longer is - it
        // now counts the stars actually earned, so it is wired below.
        [SerializeField] private TMP_Text selectionChapter;
        [SerializeField] private TMP_Text selectionTitle;
        [SerializeField] private Button enterMapButton;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Text progressLabel;
        [SerializeField] private Image progressFill;

        [Tooltip("The top bar's star count: every star earned across the whole journey.")]
        [SerializeField] private Text starTotalLabel;

        private readonly List<LevelMenuItemState> levels = new();
        private Action<int> onLevelSelected;
        private ISoundPlayer soundPlayer;
        private int selectedLevelNumber;

        public void Initialize(ISoundPlayer player)
        {
            soundPlayer = player;
        }

        private void Awake()
        {
            if (enterMapButton != null)
            {
                enterMapButton.onClick.AddListener(HandleEnterMapClicked);
            }
        }

        private void OnDestroy()
        {
            if (enterMapButton != null)
            {
                enterMapButton.onClick.RemoveListener(HandleEnterMapClicked);
            }
        }

        public void Show(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            int levelCount = levels.Count;
            EnsureAuthoredCapacity(levelCount);
            UnbindButtons();
            this.levels.Clear();
            this.onLevelSelected = onLevelSelected;

            for (int index = 0; index < levelButtons.Length; index++)
            {
                LevelButtonView view = levelButtons[index];
                bool hasLevel = index < levelCount;
                view.gameObject.SetActive(hasLevel);
                if (hasLevel)
                {
                    LevelMenuItemState state = levels[index];
                    this.levels.Add(state);
                    view.Bind(state, ReadProgress(state), HandleLevelNodeClicked);
                }
            }

            RenderStanding();
            SelectInitialLevel();
            SetVisible(true);
        }

        public void Hide()
        {
            UnbindButtons();
            levels.Clear();
            onLevelSelected = null;
            selectedLevelNumber = 0;
            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }

            SetVisible(false);
        }

        /// <summary>
        /// Reads a node's state straight off the save data.
        /// </summary>
        /// <remarks>
        /// This used to guess: the highest unlocked level was "current" and everything below it
        /// was "completed". That guess is wrong the moment a player opens a level and loses -
        /// the level below still showed as beaten. The save has carried
        /// <see cref="LevelMenuItemState.IsCleared"/> all along, so the nodes now read it.
        /// </remarks>
        private static LevelNodeProgress ReadProgress(LevelMenuItemState state)
        {
            if (!state.IsUnlocked)
            {
                return LevelNodeProgress.Locked;
            }

            return state.IsCleared
                ? LevelNodeProgress.Cleared
                : LevelNodeProgress.Unlocked;
        }

        /// <summary>How much of the journey is open.</summary>
        private void RenderStanding()
        {
            int unlockedCount = 0;
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index].IsUnlocked)
                {
                    unlockedCount++;
                }
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = $"{levels.Count} LĂNG · THE JOURNEY";
            }

            if (progressLabel != null)
            {
                progressLabel.text = $"{unlockedCount}/{levels.Count}";
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = levels.Count > 0
                    ? unlockedCount / (float)levels.Count
                    : 0f;
            }

            if (starTotalLabel != null)
            {
                int starCount = 0;
                for (int index = 0; index < levels.Count; index++)
                {
                    starCount += levels[index].Stars;
                }

                starTotalLabel.text = starCount.ToString();
            }
        }

        private void EnsureAuthoredCapacity(int requiredCount)
        {
            if (requiredCount > levelButtons.Length)
            {
                throw new InvalidOperationException(
                    $"LevelMenuView has {levelButtons.Length} authored buttons but requires {requiredCount}.");
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
            if (backdrop != null)
            {
                backdrop.SetActive(visible);
            }

            root.SetActive(visible);
        }

        private void SelectInitialLevel()
        {
            for (int index = levels.Count - 1; index >= 0; index--)
            {
                if (levels[index].IsUnlocked)
                {
                    SelectLevel(levels[index].LevelNumber);
                    return;
                }
            }

            if (selectionPanel != null)
            {
                selectionPanel.SetActive(false);
            }
        }

        private void HandleLevelNodeClicked(int levelNumber)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                LevelMenuItemState state = levels[index];
                if (state.LevelNumber != levelNumber)
                {
                    continue;
                }

                if (state.IsUnlocked)
                {
                    soundPlayer?.Play(SoundId.LevelSelected);
                    SelectLevel(levelNumber);
                }
                else
                {
                    onLevelSelected?.Invoke(levelNumber);
                }

                return;
            }
        }

        private void SelectLevel(int levelNumber)
        {
            selectedLevelNumber = levelNumber;
            LevelMenuItemState selected = default;
            for (int index = 0; index < levels.Count; index++)
            {
                LevelMenuItemState state = levels[index];
                bool isSelected = state.LevelNumber == levelNumber;
                levelButtons[index].SetSelected(isSelected);
                if (isSelected)
                {
                    selected = state;
                }
            }

            if (selectionPanel != null)
            {
                selectionPanel.SetActive(true);
            }

            if (selectionChapter != null)
            {
                selectionChapter.text = $"HỒI {selected.LevelNumber:00} · ĐANG CHỌN";
            }

            if (selectionTitle != null)
            {
                // The level's own name carries the panel; the chapter line above already
                // numbers it, so repeating the number here only crowded the title. Printed
                // as authored - the catalog already capitalises each word, and shouting a
                // Vietnamese name in full caps loses the diacritics' shape.
                selectionTitle.text = selected.DisplayName;
            }
        }

        private void HandleEnterMapClicked()
        {
            if (selectedLevelNumber > 0)
            {
                soundPlayer?.Play(SoundId.EnterLevel);
                onLevelSelected?.Invoke(selectedLevelNumber);
            }
        }
    }
}
