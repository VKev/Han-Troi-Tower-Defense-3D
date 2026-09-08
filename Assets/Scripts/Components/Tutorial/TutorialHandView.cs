using DG.Tweening;
using TowerDefense3D.Tutorials;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    public sealed class TutorialHandView : MonoBehaviour
    {
        [SerializeField] private Image image;

        public Tween Play(TutorialStep step, Rect[] targets)
        {
            if (image == null) image = GetComponent<Image>();
            if (image == null) return null;

            gameObject.SetActive(true);
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
            if (step.ActionId == "link_towers" && targets.Length > 1)
            {
                Vector2 from = targets[0].center + fingerOffset;
                Vector2 to = targets[1].center + fingerOffset;
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

        private void OnDisable()
        {
            transform.DOKill();
        }
    }
}
