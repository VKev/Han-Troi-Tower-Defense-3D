using System;
using System.Collections.Generic;
using TMPro;
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
        // for every level, so it is authored once in the prefab and no field here points at it.
        // Holding a reference the view never writes only invites someone to start writing it.
        [SerializeField] private TMP_Text selectionChapter;
        [SerializeField] private TMP_Text selectionTitle;
        [SerializeField] private Button enterMapButton;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private Text progressLabel;
        [SerializeField] private Image progressFill;
        [SerializeField] private Text starLabel;

        private readonly List<LevelMenuItemState> levels = new();
        private Action<int> onLevelSelected;
        private int selectedLevelNumber;

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

            int currentLevelNumber = FindCurrentLevelNumber(levels);
            for (int index = 0; index < levelButtons.Length; index++)
            {
                LevelButtonView view = levelButtons[index];
                bool hasLevel = index < levelCount;
                view.gameObject.SetActive(hasLevel);
                if (hasLevel)
                {
                    LevelMenuItemState state = levels[index];
                    this.levels.Add(state);
                    view.Bind(state, ReadProgress(state, currentLevelNumber), HandleLevelNodeClicked);
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
        /// The level the player is on: the last one unlocked. Everything unlocked before it had to
        /// be beaten to get here, which is all the trail needs in order to read.
        /// </summary>
        private static int FindCurrentLevelNumber(IReadOnlyList<LevelMenuItemState> levels)
        {
            int current = 0;
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index].IsUnlocked && levels[index].LevelNumber > current)
                {
                    current = levels[index].LevelNumber;
                }
            }

            return current;
        }

        private static LevelNodeProgress ReadProgress(LevelMenuItemState state, int currentLevelNumber)
        {
            if (!state.IsUnlocked)
            {
                return LevelNodeProgress.Locked;
            }

            return state.LevelNumber < currentLevelNumber
                ? LevelNodeProgress.Completed
                : LevelNodeProgress.Current;
        }

        /// <summary>How much of the journey is open, and what the run has to show for it.</summary>
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

            if (starLabel != null)
            {
                // TODO: count the stars actually earned once level results are saved.
                starLabel.text = $"★ 0/{levels.Count * 3}";
            }
        }

        private void EnsureAuthoredCapacity(int requiredCount)
        {
            if (requiredCount <= levelButtons.Length)
            {
                return;
            }

            LevelButtonView template = FindTemplate();
            int authoredCount = levelButtons.Length;
            Vector2 step = ReadJourneyStep();
            Array.Resize(ref levelButtons, requiredCount);
            for (int index = authoredCount; index < requiredCount; index++)
            {
                LevelButtonView copy = Instantiate(
                    template,
                    template.transform.parent,
                    false);
                copy.name = $"{template.name} {index + 1}";
                PlaceAlongJourney(copy, levelButtons[index - 1], step, index - authoredCount);
                levelButtons[index] = copy;
            }
        }

        /// <summary>
        /// How far apart the authored nodes sit, so a level the catalog gained carries the trail on
        /// instead of landing on top of the last node.
        /// </summary>
        private Vector2 ReadJourneyStep()
        {
            if (levelButtons.Length < 2)
            {
                return new Vector2(300f, 0f);
            }

            var last = (RectTransform)levelButtons[levelButtons.Length - 1].transform;
            var previous = (RectTransform)levelButtons[levelButtons.Length - 2].transform;
            return last.anchoredPosition - previous.anchoredPosition;
        }

        private static void PlaceAlongJourney(
            LevelButtonView copy,
            LevelButtonView previous,
            Vector2 step,
            int stepIndex)
        {
            var placed = (RectTransform)copy.transform;
            var from = (RectTransform)previous.transform;

            // The authored trail zig-zags, so the vertical half of the step flips each time and the
            // added nodes keep weaving instead of climbing off the top of the map.
            float verticalSign = stepIndex % 2 == 0 ? 1f : -1f;
            placed.anchoredPosition = from.anchoredPosition
                + new Vector2(Mathf.Abs(step.x), step.y * verticalSign);
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
                onLevelSelected?.Invoke(selectedLevelNumber);
            }
        }
    }
}
