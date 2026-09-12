using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelOutcomeHudView : MonoBehaviour, ILevelOutcomeHudView
    {
        [SerializeField] private GameObject root;
        [Tooltip("Sorting override that keeps this modal above everything else, the tutorial overlay included. Without it the modal draws inside the gameplay canvas and the tutorial's dimmer and running text cover it.")]
        [SerializeField] private Canvas modalCanvas;

        /// <summary>
        /// Above the tutorial overlay, which sorts at 100.
        /// </summary>
        /// <remarks>
        /// A modal is a question put to the player, so nothing may draw over it - and a tutorial
        /// beat can still be mid-sentence when one opens. The order is only claimed while the
        /// modal is up; a hidden modal holding the top of the stack would keep raycasts that
        /// belong to the HUD underneath it.
        /// </remarks>
        private const int ModalSortingOrder = 200;

        private void ApplyModalSorting(bool visible)
        {
            if (modalCanvas == null)
            {
                return;
            }

            modalCanvas.overrideSorting = visible;
            if (visible)
            {
                modalCanvas.sortingOrder = ModalSortingOrder;
            }
        }

        [SerializeField] private TMP_Text titleText;

        [Header("Score")]
        [Tooltip("The three star slots, left to right. Each is filled or hollow, never hidden.")]
        [SerializeField] private Image[] starSlots = Array.Empty<Image>();

        [Tooltip("Drawn in a slot the run earned.")]
        [SerializeField] private Sprite earnedStar;

        [Tooltip("Drawn in a slot the run did not earn, so the score reads out of three.")]
        [SerializeField] private Sprite unearnedStar;

        [Tooltip("The burst behind the stars. Hidden on a defeat, which has nothing to celebrate.")]
        [SerializeField] private GameObject starBurst;

        [Header("Rows")]
        [Tooltip("The green bar. Driven by its rect rather than by fillAmount, so the sliced end caps keep their radius - the same bargain the frog's HUD bar makes.")]
        [SerializeField] private RectTransform healthFill;

        [SerializeField] private TMP_Text healthValueText;
        [SerializeField] private TMP_Text goldValueText;

        [Tooltip("The coin drawn beside the gold total. The burst is thrown from where this sits, so the coins leave the row the player is watching.")]
        [SerializeField] private Image coinIcon;

        [Header("Victory animation")]
        [Tooltip("The stars that ride the health bar up to their slots, one per slot in the same order. Their positions along the bar are set from the score thresholds, so the bar and the markers cannot disagree.")]
        [SerializeField] private Image[] healthStarMarkers = Array.Empty<Image>();

        [Tooltip("Covers the panel while the victory animation plays and cuts it to its end when pressed. Left inactive in the prefab; the view only ever switches it on and off.")]
        [SerializeField] private Button victorySkipButton;

        [Tooltip("The coins thrown out of the gold row while the total counts up. A fixed pool - the burst throws one per slice of the count-up and never needs more than it has.")]
        [SerializeField] private Image[] burstCoins = Array.Empty<Image>();

        [Tooltip("The party cannons at the bottom of the screen. Fired as the card pops, so the celebration is already in the air by the time the score starts counting.")]
        [SerializeField] private ConfettiBurstView confetti;

        [Header("Buttons")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button nextLevelButton;
        [SerializeField] private Button returnToLevelMenuButton;

        private bool isInitialized;
        private bool hasPlayedVictoryAnimation;
        private Sequence victorySequence;
        private Tween starBurstRotationTween;
        private LevelOutcomeHudState latestVictoryState;
        private int goldCountTarget;
        private int thrownCoinCount;

        private static readonly float[] StarThresholds = { 0.05f, 0.5f, 1f };
        private const float StarGrowDuration = 0.25f;
        private const float StarFlightDuration = 0.85f;
        private const float GoldCountDuration = 0.9f;

        public event Action PlayAgainRequested;
        public event Action NextLevelRequested;
        public event Action ReturnToLevelMenuRequested;

        public void Initialize()
        {
            if (isInitialized)
            {
                return;
            }

            playAgainButton.onClick.AddListener(HandlePlayAgainClicked);
            nextLevelButton.onClick.AddListener(HandleNextLevelClicked);
            returnToLevelMenuButton.onClick.AddListener(HandleReturnToLevelMenuClicked);
            if (victorySkipButton != null)
            {
                victorySkipButton.onClick.AddListener(SkipVictoryAnimation);
            }

            isInitialized = true;
        }

        public void Render(LevelOutcomeHudState state)
        {
            ApplyModalSorting(state.IsVisible);
            root.SetActive(state.IsVisible);

            if (titleText != null)
            {
                titleText.text = state.TitleText;
            }

            if (!state.IsVisible)
            {
                ResetVictoryAnimation();
                RenderStars(state);
                RenderHealth(state);
            }
            else if (state.Outcome == LevelOutcome.Victory)
            {
                if (!hasPlayedVictoryAnimation)
                {
                    PlayVictoryAnimation(state);
                }
            }
            else
            {
                ResetVictoryAnimation();
                RenderStars(state);
                RenderHealth(state);
            }

            // While the victory sequence runs the count-up owns this label: writing the final
            // total here every frame would leave nothing to count towards.
            if (goldValueText != null && victorySequence == null)
            {
                goldValueText.text = state.Gold.ToString("N0");
            }

            // Hidden rather than disabled so the button row re-flows around what is left.
            nextLevelButton.gameObject.SetActive(state.NextLevelVisible);
        }

        private void PlayVictoryAnimation(LevelOutcomeHudState state)
        {
            hasPlayedVictoryAnimation = true;
            latestVictoryState = state;
            victorySequence?.Kill();
            DOTween.Kill(this);
            SetVictorySkipActive(true);
            if (confetti != null)
            {
                confetti.Play();
            }

            for (int index = 0; index < starSlots.Length; index++)
            {
                if (starSlots[index] != null)
                {
                    starSlots[index].sprite = unearnedStar;
                    starSlots[index].rectTransform.localScale = Vector3.one;
                }

                if (index < healthStarMarkers.Length && healthStarMarkers[index] != null)
                {
                    healthStarMarkers[index].sprite = index < state.Stars ? earnedStar : unearnedStar;
                    healthStarMarkers[index].gameObject.SetActive(true);
                    PositionHealthStarMarker(index);
                }
            }

            if (starBurst != null)
            {
                starBurst.SetActive(true);
                StartStarBurstRotation();
            }

            HideBurstCoins();
            goldCountTarget = state.Gold;
            thrownCoinCount = 0;
            if (goldValueText != null)
            {
                goldValueText.text = "0";
            }

            SetHealthRatio(0f);
            if (healthValueText != null)
            {
                healthValueText.text = $"{state.CurrentHealth}/{state.MaximumHealth}";
            }

            RectTransform rootRect = root.transform as RectTransform;
            rootRect.localScale = Vector3.one * 0.92f;
            float targetRatio = state.HealthRatio;
            const float popupDuration = 0.3f;
            const float healthDelay = 0.2f;
            const float healthDuration = 2.2f;
            victorySequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            victorySequence.Append(rootRect.DOScale(1f, popupDuration).SetEase(Ease.OutBack));
            victorySequence.AppendInterval(healthDelay);
            victorySequence.Append(DOTween.To(
                () => healthFill.anchorMax.x,
                SetHealthRatio,
                targetRatio,
                healthDuration).SetEase(Ease.OutCubic));

            for (int index = 0; index < state.Stars && index < starSlots.Length; index++)
            {
                int starIndex = index;
                float threshold = StarThresholds[starIndex];
                float reachedAt = popupDuration + healthDelay
                    + healthDuration * Mathf.Min(1f, threshold / Mathf.Max(targetRatio, 0.0001f));
                victorySequence.InsertCallback(reachedAt, () => FlyStarToSlot(starIndex));
            }

            victorySequence.AppendInterval(StarGrowDuration + StarFlightDuration);

            // The purse counts up last, after the bar and the stars have settled, so the coins it
            // throws are the only thing moving when they fly.
            if (goldCountTarget > 0 && burstCoins.Length > 0)
            {
                victorySequence.Append(DOTween
                    .To(() => 0, SetGoldDisplay, goldCountTarget, GoldCountDuration)
                    .SetEase(Ease.OutCubic));
            }

            victorySequence.OnComplete(ApplyFinalVictoryState);
        }

        private void StartStarBurstRotation()
        {
            starBurstRotationTween?.Kill();
            starBurst.transform.localRotation = Quaternion.identity;
            starBurstRotationTween = starBurst.transform
                .DOLocalRotate(new Vector3(0f, 0f, -360f), 10f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void StopConfetti()
        {
            if (confetti != null)
            {
                confetti.Stop();
            }
        }

        private void StopStarBurstRotation()
        {
            starBurstRotationTween?.Kill();
            starBurstRotationTween = null;
            if (starBurst != null)
            {
                starBurst.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Parks one marker over the point on the bar its star is worth.
        /// </summary>
        /// <remarks>
        /// Read from <see cref="StarThresholds"/> rather than left where the prefab put it: the
        /// same array times the star's flight, so a threshold that moves moves the marker with it
        /// instead of leaving a star taking off from the wrong place on the bar.
        /// </remarks>
        private void PositionHealthStarMarker(int index)
        {
            RectTransform marker = healthStarMarkers[index].rectTransform;
            float threshold = StarThresholds[Mathf.Min(index, StarThresholds.Length - 1)];
            marker.anchorMin = new Vector2(threshold, 0.5f);
            marker.anchorMax = marker.anchorMin;
            marker.anchoredPosition = new Vector2(0f, 46f);
            marker.localScale = Vector3.one;
        }

        private void FlyStarToSlot(int index)
        {
            if (index >= healthStarMarkers.Length || index >= starSlots.Length
                || healthStarMarkers[index] == null || starSlots[index] == null)
            {
                return;
            }

            Image marker = healthStarMarkers[index];
            Image slot = starSlots[index];
            DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Append(marker.rectTransform
                    .DOScale(1.5f, StarGrowDuration)
                    .SetEase(Ease.OutBack))
                .Append(marker.transform
                    .DOMove(slot.transform.position, StarFlightDuration)
                    .SetEase(Ease.InOutCubic))
                .Join(marker.rectTransform
                    .DOScale(0.85f, StarFlightDuration)
                    .SetEase(Ease.InCubic))
                .OnComplete(() =>
                {
                    slot.sprite = earnedStar;
                    marker.gameObject.SetActive(false);
                    slot.rectTransform
                        .DOPunchScale(Vector3.one * 0.24f, 0.3f, 7, 0.6f)
                        .SetUpdate(true)
                        .SetTarget(this);
                });
        }

        private void SetVictorySkipActive(bool active)
        {
            if (victorySkipButton != null)
            {
                victorySkipButton.gameObject.SetActive(active);
            }
        }

        private void SkipVictoryAnimation()
        {
            victorySequence?.Kill();
            victorySequence = null;
            DOTween.Kill(this);
            StopConfetti();
            StopStarBurstRotation();
            HideBurstCoins();
            ApplyFinalVictoryState();
        }

        private void ApplyFinalVictoryState()
        {
            victorySequence = null;
            SetVictorySkipActive(false);
            SetHealthRatio(latestVictoryState.HealthRatio);
            RenderStars(latestVictoryState);

            for (int index = 0; index < healthStarMarkers.Length; index++)
            {
                if (healthStarMarkers[index] != null)
                {
                    healthStarMarkers[index].gameObject.SetActive(false);
                }
            }

            if (root != null)
            {
                root.transform.localScale = Vector3.one;
            }

            if (goldValueText != null)
            {
                goldValueText.text = latestVictoryState.Gold.ToString("N0");
            }

            // A skip kills every tween this view owns, the burst's spin among them, so the finale
            // would otherwise end on a burst frozen mid-turn.
            if (starBurst != null && starBurst.activeSelf && starBurstRotationTween == null)
            {
                StartStarBurstRotation();
            }

            StartFullStarCelebration();
        }

        /// <summary>
        /// Breathes the star row once the run has earned every one of them.
        /// </summary>
        /// <remarks>
        /// Only a perfect score gets it. A pulse on a two-star row would congratulate the player
        /// for the run they did not have, and saying which run they did have is the panel's whole
        /// job. The stagger keeps the three from reading as one block scaling.
        /// </remarks>
        private void StartFullStarCelebration()
        {
            if (starSlots.Length == 0 || latestVictoryState.Stars < starSlots.Length)
            {
                return;
            }

            for (int index = 0; index < starSlots.Length; index++)
            {
                Image slot = starSlots[index];
                if (slot == null)
                {
                    continue;
                }

                slot.rectTransform.localScale = Vector3.one;
                slot.rectTransform
                    .DOScale(1.16f, 0.5f)
                    .SetDelay(index * 0.12f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(true)
                    .SetTarget(this);
            }
        }

        private void ResetStarScales()
        {
            for (int index = 0; index < starSlots.Length; index++)
            {
                if (starSlots[index] != null)
                {
                    starSlots[index].rectTransform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Prints the running total and throws a coin each time it passes the next slice of the
        /// way there, so the spray thins out exactly as the count-up eases to a stop.
        /// </summary>
        private void SetGoldDisplay(int value)
        {
            if (goldValueText != null)
            {
                goldValueText.text = value.ToString("N0");
            }

            while (thrownCoinCount < burstCoins.Length
                && value >= (long)goldCountTarget * (thrownCoinCount + 1) / burstCoins.Length)
            {
                ThrowCoin(burstCoins[thrownCoinCount]);
                thrownCoinCount++;
            }
        }

        /// <summary>
        /// Throws one coin out of the gold row and lets it fall away.
        /// </summary>
        /// <remarks>
        /// The coin is put back on the row's own coin before it is thrown rather than left where
        /// the prefab parked it: the pool hangs off the modal root so a coin can fly clear of the
        /// card, which is the one place it cannot be authored already in position.
        /// </remarks>
        private void ThrowCoin(Image coin)
        {
            if (coin == null || coinIcon == null)
            {
                return;
            }

            RectTransform source = coinIcon.rectTransform;
            RectTransform coinRect = coin.rectTransform;
            coin.gameObject.SetActive(true);
            coin.color = Color.white;
            coinRect.localRotation = Quaternion.identity;
            coinRect.localScale = Vector3.one * UnityEngine.Random.Range(0.55f, 0.95f);
            coinRect.position = source.position;

            Vector3 start = coinRect.anchoredPosition3D;
            start.z = 0f;
            coinRect.anchoredPosition3D = start;

            float spread = Mathf.Max(source.rect.width, 1f);
            Vector3 apex = start + new Vector3(
                UnityEngine.Random.Range(-2.4f, 2.4f) * spread,
                UnityEngine.Random.Range(1.5f, 3f) * spread,
                0f);
            Vector3 landing = apex + new Vector3(
                UnityEngine.Random.Range(-0.6f, 0.6f) * spread,
                -UnityEngine.Random.Range(3f, 4.5f) * spread,
                0f);

            const float riseDuration = 0.3f;
            const float fallDuration = 0.5f;
            DOTween.Sequence()
                .SetUpdate(true)
                .SetTarget(this)
                .Join(coinRect
                    .DOLocalRotate(
                        new Vector3(0f, 0f, UnityEngine.Random.Range(-260f, 260f)),
                        riseDuration + fallDuration,
                        RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear))
                .Append(TweenAnchoredPosition(coinRect, apex, riseDuration).SetEase(Ease.OutQuad))
                .Append(TweenAnchoredPosition(coinRect, landing, fallDuration).SetEase(Ease.InQuad))
                .Join(TweenFadeOut(coin, fallDuration).SetEase(Ease.InQuad))
                .OnComplete(() => coin.gameObject.SetActive(false));
        }

        // Both driven through DOTween.To rather than the DOAnchorPos3D and DOFade shortcuts:
        // those live in DOTweenModuleUI, which this assembly does not reference, and the rest of
        // the HUD already tweens rects and colours this way.
        private static Tween TweenAnchoredPosition(RectTransform target, Vector3 end, float duration)
        {
            return DOTween.To(
                () => target.anchoredPosition3D,
                value => target.anchoredPosition3D = value,
                end,
                duration);
        }

        private static Tween TweenFadeOut(Graphic target, float duration)
        {
            Color faded = target.color;
            faded.a = 0f;
            return DOTween.To(() => target.color, value => target.color = value, faded, duration);
        }

        private void HideBurstCoins()
        {
            for (int index = 0; index < burstCoins.Length; index++)
            {
                if (burstCoins[index] != null)
                {
                    burstCoins[index].gameObject.SetActive(false);
                }
            }
        }

        private void SetHealthRatio(float ratio)
        {
            if (healthFill == null)
            {
                return;
            }

            healthFill.anchorMin = new Vector2(0f, healthFill.anchorMin.y);
            healthFill.anchorMax = new Vector2(Mathf.Clamp01(ratio), healthFill.anchorMax.y);
        }

        private void ResetVictoryAnimation()
        {
            victorySequence?.Kill();
            victorySequence = null;
            DOTween.Kill(this);
            StopConfetti();
            StopStarBurstRotation();
            HideBurstCoins();
            ResetStarScales();
            hasPlayedVictoryAnimation = false;
            SetVictorySkipActive(false);
            for (int index = 0; index < healthStarMarkers.Length; index++)
            {
                if (healthStarMarkers[index] != null)
                {
                    healthStarMarkers[index].gameObject.SetActive(false);
                }
            }

            if (root != null)
            {
                root.transform.localScale = Vector3.one;
            }
        }

        private void RenderStars(LevelOutcomeHudState state)
        {
            for (int index = 0; index < starSlots.Length; index++)
            {
                if (starSlots[index] == null)
                {
                    continue;
                }

                starSlots[index].sprite = index < state.Stars ? earnedStar : unearnedStar;
            }

            if (starBurst != null)
            {
                starBurst.SetActive(state.Outcome == LevelOutcome.Victory);
            }
        }

        /// <summary>
        /// Stretches the bar from its left edge, exactly as the frog's own HUD bar does.
        /// </summary>
        /// <remarks>
        /// The right anchor moves and the left one stays at zero, rather than setting
        /// <c>Image.fillAmount</c>: a filled image ignores its sprite's border, which would
        /// flatten the rounded caps this bar is drawn with.
        /// </remarks>
        private void RenderHealth(LevelOutcomeHudState state)
        {
            SetHealthRatio(state.HealthRatio);

            if (healthValueText != null)
            {
                healthValueText.text = $"{state.CurrentHealth}/{state.MaximumHealth}";
            }
        }

        public void Shutdown()
        {
            if (!isInitialized)
            {
                return;
            }

            playAgainButton.onClick.RemoveListener(HandlePlayAgainClicked);
            nextLevelButton.onClick.RemoveListener(HandleNextLevelClicked);
            returnToLevelMenuButton.onClick.RemoveListener(HandleReturnToLevelMenuClicked);
            if (victorySkipButton != null)
            {
                victorySkipButton.onClick.RemoveListener(SkipVictoryAnimation);
            }

            ResetVictoryAnimation();
            isInitialized = false;
        }

        private void HandlePlayAgainClicked()
        {
            PlayAgainRequested?.Invoke();
        }

        private void HandleNextLevelClicked()
        {
            NextLevelRequested?.Invoke();
        }

        private void HandleReturnToLevelMenuClicked()
        {
            ReturnToLevelMenuRequested?.Invoke();
        }
    }
}
