using DG.Tweening;
using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Authored root boundary for the level-owned gameplay HUD.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GameplayUIView : MonoBehaviour, IGameplayUIView
    {
        private const float TutorialFocusFadeSeconds = 0.25f;

        [SerializeField] private CanvasGroup canvasGroup;
        private Tween focusFade;

        public bool IsVisible => gameObject.activeSelf && GetCanvasGroup().alpha > 0.01f;

        public void Show()
        {
            // Teardown destroys this HUD in no fixed order relative to the level systems that
            // still hold it, and those hold it as an interface, where a destroyed component does
            // not compare equal to null - so the check has to happen on this side.
            if (this == null)
            {
                return;
            }

            gameObject.SetActive(true);
            focusFade?.Kill();
            CanvasGroup group = GetCanvasGroup();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        public void SetTutorialFocusVisible(bool visible)
        {
            // Teardown destroys this HUD in no fixed order relative to the level systems that
            // still hold it, and those hold it as an interface, where a destroyed component does
            // not compare equal to null - so the check has to happen on this side.
            if (this == null)
            {
                return;
            }

            gameObject.SetActive(true);
            focusFade?.Kill();
            CanvasGroup group = GetCanvasGroup();
            group.interactable = false;
            group.blocksRaycasts = false;
            focusFade = DOTween.To(
                    () => group.alpha,
                    value => group.alpha = value,
                    visible ? 1f : 0f,
                    TutorialFocusFadeSeconds)
                .SetEase(Ease.InOutSine)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    group.interactable = visible;
                    group.blocksRaycasts = visible;
                });
        }

        private CanvasGroup GetCanvasGroup()
        {
            if (canvasGroup == null)
            {
                throw new MissingComponentException(
                    "GameplayUIView requires an authored CanvasGroup on its root.");
            }

            return canvasGroup;
        }

        private void OnDestroy()
        {
            focusFade?.Kill();
        }
    }
}
