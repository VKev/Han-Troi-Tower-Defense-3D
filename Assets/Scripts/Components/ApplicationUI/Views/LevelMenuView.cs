using System;
using System.Collections.Generic;
using DG.Tweening;
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
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private Image progressFill;

        [Tooltip("The top bar's star count: every star earned across the whole journey.")]
        [SerializeField] private TMP_Text starTotalLabel;

        [Tooltip("The top bar's gold count: the player's wallet, which clearing levels pays into.")]
        [SerializeField] private TMP_Text goldTotalLabel;

        [Header("Reward flight")]
        [Tooltip("Where flown stars land - the star icon in the top bar.")]
        [SerializeField] private RectTransform starFlightTarget;

        [Tooltip("Where flown coins land - the coin icon in the top bar.")]
        [SerializeField] private RectTransform goldFlightTarget;

        [Tooltip("The stars thrown from the beaten level's node up to the top bar. A fixed pool; a haul bigger than the pool still counts up in full, it just flies in fewer pieces.")]
        [SerializeField] private Image[] rewardStars = Array.Empty<Image>();

        [Tooltip("The coins thrown from the beaten level's node up to the top bar.")]
        [SerializeField] private Image[] rewardCoins = Array.Empty<Image>();

        private const float RewardLaunchDelay = 0.35f;
        private const float RewardStagger = 0.09f;
        private const float RewardRiseDuration = 0.22f;
        private const float RewardFlightDuration = 0.55f;

        private readonly List<LevelMenuItemState> levels = new();
        private Action<int> onLevelSelected;
        private ISoundPlayer soundPlayer;
        private int selectedLevelNumber;
        private int goldTotal;
        private int starTotal;

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

        public void Show(
            IReadOnlyList<LevelMenuItemState> levels,
            int goldTotal,
            LevelMenuRewardState reward,
            Action<int> onLevelSelected)
        {
            StopRewardFlight();
            this.goldTotal = goldTotal;
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

            RenderStanding(reward);
            SelectInitialLevel();
            SetVisible(true);
            if (reward.HasAnything)
            {
                PlayRewardFlight(reward);
            }
        }

        public void Hide()
        {
            StopRewardFlight();
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

        /// <summary>
        /// How much of the journey is open, and what it has earned.
        /// </summary>
        /// <remarks>
        /// The two counters open on their pre-reward values when there is a reward to show, so
        /// the flight has somewhere to land. Landing is what moves them; see
        /// <see cref="PlayRewardFlight"/>.
        /// </remarks>
        private void RenderStanding(LevelMenuRewardState reward)
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

            starTotal = 0;
            for (int index = 0; index < levels.Count; index++)
            {
                starTotal += levels[index].Stars;
            }

            SetStarTotalText(starTotal - reward.StarsGained);
            SetGoldTotalText(goldTotal - reward.GoldGained);
        }

        private void SetStarTotalText(int value)
        {
            if (starTotalLabel != null)
            {
                starTotalLabel.text = Mathf.Max(0, value).ToString();
            }
        }

        private void SetGoldTotalText(int value)
        {
            if (goldTotalLabel != null)
            {
                goldTotalLabel.text = Mathf.Max(0, value).ToString("N0");
            }
        }

        /// <summary>
        /// Throws what the last run earned out of its node and up into the top bar.
        /// </summary>
        /// <remarks>
        /// The counters only move as pieces land, so the player is told where the number came
        /// from rather than watching it jump on its own. A run that earned nothing never gets
        /// here - the menu simply opens with its totals already settled, which is the right
        /// answer for a replay of a level already at three stars.
        ///
        /// Canvas layout is forced first: the top bar lays its icons out horizontally, so a
        /// target read before layout has run would be read at wherever the prefab left it.
        /// </remarks>
        private void PlayRewardFlight(LevelMenuRewardState reward)
        {
            if (!TryFindNodeOrigin(reward.LevelNumber, out Vector3 origin))
            {
                SetStarTotalText(starTotal);
                SetGoldTotalText(goldTotal);
                return;
            }

            Canvas.ForceUpdateCanvases();

            int starCount = Mathf.Min(reward.StarsGained, rewardStars.Length);
            int coinCount = reward.GoldGained > 0 ? Mathf.Min(6, rewardCoins.Length) : 0;
            float delay = RewardLaunchDelay;

            for (int index = 0; index < starCount; index++)
            {
                // Each piece carries its slice of the haul, and the slices are cumulative, so
                // the last one to land leaves the counter on exactly the real total however
                // awkwardly the haul divides by the number of pieces flown.
                int carried = SliceOf(reward.StarsGained, index, starCount);
                int runningTotal = starTotal - reward.StarsGained
                    + CumulativeSlice(reward.StarsGained, index, starCount);
                ThrowReward(
                    rewardStars[index],
                    origin,
                    starFlightTarget,
                    starTotalLabel == null ? null : starTotalLabel.rectTransform,
                    delay,
                    () => SetStarTotalText(runningTotal),
                    carried > 0);
                delay += RewardStagger;
            }

            for (int index = 0; index < coinCount; index++)
            {
                int runningTotal = goldTotal - reward.GoldGained
                    + CumulativeSlice(reward.GoldGained, index, coinCount);
                ThrowReward(
                    rewardCoins[index],
                    origin,
                    goldFlightTarget,
                    goldTotalLabel == null ? null : goldTotalLabel.rectTransform,
                    delay,
                    () => SetGoldTotalText(runningTotal),
                    true);
                delay += RewardStagger;
            }
        }

        private static int CumulativeSlice(int total, int index, int count)
        {
            return count <= 0 ? total : (int)((long)total * (index + 1) / count);
        }

        private static int SliceOf(int total, int index, int count)
        {
            return CumulativeSlice(total, index, count) - CumulativeSlice(total, index - 1, count);
        }

        /// <summary>
        /// Sends one piece from the node up to the top bar: a small hop out, then the flight in,
        /// and a nudge of both the icon it hit and the number it just moved.
        /// </summary>
        /// <remarks>
        /// The counter is nudged as well as the icon because the counter is the thing that
        /// actually changed. An icon bouncing on its own beside a number that quietly ticks over
        /// reads as decoration; bouncing the pair reads as the coin having gone in.
        /// </remarks>
        private void ThrowReward(
            Image piece,
            Vector3 origin,
            RectTransform target,
            RectTransform counter,
            float delay,
            Action onLanded,
            bool countsUp)
        {
            if (piece == null || target == null)
            {
                if (countsUp)
                {
                    onLanded();
                }

                return;
            }

            RectTransform pieceRect = piece.rectTransform;
            piece.gameObject.SetActive(true);
            piece.color = Color.white;
            pieceRect.position = origin;
            pieceRect.localScale = Vector3.zero;

            Vector3 hop = pieceRect.position
                + new Vector3(UnityEngine.Random.Range(-40f, 40f), 80f, 0f);

            DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .AppendInterval(delay)
                .Append(pieceRect.DOScale(1f, RewardRiseDuration).SetEase(Ease.OutBack))
                .Join(pieceRect.DOMove(hop, RewardRiseDuration).SetEase(Ease.OutQuad))
                .Append(pieceRect.DOMove(target.position, RewardFlightDuration).SetEase(Ease.InBack))
                .Join(pieceRect.DOScale(0.55f, RewardFlightDuration).SetEase(Ease.InQuad))
                .OnComplete(() =>
                {
                    piece.gameObject.SetActive(false);

                    // The number is written before either punch, so what bounces is the new one.
                    if (countsUp)
                    {
                        onLanded();
                    }

                    Punch(target);
                    Punch(counter);
                });
        }

        /// <summary>
        /// A short squash on something that has just changed.
        /// </summary>
        /// <remarks>
        /// Scale only, so a counter sitting in the top bar's horizontal layout can be punched
        /// without the row re-flowing around it - layout reads a rect, not a scale.
        /// </remarks>
        private static void Punch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.DOKill();
            rect.localScale = Vector3.one;
            rect.DOPunchScale(Vector3.one * 0.3f, 0.28f, 8, 0.7f).SetUpdate(true);
        }

        private bool TryFindNodeOrigin(int levelNumber, out Vector3 origin)
        {
            for (int index = 0; index < levels.Count && index < levelButtons.Length; index++)
            {
                if (levels[index].LevelNumber == levelNumber && levelButtons[index] != null)
                {
                    origin = levelButtons[index].transform.position;
                    return true;
                }
            }

            origin = default;
            return false;
        }

        /// <summary>
        /// Kills any flight in progress and puts every piece back in the pool.
        /// </summary>
        /// <remarks>
        /// Called on hide and again before a new flight, so a player who leaves the menu
        /// mid-flight never finds a coin frozen in the air on the way back in. The counters are
        /// not touched here; the screen is either about to be hidden or about to have them
        /// rewritten by <see cref="RenderStanding"/>.
        /// </remarks>
        private void StopRewardFlight()
        {
            DOTween.Kill(this);
            GroundPieces(rewardStars);
            GroundPieces(rewardCoins);
            if (starFlightTarget != null)
            {
                starFlightTarget.DOKill();
                starFlightTarget.localScale = Vector3.one;
            }

            if (goldFlightTarget != null)
            {
                goldFlightTarget.DOKill();
                goldFlightTarget.localScale = Vector3.one;
            }

            Settle(starTotalLabel);
            Settle(goldTotalLabel);
        }

        private static void Settle(TMP_Text label)
        {
            if (label != null)
            {
                label.rectTransform.DOKill();
                label.rectTransform.localScale = Vector3.one;
            }
        }

        private static void GroundPieces(Image[] pieces)
        {
            for (int index = 0; index < pieces.Length; index++)
            {
                if (pieces[index] != null)
                {
                    pieces[index].gameObject.SetActive(false);
                }
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
