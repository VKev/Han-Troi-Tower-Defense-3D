using System;
using System.Collections.Generic;
using DG.Tweening;
using TowerDefense3D.Audio;
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
        [SerializeField, Range(0f, 1f)] private float darkness = 0.72f;
        [SerializeField, Range(0.02f, 0.5f)] private float edgeSoftness = 0.2f;
        [SerializeField, Min(0f)] private float focusPadding = 28f;
        [SerializeField] private Vector2 frogFocusOffset = new Vector2(-45f, 0f);

        private Material dimmerMaterial;
        private Tween handTween;
        private Tween overlayFade;
        private Tween instructionTween;
        private string instructionValue;
        private TutorialStep pendingHandStep;
        private Rect[] pendingHandAreas;
        private TutorialSoftMaskRaycastFilter raycastFilter;
        private Canvas cameraCanvas;
        private ISoundPlayer soundPlayer;

        public event Action<bool> BlackOverlayVisibilityChanged;

        public bool IsInstructionComplete { get; private set; }
        public bool IsBlackOverlayVisible { get; private set; }

        public void Initialize(ISoundPlayer player)
        {
            soundPlayer = player;
        }

        private void Awake()
        {
            EnsureParts();
            ConfigureCameraCanvas();
            Hide();
        }

        public void Show(TutorialStep step, TutorialContext context)
        {
            EnsureParts();
            Rect[] areas = GetFocusRects(step.TargetId, context);
            float padding = step.Id == "wave_two_warning" || step.Id == "place_generator"
                || step.Id == "fire_tower_intro"
                ? 12f
                : focusPadding;
            for (int index = 0; index < areas.Length; index++) areas[index] = Expand(areas[index], padding);
            if (step.Id == "protect_frog")
            {
                for (int index = 0; index < areas.Length; index++)
                {
                    areas[index].position += frogFocusOffset;
                }
            }
            float glowAlpha = step.Id == "link_generator_to_nexus" ? 0.35f : 0f;
            bool isPlacementDrag = step.ActionId == "place_generator"
                || step.ActionId == "place_sink"
                || step.ActionId == "place_water"
                || step.ActionId == "place_fire";
            if (string.IsNullOrWhiteSpace(step.TargetId))
            {
                ClearSpotlights();
                if (step.Id == "link_new_generator_to_nexus"
                    || step.Id == "water_tower_hint")
                {
                    dimmer.color = Color.clear;
                }
            }
            else
            {
                ApplySpotlights(areas, glowAlpha, !isPlacementDrag);
            }
            if (!step.KeepInstructionVisible)
            {
                Rect instructionArea = GetInstructionArea(step, context, areas);
                ApplyInstruction(instructionArea, step);
            }
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
            SetBlackOverlayVisible(dimmer.color.a > 0.01f);
            handTween?.Kill();
            handTween = null;
            pendingHandStep = null;
            pendingHandAreas = null;
            bool hideHand = step.Id == "highlight_next_enemy"
                || step.Id == "protect_frog"
                || step.Id == "read_enemy_description"
                || step.Id == "link_new_generator_to_nexus"
                || step.Id == "water_tower_hint"
                || step.Id == "burn_status_focus"
                || step.Id == "burn_status_hold";
            if (hand != null)
            {
                hand.gameObject.SetActive(false);
            }

            if (!hideHand && hand != null)
            {
                pendingHandStep = step;
                pendingHandAreas = areas;
                if (IsInstructionComplete
                    || step.Id == "fire_tower_intro"
                    || step.Id == "enemy_strength_warning")
                {
                    PlayPendingHand();
                }
            }
        }

        public void Hide()
        {
            handTween?.Kill();
            overlayFade?.Kill();
            instructionTween?.Kill();
            handTween = null;
            pendingHandStep = null;
            pendingHandAreas = null;
            IsInstructionComplete = true;
            StopTypingSound();
            SetBlackOverlayVisible(false);
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
            StopTypingSound();
            PlayPendingHand();
        }

        public void SetPaused(bool paused)
        {
            if (handTween == null) return;
            if (paused) handTween.Pause(); else handTween.Play();
        }

        private void SetBlackOverlayVisible(bool visible)
        {
            if (IsBlackOverlayVisible == visible) return;
            IsBlackOverlayVisible = visible;
            BlackOverlayVisibilityChanged?.Invoke(visible);
        }

        private void ApplySpotlights(Rect[] areas, float glowAlpha, bool blockOutsideFocus)
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
            raycastFilter.SetFocus(areas, blockOutsideFocus);
        }

        private void ClearSpotlights()
        {
            dimmerMaterial.SetFloat(FocusCountId, 0f);
            dimmerMaterial.SetFloat(GlowEnabledId, 0f);
            dimmer.color = new Color(0f, 0f, 0f, darkness);
            raycastFilter.SetFocus(Array.Empty<Rect>(), false);
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
            bool placeBesideTarget = step.Id == "highlight_next_enemy"
                || step.Id == "inspect_next_enemy";
            bool isMagicResistanceIntroduction = step.Id == "inspect_magic_resistant";
            bool isWaveTwoWarning = step.Id == "wave_two_warning";
            bool isEnemyStrengthWarning = step.Id == "enemy_strength_warning"
                || step.Id == "water_tower_hint";
            bool isTutorialWarning = isWaveTwoWarning
                || isEnemyStrengthWarning
                || step.Id == "highlight_generator_card"
                || step.Id == "fire_tower_intro";
            Rect safe = Screen.safeArea;
            float safeTextWidth = Mathf.Max(1f, safe.width - 48f);
            label.sizeDelta = isFrogInstruction
                ? new Vector2(Mathf.Min(480f, Screen.width * 0.42f), 120f)
                : new Vector2(Mathf.Min(Mathf.Min(620f, Screen.width * 0.72f), safeTextWidth), 84f);
            instruction.fontStyle = FontStyle.Bold;
            instruction.alignment = placeBesideTarget || isMagicResistanceIntroduction
                ? TextAnchor.MiddleLeft
                : TextAnchor.MiddleCenter;
            instruction.color = new Color(1f, 0.9f, 0.62f, 1f);
            instruction.horizontalOverflow = HorizontalWrapMode.Wrap;
            instruction.verticalOverflow = VerticalWrapMode.Overflow;

            float halfWidth = label.sizeDelta.x * 0.5f;
            float halfHeight = label.sizeDelta.y * 0.5f;
            float x;
            float y;
            if (isMagicResistanceIntroduction)
            {
                label.pivot = new Vector2(0f, 0.5f);
                x = safe.xMin + 24f;
                float below = area.yMin - halfHeight - 20f;
                float above = area.yMax + halfHeight + 20f;
                y = below >= safe.yMin + halfHeight ? below : above;
                y = Mathf.Clamp(y, safe.yMin + halfHeight, safe.yMax - halfHeight);
            }
            else if (placeBesideTarget)
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
            else if (isWaveTwoWarning)
            {
                label.pivot = new Vector2(0.5f, 0.5f);
                x = Mathf.Clamp(area.center.x, safe.xMin + halfWidth, safe.xMax - halfWidth);
                y = Mathf.Clamp(
                    area.yMax + halfHeight + 28f,
                    safe.yMin + halfHeight,
                    safe.yMax - halfHeight);
            }
            else if (isEnemyStrengthWarning)
            {
                label.pivot = new Vector2(0.5f, 0.5f);
                x = Mathf.Clamp(area.center.x, safe.xMin + halfWidth, safe.xMax - halfWidth);
                y = Mathf.Clamp(
                    area.yMax + halfHeight + 28f,
                    safe.yMin + halfHeight,
                    safe.yMax - halfHeight);
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

        private static Rect GetInstructionArea(
            TutorialStep step,
            TutorialContext context,
            Rect[] spotlightAreas)
        {
            if (!string.IsNullOrEmpty(step.InstructionTargetId))
            {
                Rect[] instructionAreas = GetFocusRects(step.InstructionTargetId, context);
                if (instructionAreas.Length > 0)
                {
                    return instructionAreas[0];
                }
            }

            return step.Id == "highlight_next_enemy"
                ? spotlightAreas[spotlightAreas.Length - 1]
                : GetCombinedRect(spotlightAreas);
        }

        private void PlayInstruction(string value)
        {
            instructionTween?.Kill();
            StopTypingSound();
            instructionValue = value;
            IsInstructionComplete = false;
            Color color = instruction.color;
            color.a = 0f;
            instruction.color = color;
            instruction.text = string.Empty;

            int visibleCharacters = 0;
            float duration = Mathf.Clamp(value.Length * 0.035f, 0.4f, 1.6f);
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            soundPlayer?.Play(SoundId.TutorialTyping);
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
            sequence.OnComplete(() =>
            {
                IsInstructionComplete = true;
                StopTypingSound();
                PlayPendingHand();
            });
            instructionTween = sequence;
        }

        private void StopTypingSound()
        {
            soundPlayer?.Stop(SoundId.TutorialTyping);
        }

        private void PlayPendingHand()
        {
            if (pendingHandStep == null || pendingHandAreas == null || hand == null)
            {
                return;
            }

            TutorialStep step = pendingHandStep;
            Rect[] areas = pendingHandAreas;
            pendingHandStep = null;
            pendingHandAreas = null;
            hand.gameObject.SetActive(true);
            handTween = hand.Play(step, areas);
        }

        private void EnsureParts()
        {
            cameraCanvas = GetComponent<Canvas>();
            if (canvasGroup == null
                || canvasGroup.gameObject != gameObject
                || cameraCanvas == null
                || dimmer == null
                || instruction == null
                || hand == null)
            {
                throw new InvalidOperationException("TutorialOverlayView requires fully authored UI references.");
            }

            dimmer.rectTransform.SetAsFirstSibling();
            Stretch(dimmer.rectTransform);
            raycastFilter = dimmer.GetComponent<TutorialSoftMaskRaycastFilter>();
            if (raycastFilter == null)
            {
                throw new InvalidOperationException("Tutorial dimmer requires an authored raycast filter.");
            }
            if (dimmerMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/TutorialSoftMask");
                if (shader == null) throw new InvalidOperationException("Missing TutorialSoftMask shader.");
                dimmerMaterial = new Material(shader) { name = "Tutorial Soft Mask (Runtime)" };
                dimmer.material = dimmerMaterial;
            }

            if (instructionFont != null) instruction.font = instructionFont;
        }

        private void ConfigureCameraCanvas()
        {
            Camera camera = Camera.main;
            if (camera == null || cameraCanvas == null) return;
            cameraCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            cameraCanvas.worldCamera = camera;
            cameraCanvas.planeDistance = 1f;
            cameraCanvas.overrideSorting = true;
            cameraCanvas.sortingOrder = 100;
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
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera != null ? canvas.worldCamera : Camera.main
                : null;
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
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

            TutorialTargetView tutorialTarget = target.GetComponent<TutorialTargetView>();
            if (tutorialTarget != null
                && tutorialTarget.TryGetWorldBounds(out Bounds tutorialBounds)
                && TryProjectBounds(camera, tutorialBounds, out Rect tutorialRect))
            {
                return tutorialRect;
            }

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
            StopTypingSound();
            if (dimmerMaterial != null) Destroy(dimmerMaterial);
        }
    }
}
