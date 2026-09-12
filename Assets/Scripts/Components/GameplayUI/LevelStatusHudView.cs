using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelStatusHudView : MonoBehaviour, ILevelStatusHudView, IWaveThreeDefeatHudView
    {
        [SerializeField] private TMP_Text goldText;

        [Tooltip("Optional. The frog's bar carries the health by its length, so the numbers are left off unless a design asks for them; wire a label here and they come back.")]
        [SerializeField] private TMP_Text healthText;

        [Tooltip("The green bar. It is a 9-sliced capsule stretched from its left edge, so its length is driven by the rect rather than by Image.fillAmount - a filled image ignores sprite borders and would flatten the rounded end caps.")]
        [SerializeField] private Image healthFill;

        [Tooltip("The bar's container, hidden while the frog is still untouched. The damage shake plays on the whole status HUD rather than on this, so the portrait and the gold move with the bar.")]
        [SerializeField] private RectTransform healthShakeTarget;

        [SerializeField, Min(0f)] private float healthTweenDuration = 0.32f;
        [SerializeField, Min(0f)] private float healthShakeDuration = 0.38f;
        [SerializeField, Min(0f)] private float healthShakeStrength = 9f;

        [Tooltip("Laid over the health fill and flashed on a hit. It carries its own colour instead of tinting the fill, because tinting cannot make the fill red - the green lives in the sprite and the UI shader multiplies, so a red tint on a green bar only produces a dark, muddy green.")]
        [SerializeField] private Image healthDamageOverlay;

        [Tooltip("The colour of that overlay. Drawn as itself rather than multiplied into the bar, so red really is red.")]
        [SerializeField] private Color healthDamageFlashColor = new Color(0.93f, 0.21f, 0.21f, 1f);

        [Tooltip("How long the bar takes to settle back to its own colour once the drain has finished. The red itself snaps on instantly - a hit is not a thing that eases in.")]
        [SerializeField, Min(0f)] private float healthFlashFadeBackDuration = 0.22f;

        [Tooltip("How long the gold counter takes to run down to a new balance. Zero snaps it.")]
        [SerializeField, Min(0f)] private float goldTweenDuration = 0.35f;

        [Header("Cannot afford")]
        [Tooltip("The colour the balance flashes when a purchase is refused for want of gold.")]
        [SerializeField] private Color goldRefusedFlashColor = new Color(0.95f, 0.26f, 0.22f, 1f);

        [Tooltip("How long the refused balance stays red before returning to its authored colour.")]
        [SerializeField, Min(0f)] private float goldRefusedFlashDuration = 0.5f;

        [Tooltip("How far the balance swells on a refused purchase, as a fraction of its size.")]
        [SerializeField, Min(0f)] private float goldRefusedPunchScale = 0.35f;

        [Header("Spending")]
        [Tooltip("The coin beside the balance. Optional - without it only the number reacts to a purchase.")]
        [SerializeField] private RectTransform goldIcon;

        [Tooltip("How far the coin and the balance swell when gold is spent, as a fraction of their size. Smaller than the refused swell on purpose: a purchase that went through is good news, not an answer to an objection.")]
        [SerializeField, Min(0f)] private float goldSpentPunchScale = 0.22f;

        [SerializeField, Min(0f)] private float goldSpentPunchDuration = 0.28f;

        [Header("Wave 3 Retry")]
        [SerializeField] private RectTransform failurePresentationTarget;
        [SerializeField] private TMP_Text failureMessageText;
        [SerializeField] private Image failureDimmer;
        [SerializeField] private Canvas failurePresentationCanvas;
        [SerializeField] private Canvas failureDimmerCanvas;
        [SerializeField, Min(0f)] private float failureMoveDuration = 0.42f;
        [SerializeField, Min(0f)] private float failureHealthDuration = 0.55f;
        [SerializeField, Min(0f)] private float failureMessageRetryDelay = 1f;

        private static readonly Color TutorialInstructionColor = new Color(1f, 0.9f, 0.62f, 1f);

        private const string FailureMessage = "Không đủ hỏa lực rồi, hãy đặt thêm trụ";

        private Tween healthFillTween;
        private Tween healthShakeTween;
        private Tween healthFlashTween;
        private Tween goldTween;
        private Tween goldRefusedFlashTween;
        private Tween goldRefusedPunchTween;
        private Tween goldSpentTextPunchTween;
        private Tween goldSpentIconPunchTween;
        private Color goldBaseColor;
        private int previousHealth;
        private int displayedGold;
        private bool hasRenderedGold;
        private Tween failureMoveTween;
        private Tween failureHealthTween;
        private Tween failureMessageTween;
        private bool hasRenderedHealth;
        private Transform normalParent;
        private int normalSiblingIndex;
        private Vector2 normalAnchorMin;
        private Vector2 normalAnchorMax;
        private Vector2 normalPivot;
        private Vector2 normalAnchoredPosition;
        private bool isShowingWaveThreeDefeat;

        public event System.Action RetryWaveThreeRequested;

        private void Awake()
        {
            if (healthFill == null
                || failurePresentationTarget == null
                || failureMessageText == null
                || failureDimmer == null
                || failurePresentationCanvas == null
                || failureDimmerCanvas == null)
            {
                throw new MissingReferenceException(
                    "LevelStatusHudView requires authored Wave 3 retry references.");
            }

            CaptureNormalLayout();
            HideDamageOverlay();
            if (goldText != null)
            {
                goldBaseColor = goldText.color;
            }
            failureMessageText.gameObject.SetActive(false);
            failureDimmer.gameObject.SetActive(false);
            failurePresentationCanvas.overrideSorting = false;
        }

        /// <summary>
        /// Runs the counter to the new balance rather than snapping it, so the price of a tower
        /// is legible as it is spent.
        /// </summary>
        /// <remarks>
        /// A level's first balance snaps: there is no earlier number for it to count from, and
        /// counting up from zero would read as an award rather than as the starting purse.
        /// </remarks>
        public void RenderGold(int gold)
        {
            goldTween?.Kill();

            // Only a fall pops. Gold arrives constantly - every enemy that dies pays out - and a
            // counter that jumped on each of those would be twitching for the whole wave, which
            // would leave the player nothing to read the one moment they actually spend.
            if (hasRenderedGold && gold < displayedGold)
            {
                PlayGoldSpentPunch();
            }

            if (!hasRenderedGold || goldTweenDuration <= 0f || displayedGold == gold)
            {
                hasRenderedGold = true;
                SetDisplayedGold(gold);
                return;
            }

            goldTween = DOTween.To(
                    () => displayedGold,
                    SetDisplayedGold,
                    gold,
                    goldTweenDuration)
                .SetEase(Ease.OutCubic)
                .SetTarget(this);
        }

        private void SetDisplayedGold(int gold)
        {
            displayedGold = gold;
            goldText.text = gold.ToString("N0");
        }

        /// <summary>
        /// Bumps the coin and the balance when gold is spent.
        /// </summary>
        /// <remarks>
        /// Both, not just the number: the counter is already sliding down on its own, and a
        /// number that is moving anyway does not read as having been acted upon. The coin is the
        /// still thing beside it, so the coin is what makes the spend land.
        ///
        /// Restarted rather than stacked, for the same reason the refusal is: towers get placed
        /// in quick succession and each purchase should restate itself rather than queue behind
        /// the last one or leave the coin parked at a half-swollen size.
        /// </remarks>
        private void PlayGoldSpentPunch()
        {
            if (goldSpentPunchScale <= 0f || goldSpentPunchDuration <= 0f)
            {
                return;
            }

            goldSpentIconPunchTween = RestartPunch(goldSpentIconPunchTween, goldIcon);
            goldSpentTextPunchTween = RestartPunch(
                goldSpentTextPunchTween,
                goldText == null ? null : goldText.rectTransform);
        }

        private Tween RestartPunch(Tween running, RectTransform target)
        {
            running?.Kill();
            if (target == null)
            {
                return null;
            }

            target.localScale = Vector3.one;
            return target
                .DOPunchScale(Vector3.one * goldSpentPunchScale, goldSpentPunchDuration, 8, 0.7f)
                .SetTarget(this);
        }

        public void SetHealthVisible(bool visible)
        {
            // Only the bar hides while the frog is untouched. This used to hide the bar's parent,
            // which is the whole HUD - portrait, gold and all - so entering a level at full health
            // emptied it, and it came back only if a tutorial step happened to re-show the HUD.
            if (healthShakeTarget != null)
            {
                healthShakeTarget.gameObject.SetActive(visible);
            }
        }

        public void RenderHealth(int currentHealth, int maximumHealth)
        {
            if (healthText != null)
            {
                healthText.text = $"{currentHealth}/{maximumHealth}";
            }

            float ratio = maximumHealth <= 0
                ? 0f
                : Mathf.Clamp01((float)currentHealth / maximumHealth);

            // Compared before the guard below so a HUD authored without a fill still tracks the
            // frog's health, instead of reporting the first drop it sees after one as damage.
            bool tookDamage = hasRenderedHealth && currentHealth < previousHealth;
            previousHealth = currentHealth;

            if (healthFill == null)
            {
                hasRenderedHealth = true;
                return;
            }

            // The bar stretches from its left edge: anchorMin stays at 0 and only the right
            // anchor moves, which keeps the sliced end caps at their authored radius.
            RectTransform fill = healthFill.rectTransform;
            Vector2 targetAnchorMax = new Vector2(ratio, 1f);
            healthFillTween?.Kill();
            if (!hasRenderedHealth || healthTweenDuration <= 0f)
            {
                fill.anchorMax = targetAnchorMax;
            }
            else
            {
                healthFillTween = DOTween.To(
                        () => fill.anchorMax,
                        value => fill.anchorMax = value,
                        targetAnchorMax,
                        healthTweenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetTarget(this);
            }

            // Only a real drop plays the damage beat. The old test was "below full", which fired
            // again on every later render while the frog stayed hurt, so a heal or a plain refresh
            // shook the HUD as though it had just been hit.
            if (tookDamage)
            {
                PlayDamageShake();
                PlayDamageFlash();
            }

            hasRenderedHealth = true;
        }

        /// <summary>
        /// Shakes the whole status HUD - portrait, gold and bar together - so the hit registers
        /// even while the player is looking at the board rather than at the bar.
        /// </summary>
        private void PlayDamageShake()
        {
            RectTransform shakeTarget = transform as RectTransform;
            if (shakeTarget == null || healthShakeDuration <= 0f || healthShakeStrength <= 0f)
            {
                return;
            }

            healthShakeTween?.Kill();
            healthShakeTween = shakeTarget
                .DOShakePosition(
                    healthShakeDuration,
                    new Vector2(healthShakeStrength, healthShakeStrength * 0.55f),
                    18,
                    95f,
                    false,
                    true)
                .SetTarget(this);
        }

        /// <summary>
        /// Throws the bar red the instant it is hit, holds the red for the whole drain, then
        /// fades it off.
        /// </summary>
        /// <remarks>
        /// Through an overlay of its own rather than by tinting the fill. Tinting could never
        /// have worked here: the bar's green lives in its sprite, the fill's own colour is plain
        /// white, and Unity's UI shader multiplies the two - so a red tint on a green bar yields
        /// a dark muddy green, which on screen reads as nothing happening at all. That is why
        /// this beat was invisible however far its numbers were pushed.
        ///
        /// The overlay is a child of the fill, so it is exactly as long as the health that is
        /// left and keeps pace as the bar drains.
        ///
        /// The red snaps on rather than easing - a hit does not arrive gradually - and only the
        /// fade off is tweened, delayed until the bar has finished falling so the colour outlasts
        /// the movement it belongs to.
        ///
        /// Driven through DOTween.To rather than Image.DOFade: the shortcut lives in
        /// DOTweenModuleUI, which this assembly does not reference, and the rest of this HUD
        /// already tweens colour the same way.
        /// </remarks>
        private void PlayDamageFlash()
        {
            if (healthDamageOverlay == null)
            {
                return;
            }

            healthFlashTween?.Kill();
            healthDamageOverlay.color = healthDamageFlashColor;

            if (healthFlashFadeBackDuration <= 0f)
            {
                HideDamageOverlay();
                return;
            }

            Color faded = healthDamageFlashColor;
            faded.a = 0f;
            healthFlashTween = DOTween.To(
                    () => healthDamageOverlay.color,
                    value => healthDamageOverlay.color = value,
                    faded,
                    healthFlashFadeBackDuration)
                .SetDelay(healthTweenDuration)
                .SetEase(Ease.InOutSine)
                .SetTarget(this)
                .OnKill(HideDamageOverlay);
        }

        private void HideDamageOverlay()
        {
            healthFlashTween = null;
            if (healthDamageOverlay != null)
            {
                Color hidden = healthDamageFlashColor;
                hidden.a = 0f;
                healthDamageOverlay.color = hidden;
            }
        }

        /// <summary>
        /// Shakes the status HUD and throws the balance red for a moment, because a purchase the
        /// player cannot afford leaves the number unchanged and so has nothing else to show them.
        /// </summary>
        /// <remarks>
        /// The same shake damage uses. Two different refusals reading as the same nudge is the
        /// point: it means "the HUD is answering you", and the red on the balance says which part
        /// of the HUD to look at.
        /// </remarks>
        public void PlayPurchaseRefusedFeedback()
        {
            PlayDamageShake();
            PlayGoldRefusedFlash();
        }

        private void PlayGoldRefusedFlash()
        {
            if (goldText == null)
            {
                return;
            }

            // Restarted rather than stacked. Players tap a card they cannot afford several times
            // in a row, and each tap should restate the answer rather than queue another one
            // behind it or settle the number on a half-red colour.
            goldRefusedFlashTween?.Kill();
            goldRefusedPunchTween?.Kill();
            RectTransform goldRect = goldText.rectTransform;
            goldRect.localScale = Vector3.one;
            goldText.color = goldBaseColor;

            if (goldRefusedFlashDuration > 0f)
            {
                goldRefusedFlashTween = DOTween.To(
                        () => goldText.color,
                        value => goldText.color = value,
                        goldRefusedFlashColor,
                        goldRefusedFlashDuration * 0.5f)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine)
                    .SetTarget(this)
                    .OnKill(RestoreGoldColor);
            }

            if (goldRefusedPunchScale > 0f && goldRefusedFlashDuration > 0f)
            {
                goldRefusedPunchTween = goldRect
                    .DOPunchScale(
                        Vector3.one * goldRefusedPunchScale,
                        goldRefusedFlashDuration,
                        6,
                        0.6f)
                    .SetTarget(this)
                    .OnKill(RestoreGoldScale);
            }
        }

        private void RestoreGoldColor()
        {
            goldRefusedFlashTween = null;
            if (goldText != null)
            {
                goldText.color = goldBaseColor;
            }
        }

        private void RestoreGoldScale()
        {
            goldRefusedPunchTween = null;
            if (goldText != null)
            {
                goldText.rectTransform.localScale = Vector3.one;
            }
        }

        public void ShowWaveThreeDefeat()
        {
            if (isShowingWaveThreeDefeat)
            {
                return;
            }

            isShowingWaveThreeDefeat = true;
            SetHealthVisible(true);
            failureMessageText.gameObject.SetActive(false);
            failureDimmer.gameObject.SetActive(true);
            failurePresentationTarget.gameObject.SetActive(true);
            MoveToFailurePresentation();
            failureDimmerCanvas.overrideSorting = true;
            failureDimmerCanvas.sortingOrder = 90;
            failurePresentationCanvas.overrideSorting = true;
            failurePresentationCanvas.sortingOrder = 91;
            healthFillTween?.Kill();
            healthShakeTween?.Kill();
            healthFlashTween?.Kill();
            failureMoveTween?.Kill();
            failureHealthTween?.Kill();
            failureMessageTween?.Kill();

            RectTransform status = transform as RectTransform;
            failureMoveTween = DOTween.To(
                    () => status.anchoredPosition,
                    value => status.anchoredPosition = value,
                    new Vector2(0f, 50f),
                    failureMoveDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(DrainWaveThreeFailureHealth);
        }

        private void DrainWaveThreeFailureHealth()
        {
            failureHealthTween = DOTween.To(
                    () => healthFill.rectTransform.anchorMax,
                    value => healthFill.rectTransform.anchorMax = value,
                    new Vector2(0f, 1f),
                    failureHealthDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(PlayFailureMessage);
        }

        private void PlayFailureMessage()
        {
            failureMessageTween?.Kill();
            failureMessageText.gameObject.SetActive(true);

            // The same warm off-gold the tutorial types in. This line is typed out under the same
            // circumstances and used to be authored near-white, so the two running lines read as
            // coming from two different voices.
            Color color = TutorialInstructionColor;
            color.a = 0f;
            failureMessageText.color = color;
            failureMessageText.text = string.Empty;

            int visibleCharacters = 0;
            float duration = Mathf.Clamp(FailureMessage.Length * 0.035f, 0.4f, 1.6f);
            Sequence sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            sequence.Append(DOTween.To(
                    () => failureMessageText.color.a,
                    alpha => { Color next = failureMessageText.color; next.a = alpha; failureMessageText.color = next; },
                    1f, 0.2f)
                .SetEase(Ease.OutSine));
            sequence.Append(DOTween.To(
                    () => visibleCharacters,
                    count => { visibleCharacters = count; failureMessageText.text = FailureMessage.Substring(0, count); },
                    FailureMessage.Length, duration)
                .SetEase(Ease.Linear));
            sequence.AppendInterval(failureMessageRetryDelay);
            sequence.OnComplete(() => RetryWaveThreeRequested?.Invoke());
            failureMessageTween = sequence;
        }

        /// <summary>
        /// Puts the HUD back the way it was before the Wave 3 defeat took it over.
        /// </summary>
        /// <remarks>
        /// Guarded on the flag, because the presenter calls this on every refresh for as long as
        /// no retry is being offered - which is nearly every frame of an ordinary wave. The body
        /// below clears the health render state, so running it unguarded left
        /// <see cref="hasRenderedHealth"/> false whenever a hit landed: every drop read as the
        /// first render of the bar rather than as damage, which snapped the bar instead of
        /// draining it and swallowed the shake and the red flash entirely.
        /// </remarks>
        public void HideWaveThreeDefeat()
        {
            if (!isShowingWaveThreeDefeat)
            {
                return;
            }

            isShowingWaveThreeDefeat = false;
            failureMoveTween?.Kill();
            failureHealthTween?.Kill();
            failureMessageTween?.Kill();
            if (failureMessageText != null) failureMessageText.gameObject.SetActive(false);
            if (failureDimmer != null) failureDimmer.gameObject.SetActive(false);
            if (failurePresentationCanvas != null) failurePresentationCanvas.overrideSorting = false;
            RestoreNormalLayout();
            if (failurePresentationTarget != null) failurePresentationTarget.gameObject.SetActive(false);

            healthFlashTween?.Kill();
            hasRenderedHealth = false;
            previousHealth = 0;
        }

        private void CaptureNormalLayout()
        {
            RectTransform status = transform as RectTransform;
            normalParent = status.parent;
            normalSiblingIndex = status.GetSiblingIndex();
            normalAnchorMin = status.anchorMin;
            normalAnchorMax = status.anchorMax;
            normalPivot = status.pivot;
            normalAnchoredPosition = status.anchoredPosition;
        }

        private void MoveToFailurePresentation()
        {
            RectTransform status = transform as RectTransform;
            Vector2 startPosition = failurePresentationTarget.InverseTransformPoint(status.position);
            status.SetParent(failurePresentationTarget, false);
            status.anchorMin = new Vector2(0.5f, 0.5f);
            status.anchorMax = new Vector2(0.5f, 0.5f);
            status.pivot = new Vector2(0.5f, 0.5f);
            status.anchoredPosition = startPosition;
        }

        private void RestoreNormalLayout()
        {
            if (normalParent == null)
            {
                return;
            }

            RectTransform status = transform as RectTransform;
            status.SetParent(normalParent, false);
            status.SetSiblingIndex(normalSiblingIndex);
            status.anchorMin = normalAnchorMin;
            status.anchorMax = normalAnchorMax;
            status.pivot = normalPivot;
            status.anchoredPosition = normalAnchoredPosition;
        }

        private void OnDisable()
        {
            healthFillTween?.Kill();
            healthShakeTween?.Kill();
            healthFlashTween?.Kill();
            goldTween?.Kill();
            goldRefusedFlashTween?.Kill();
            goldRefusedPunchTween?.Kill();
            healthFillTween = null;
            healthShakeTween = null;
            healthFlashTween = null;
            goldTween = null;
            goldRefusedFlashTween = null;
            goldRefusedPunchTween = null;
            failureMoveTween?.Kill();
            failureHealthTween?.Kill();
            failureMessageTween?.Kill();
            failureMoveTween = null;
            failureHealthTween = null;
            failureMessageTween = null;
        }
    }
}
