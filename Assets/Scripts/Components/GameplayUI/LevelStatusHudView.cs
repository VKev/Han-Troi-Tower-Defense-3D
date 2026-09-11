using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class LevelStatusHudView : MonoBehaviour, ILevelStatusHudView, IWaveThreeDefeatHudView
    {
        [SerializeField] private Text goldText;

        [Tooltip("Optional. The frog's bar carries the health by its length, so the numbers are left off unless a design asks for them; wire a Text here and they come back.")]
        [SerializeField] private Text healthText;

        [Tooltip("The green bar. It is a 9-sliced capsule stretched from its left edge, so its length is driven by the rect rather than by Image.fillAmount - a filled image ignores sprite borders and would flatten the rounded end caps.")]
        [SerializeField] private Image healthFill;

        [Tooltip("Optional shake target for damage feedback. Defaults to this HUD root.")]
        [SerializeField] private RectTransform healthShakeTarget;

        [SerializeField, Min(0f)] private float healthTweenDuration = 0.32f;
        [SerializeField, Min(0f)] private float healthShakeDuration = 0.38f;
        [SerializeField, Min(0f)] private float healthShakeStrength = 9f;

        [Header("Wave 3 Retry")]
        [SerializeField] private RectTransform failurePresentationTarget;
        [SerializeField] private Text failureMessageText;
        [SerializeField] private Image failureDimmer;
        [SerializeField] private Canvas failurePresentationCanvas;
        [SerializeField] private Canvas failureDimmerCanvas;
        [SerializeField, Min(0f)] private float failureMoveDuration = 0.42f;
        [SerializeField, Min(0f)] private float failureHealthDuration = 0.55f;
        [SerializeField, Min(0f)] private float failureMessageRetryDelay = 1f;

        private const string FailureMessage = "Không đủ hỏa lực rồi, hãy đặt thêm trụ";

        private Tween healthFillTween;
        private Tween healthShakeTween;
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
            failureMessageText.gameObject.SetActive(false);
            failureDimmer.gameObject.SetActive(false);
            failurePresentationCanvas.overrideSorting = false;
        }

        public void RenderGold(int gold)
        {
            goldText.text = gold.ToString("N0");
        }

        public void SetHealthVisible(bool visible)
        {
            Transform healthRow = healthShakeTarget != null
                ? healthShakeTarget.parent
                : transform.Find("Health Row");
            if (healthRow != null)
            {
                healthRow.gameObject.SetActive(visible);
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

            if (healthFill == null)
            {
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

            if (hasRenderedHealth && currentHealth < maximumHealth)
            {
                RectTransform shakeTarget = transform as RectTransform;
                if (shakeTarget != null && healthShakeDuration > 0f && healthShakeStrength > 0f)
                {
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
            }

            hasRenderedHealth = true;
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
            Color color = failureMessageText.color;
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

        public void HideWaveThreeDefeat()
        {
            isShowingWaveThreeDefeat = false;
            failureMoveTween?.Kill();
            failureHealthTween?.Kill();
            failureMessageTween?.Kill();
            if (failureMessageText != null) failureMessageText.gameObject.SetActive(false);
            if (failureDimmer != null) failureDimmer.gameObject.SetActive(false);
            if (failurePresentationCanvas != null) failurePresentationCanvas.overrideSorting = false;
            RestoreNormalLayout();
            if (failurePresentationTarget != null) failurePresentationTarget.gameObject.SetActive(false);

            hasRenderedHealth = false;
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
            healthFillTween = null;
            healthShakeTween = null;
            failureMoveTween?.Kill();
            failureHealthTween?.Kill();
            failureMessageTween?.Kill();
            failureMoveTween = null;
            failureHealthTween = null;
            failureMessageTween = null;
        }
    }
}
