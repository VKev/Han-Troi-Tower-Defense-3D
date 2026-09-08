using System;
using System.Collections.Generic;
using DG.Tweening;
using TowerDefense3D.Tutorials;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense3D.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TutorialOverlayView : MonoBehaviour, ITutorialOverlay
    {
        private static readonly int FocusCenterId = Shader.PropertyToID("_FocusCenter");
        private static readonly int FocusSizeId = Shader.PropertyToID("_FocusSize");
        private static readonly int FocusCenter2Id = Shader.PropertyToID("_FocusCenter2");
        private static readonly int FocusSize2Id = Shader.PropertyToID("_FocusSize2");
        private static readonly int FocusCountId = Shader.PropertyToID("_FocusCount");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int GlowEnabledId = Shader.PropertyToID("_GlowEnabled");

        [SerializeField] private Image dimmer;
        [SerializeField] private Text instruction;
        [SerializeField] private Font instructionFont;
        [SerializeField] private TutorialHandView hand;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Canvas cameraCanvas;
        [SerializeField, Range(0f, 1f)] private float darkness = 0.72f;
        [SerializeField, Range(0.02f, 0.5f)] private float edgeSoftness = 0.2f;
        [SerializeField, Min(0f)] private float focusPadding = 28f;
        [SerializeField] private Vector2 frogFocusOffset = new Vector2(-45f, 0f);

        private Material dimmerMaterial;
        private Tween handTween;
        private Tween overlayFade;
        private Tween instructionTween;
        private string instructionValue;
        private TutorialSoftMaskRaycastFilter raycastFilter;

        public bool IsInstructionComplete { get; private set; }

        private void Awake()
        {
            EnsureParts();
            ConfigureCameraCanvas();
            Hide();
        }

        private void Start()
        {
            // The application overlay is created before the level camera. Re-apply the camera
            // binding after scene load so the tutorial stays above gameplay UI and receives bloom.
            ConfigureCameraCanvas();
        }

        public void Show(TutorialStep step, TutorialContext context)
        {
            EnsureParts();
            ConfigureCameraCanvas();
            Rect[] areas = GetFocusRects(step.TargetId, context);
            for (int index = 0; index < areas.Length; index++) areas[index] = Expand(areas[index], focusPadding);
            if (step.Id == "protect_frog")
            {
                for (int index = 0; index < areas.Length; index++)
                {
                    areas[index].position += frogFocusOffset;
                }
            }
            float glowAlpha = step.Id == "link_generator_to_nexus" ? 0.35f : 0f;
            ApplySpotlights(areas, glowAlpha);
            Rect instructionArea = step.Id == "highlight_next_enemy" ? areas[areas.Length - 1] : GetCombinedRect(areas);
            ApplyInstruction(instructionArea, step);
            bool wasHidden = canvasGroup.alpha <= 0.01f;
            overlayFade?.Kill();
            if (wasHidden)
            {
                canvasGroup.alpha = 0f;
                overlayFade = DOTween.To(
                        () => canvasGroup.alpha,
                        value => canvasGroup.alpha = value,
                        1f,
                        0.42f)
                    .SetEase(Ease.InOutSine)
                    .SetTarget(this);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            handTween?.Kill();
            bool hideHand = step.Id == "highlight_next_enemy" || step.Id == "protect_frog";
            if (hand != null)
            {
                hand.gameObject.SetActive(!hideHand);
            }

            handTween = hideHand || hand == null ? null : hand.Play(step, areas);
        }

        public void Hide()
        {
            handTween?.Kill();
            overlayFade?.Kill();
            instructionTween?.Kill();
            handTween = null;
            IsInstructionComplete = true;
            if (hand != null) hand.gameObject.SetActive(false);
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        public void CompleteInstruction()
        {
            if (IsInstructionComplete) return;
            instructionTween?.Kill();
            instruction.text = instructionValue;
            Color color = instruction.color;
            color.a = 1f;
            instruction.color = color;
            IsInstructionComplete = true;
        }

        public void SetPaused(bool paused)
        {
            if (handTween == null) return;
            if (paused) handTween.Pause(); else handTween.Play();
        }

        private void ApplySpotlights(Rect[] areas, float glowAlpha)
        {
            Rect first = areas[0];
            Rect second = areas.Length > 1 ? areas[1] : first;
            dimmerMaterial.SetVector(FocusCenterId, ToNormalizedCenter(first));
            dimmerMaterial.SetVector(FocusSizeId, ToNormalizedSize(first));
            dimmerMaterial.SetVector(FocusCenter2Id, ToNormalizedCenter(second));
            dimmerMaterial.SetVector(FocusSize2Id, ToNormalizedSize(second));
            dimmerMaterial.SetFloat(FocusCountId, areas.Length);
            dimmerMaterial.SetFloat(SoftnessId, edgeSoftness);
            dimmerMaterial.SetColor(GlowColorId, new Color(2.8f, 2.8f, 2.8f, glowAlpha));
            dimmerMaterial.SetFloat(GlowWidthId, 0.012f);
            dimmerMaterial.SetFloat(GlowEnabledId, glowAlpha > 0f ? 1f : 0f);
            dimmer.color = new Color(0f, 0f, 0f, darkness);
            raycastFilter.SetFocus(areas);
        }

        private static Vector2 ToNormalizedCenter(Rect area) => new Vector2(
            area.center.x / Screen.width, area.center.y / Screen.height);

        private static Vector2 ToNormalizedSize(Rect area) => new Vector2(
            area.width / Screen.width, area.height / Screen.height);

        private void ApplyInstruction(Rect area, TutorialStep step)
        {
            instruction.gameObject.SetActive(!string.IsNullOrWhiteSpace(step.Instruction));
            if (string.IsNullOrWhiteSpace(step.Instruction))
            {
                IsInstructionComplete = true;
                return;
            }

            RectTransform label = instruction.rectTransform;
            bool isFrogInstruction = step.Id == "protect_frog";
            bool placeBesideTarget = step.Id == "highlight_next_enemy";
            label.sizeDelta = isFrogInstruction
                ? new Vector2(Mathf.Min(480f, Screen.width * 0.42f), 120f)
                : new Vector2(Mathf.Min(620f, Screen.width * 0.72f), 84f);
            instruction.fontStyle = placeBesideTarget || isFrogInstruction ? FontStyle.Bold : FontStyle.Normal;
            instruction.alignment = placeBesideTarget
                ? TextAnchor.MiddleLeft
                : TextAnchor.MiddleCenter;
            instruction.color = placeBesideTarget || isFrogInstruction
                ? new Color(1f, 0.9f, 0.62f, 1f)
                : Color.white;
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            instruction.verticalOverflow = VerticalWrapMode.Overflow;

            Rect safe = Screen.safeArea;
            float halfWidth = label.sizeDelta.x * 0.5f;
            float halfHeight = label.sizeDelta.y * 0.5f;
            float x;
            float y;
            if (placeBesideTarget)
            {
                float gap = 32f;
                label.pivot = new Vector2(0f, 0.5f);
                x = area.xMax + gap;
                if (x + label.sizeDelta.x > safe.xMax)
                {
                    label.pivot = new Vector2(1f, 0.5f);
                    x = area.xMin - gap;
                }

                float leftBound = label.pivot.x == 0f ? safe.xMin : safe.xMin + label.sizeDelta.x;
                float rightBound = label.pivot.x == 0f ? safe.xMax - label.sizeDelta.x : safe.xMax;
                x = Mathf.Clamp(x, leftBound, rightBound);
                y = Mathf.Clamp(area.center.y, safe.yMin + halfHeight, safe.yMax - halfHeight);
            }
            else if (isFrogInstruction)
            {
                label.pivot = new Vector2(0.5f, 0.5f);
                x = Mathf.Clamp(area.center.x, safe.xMin + halfWidth, safe.xMax - halfWidth);
                float below = area.yMin - halfHeight - 28f;
                float above = area.yMax + halfHeight + 28f;
                y = below >= safe.yMin + halfHeight ? below : above;
                y = Mathf.Clamp(y, safe.yMin + halfHeight, safe.yMax - halfHeight);
            }
            else
            {
                label.pivot = new Vector2(0.5f, 0.5f);
                x = area.center.x;
                y = area.yMin > Screen.height * 0.32f ? area.yMin - 76f : area.yMax + 76f;
                x = Mathf.Clamp(x, safe.xMin + halfWidth, safe.xMax - halfWidth);
                y = Mathf.Clamp(y, safe.yMin + halfHeight, safe.yMax - halfHeight);
            }

            label.position = new Vector3(x, y, 0f);
            PlayInstruction(step.Instruction);
        }

        private void PlayInstruction(string value)
        {
            instructionTween?.Kill();
            instructionValue = value;
            IsInstructionComplete = false;
            Color color = instruction.color;
            color.a = 0f;
            instruction.color = color;
            instruction.text = string.Empty;

            int visibleCharacters = 0;
            float duration = Mathf.Clamp(value.Length * 0.035f, 0.4f, 1.6f);
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.Append(DOTween.To(
                    () => instruction.color.a,
                    alpha =>
                    {
                        Color next = instruction.color;
                        next.a = alpha;
                        instruction.color = next;
                    },
                    1f,
                    0.2f)
                .SetEase(Ease.OutSine));
            sequence.Append(DOTween.To(
                    () => visibleCharacters,
                    count =>
                    {
                        visibleCharacters = count;
                        instruction.text = value.Substring(0, count);
                    },
                    value.Length,
                    duration)
                .SetEase(Ease.Linear));
            sequence.OnComplete(() => IsInstructionComplete = true);
            instructionTween = sequence;
        }

        private void EnsureParts()
        {
            canvasGroup = canvasGroup != null ? canvasGroup
                : GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            cameraCanvas = GetComponent<Canvas>() ?? gameObject.AddComponent<Canvas>();
            dimmer = EnsureImage(dimmer, "Soft Dimmer");
            dimmer.rectTransform.SetAsFirstSibling();
            Stretch(dimmer.rectTransform);
            raycastFilter = dimmer.GetComponent<TutorialSoftMaskRaycastFilter>()
                ?? dimmer.gameObject.AddComponent<TutorialSoftMaskRaycastFilter>();
            if (dimmerMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/TutorialSoftMask");
                if (shader == null) throw new InvalidOperationException("Missing TutorialSoftMask shader.");
                dimmerMaterial = new Material(shader) { name = "Tutorial Soft Mask (Runtime)" };
                dimmer.material = dimmerMaterial;
            }

            if (instruction != null)
            {
                Shadow shadow = instruction.GetComponent<Shadow>();
                Outline outline = instruction.GetComponent<Outline>();
                if (shadow != null) Destroy(shadow);
                if (outline != null) Destroy(outline);
                return;
            }
            GameObject child = new GameObject("Instruction", typeof(RectTransform), typeof(Text));
            child.transform.SetParent(transform, false);
            instruction = child.GetComponent<Text>();
            instruction.alignment = TextAnchor.MiddleCenter;
            instruction.color = Color.white;
            instruction.font = instructionFont != null
                ? instructionFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instruction.fontSize = 28;
            instruction.resizeTextForBestFit = true;
            instruction.resizeTextMinSize = 18;
            instruction.resizeTextMaxSize = 30;
            instruction.raycastTarget = false;
        }

        private void ConfigureCameraCanvas()
        {
            Camera camera = Camera.main;
            if (camera == null || cameraCanvas == null) return;
            cameraCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            cameraCanvas.worldCamera = camera;
            cameraCanvas.planeDistance = 1f;
            cameraCanvas.overrideSorting = true;
            cameraCanvas.sortingOrder = 200;
        }

        private Image EnsureImage(Image value, string childName)
        {
            if (value != null) return value;
            Transform existing = transform.Find(childName);
            if (existing != null && existing.TryGetComponent(out Image existingImage)) return existingImage;
            GameObject child = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(transform, false);
            return child.GetComponent<Image>();
        }

        private static void Stretch(RectTransform target)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = target.offsetMax = Vector2.zero;
        }

        private static Rect Expand(Rect area, float padding)
        {
            area.xMin -= padding; area.xMax += padding;
            area.yMin -= padding; area.yMax += padding;
            return area;
        }

        private static Rect GetScreenRect(RectTransform target)
        {
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static Rect[] GetFocusRects(string targetIds, TutorialContext context)
        {
            var areas = new List<Rect>(2);
            foreach (string rawId in targetIds.Split(','))
            {
                object target = context.GetTarget(rawId.Trim());
                Rect area = target is RectTransform rect ? GetScreenRect(rect)
                    : target is Transform world ? GetWorldScreenRect(world) : default;
                if (area.width <= 0f || area.height <= 0f) continue;
                areas.Add(area);
                if (areas.Count == 2) break;
            }

            if (areas.Count == 0) areas.Add(new Rect(
                Screen.width * 0.4f, Screen.height * 0.4f,
                Screen.width * 0.2f, Screen.height * 0.2f));
            return areas.ToArray();
        }

        private static Rect GetWorldScreenRect(Transform target)
        {
            Camera camera = Camera.main;
            if (camera == null) return default;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null) continue;
                if (hasBounds) bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }

            if (hasBounds && TryProjectBounds(camera, bounds, out Rect projected)) return projected;

            Vector3 point = camera.WorldToScreenPoint(target.position + Vector3.up);
            float size = Mathf.Clamp(Screen.height * 0.15f, 120f, 220f);
            return new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
        }

        private static bool TryProjectBounds(Camera camera, Bounds bounds, out Rect result)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            int visibleCorners = 0;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 point = camera.WorldToScreenPoint(center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)));
                        if (point.z <= 0f) continue;
                        minX = Mathf.Min(minX, point.x);
                        minY = Mathf.Min(minY, point.y);
                        maxX = Mathf.Max(maxX, point.x);
                        maxY = Mathf.Max(maxY, point.y);
                        visibleCorners++;
                    }
                }
            }

            result = visibleCorners == 0 ? default : Rect.MinMaxRect(minX, minY, maxX, maxY);
            return visibleCorners > 0 && result.width > 0f && result.height > 0f;
        }

        private static Rect GetCombinedRect(Rect[] areas)
        {
            Rect combined = areas[0];
            for (int index = 1; index < areas.Length; index++)
            {
                Rect area = areas[index];
                combined = Rect.MinMaxRect(
                    Math.Min(combined.xMin, area.xMin), Math.Min(combined.yMin, area.yMin),
                    Math.Max(combined.xMax, area.xMax), Math.Max(combined.yMax, area.yMax));
            }

            return combined;
        }

        private void OnDestroy()
        {
            handTween?.Kill();
            overlayFade?.Kill();
            instructionTween?.Kill();
            if (dimmerMaterial != null) Destroy(dimmerMaterial);
        }
    }
}
