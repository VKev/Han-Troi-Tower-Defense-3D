using DG.Tweening;
using TowerDefense3D.Tutorials;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    public sealed class TutorialHandView : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private Sprite zoomHandSprite;
        [SerializeField] private Sprite zoomUpArrowSprite;
        [SerializeField] private Sprite zoomDownArrowSprite;

        private Sprite defaultSprite;
        private Image zoomUpArrow;
        private Image zoomDownArrow;

        public Tween Play(TutorialStep step, Rect[] targets)
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null) return null;

            if (step.ActionId == "zoom_camera") return PlayZoomGuide();

            ResetZoomGuide();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            image.raycastTarget = false;
            RectTransform rect = transform as RectTransform;
            image.preserveAspect = true;
            rect.sizeDelta = Vector2.one * Mathf.Clamp(Screen.height * 0.19f, 150f, 230f);
            float scale = Screen.height / 1080f;
            Vector2 fingerOffset = new Vector2(42f, -46f) * scale;
            Rect first = targets[0];
            Vector2 start = first.center + fingerOffset;
            rect.position = start;
            rect.localScale = Vector3.zero;
            transform.DOKill();
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.AppendInterval(0.34f);
            sequence.Append(rect.DOScale(1f, 0.24f).SetEase(Ease.OutBack));
            if ((step.ActionId == "link_towers"
                    || step.ActionId == "place_generator"
                    || step.ActionId == "place_sink"
                    || step.ActionId == "place_water"
                    || step.ActionId == "place_fire")
                && targets.Length > 1)
            {
                Vector2 from = targets[0].center + fingerOffset;
                Vector2 to = targets[1].center + fingerOffset;
                if (step.ActionId == "place_generator"
                    || step.ActionId == "place_sink"
                    || step.ActionId == "place_water"
                    || step.ActionId == "place_fire")
                {
                    // GridPlacementView samples above and left of a finger. Move the displayed
                    // hand by the inverse amount so its sampled footprint lands on the lit cells.
                    to += new Vector2(0.04f, -0.10f) * Screen.height;
                }

                rect.position = from;
                sequence.Append(rect.DOScale(0.88f, 0.14f));
                sequence.AppendInterval(0.12f);
                sequence.Append(rect.DOMove(to, 0.85f).SetEase(Ease.InOutSine));
                sequence.Append(rect.DOScale(1f, 0.16f));
                sequence.AppendInterval(0.35f);
            }
            else
            {
                sequence.Append(rect.DOMove(first.center + fingerOffset, 0.25f).SetEase(Ease.OutQuad));
                sequence.Append(rect.DOScale(0.82f, 0.14f));
                sequence.Append(rect.DOScale(1f, 0.18f));
                sequence.AppendInterval(0.45f);
            }
            sequence.SetLoops(-1, LoopType.Restart);
            return sequence;
        }

        private Tween PlayZoomGuide()
        {
            if (zoomHandSprite == null || zoomUpArrowSprite == null || zoomDownArrowSprite == null)
            {
                return null;
            }

            defaultSprite ??= image.sprite;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            image.raycastTarget = false;
            image.sprite = zoomHandSprite;
            image.preserveAspect = true;
            RectTransform rect = transform as RectTransform;
            rect.position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            rect.sizeDelta = Vector2.one * Mathf.Clamp(Screen.height * 0.25f, 220f, 340f);
            rect.localScale = Vector3.one;
            EnsureZoomArrows();
            float arrowX = rect.sizeDelta.x * 0.55f;
            Vector2 upBase = new Vector2(arrowX, rect.sizeDelta.y * 0.22f);
            Vector2 downBase = new Vector2(arrowX, -rect.sizeDelta.y * 0.22f);
            zoomUpArrow.rectTransform.anchoredPosition = upBase;
            zoomDownArrow.rectTransform.anchoredPosition = downBase;
            zoomUpArrow.gameObject.SetActive(true);
            zoomDownArrow.gameObject.SetActive(true);
            float travel = rect.sizeDelta.y * 0.12f;
            Sequence sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
            sequence.Append(TweenAnchorY(zoomUpArrow.rectTransform, upBase.y + travel));
            sequence.Join(TweenAnchorY(zoomDownArrow.rectTransform, downBase.y - travel));
            sequence.Join(rect.DOScale(1.1f, 0.45f));
            sequence.Append(TweenAnchorY(zoomUpArrow.rectTransform, upBase.y));
            sequence.Join(TweenAnchorY(zoomDownArrow.rectTransform, downBase.y));
            sequence.Join(rect.DOScale(0.9f, 0.45f));
            sequence.SetLoops(-1, LoopType.Restart);
            return sequence;
        }

        private static Tween TweenAnchorY(RectTransform target, float y)
        {
            Vector2 end = new Vector2(target.anchoredPosition.x, y);
            return DOTween.To(
                () => target.anchoredPosition,
                value => target.anchoredPosition = value,
                end,
                0.45f);
        }

        private void EnsureZoomArrows()
        {
            zoomUpArrow ??= CreateZoomArrow("Zoom Up Arrow", zoomUpArrowSprite);
            zoomDownArrow ??= CreateZoomArrow("Zoom Down Arrow", zoomDownArrowSprite);
        }

        private Image CreateZoomArrow(string name, Sprite sprite)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            owner.transform.SetParent(transform, false);
            var arrow = owner.GetComponent<Image>();
            arrow.sprite = sprite;
            arrow.preserveAspect = true;
            arrow.raycastTarget = false;
            arrow.rectTransform.sizeDelta = new Vector2(58f, 76f);
            return arrow;
        }

        private void ResetZoomGuide()
        {
            if (defaultSprite != null) image.sprite = defaultSprite;
            if (zoomUpArrow != null) zoomUpArrow.gameObject.SetActive(false);
            if (zoomDownArrow != null) zoomDownArrow.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            transform.DOKill();
            ResetZoomGuide();
        }
    }
}
