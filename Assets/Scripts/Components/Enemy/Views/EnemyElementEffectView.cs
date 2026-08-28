using System;
using System.Collections.Generic;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    public enum EnemyElementEffectPlacement
    {
        FollowEnemy,
        ReactionPosition,
        EnemyPosition,
        EnemyFeet
    }

    /// <summary>
    /// Shows authored element marks and reaction effects. Roots keep their authored scale while
    /// renderer opacity fades; reaction roots can follow an enemy or detach at an authored point.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyElementEffectView : MonoBehaviour
    {
        private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

        private sealed class EffectRoot
        {
            public EffectRoot(GameObject root)
            {
                Root = root;
                AuthoredParent = root.transform.parent;
                AuthoredLocalPosition = root.transform.localPosition;
                AuthoredLocalRotation = root.transform.localRotation;
                AuthoredScale = root.transform.localScale;
                Particles = root.GetComponentsInChildren<ParticleSystem>(true);
                Renderers = root.GetComponentsInChildren<Renderer>(true);
                AuthoredColors = new Color[Renderers.Length];
                for (int index = 0; index < Renderers.Length; index++)
                {
                    Material material = Renderers[index].sharedMaterial;
                    AuthoredColors[index] = material != null && material.HasProperty(BaseColorProperty)
                        ? material.GetColor(BaseColorProperty)
                        : Color.white;
                }
            }

            public GameObject Root { get; }
            public Transform AuthoredParent { get; }
            public Vector3 AuthoredLocalPosition { get; }
            public Quaternion AuthoredLocalRotation { get; }
            public Vector3 AuthoredScale { get; }
            public ParticleSystem[] Particles { get; }
            public Renderer[] Renderers { get; }
            public Color[] AuthoredColors { get; }
        }

        private sealed class OverlayReaction
        {
            public OverlayReaction(
                ReactionEffectAuthoring authoring,
                EffectRoot effect,
                float remainingSeconds)
            {
                Authoring = authoring;
                Effect = effect;
                RemainingSeconds = remainingSeconds;
            }

            public ReactionEffectAuthoring Authoring { get; }
            public EffectRoot Effect { get; }
            public float RemainingSeconds { get; set; }
            public float Opacity { get; set; }
            public bool IsFadingOut { get; set; }
        }

        [Serializable]
        private sealed class ReactionEffectAuthoring
        {
            [SerializeField] private ElementReactionId reactionId;
            [SerializeField] private GameObject effect;
            [SerializeField] private EnemyElementEffectPlacement placement = EnemyElementEffectPlacement.FollowEnemy;
            [SerializeField, Min(0)] private int priority;
            [SerializeField, Min(0.01f)] private float durationSeconds = 1f;

            public ElementReactionId ReactionId => reactionId;
            public GameObject Effect => effect;
            public EnemyElementEffectPlacement Placement => placement;
            public int Priority => priority;
            public float DurationSeconds => durationSeconds;
        }

        [SerializeField] private GameObject fireEffect;
        [SerializeField] private GameObject waterEffect;
        [SerializeField] private GameObject windEffect;
        [SerializeField] private Transform reactionEffectSpawnPoint;
        [SerializeField] private ReactionEffectAuthoring[] reactionEffects = new ReactionEffectAuthoring[0];
        [SerializeField, Min(0.01f)] private float transitionDurationSeconds = 0.4f;

        private Transform enemyRoot;
        private MaterialPropertyBlock properties;
        private EffectRoot[] effectsByElement;
        private EffectRoot activeEffect;
        private EffectRoot desiredEffect;
        private EffectRoot activeReactionEffect;
        private EffectRoot pendingReactionEffect;
        private ReactionEffectAuthoring activeReactionAuthoring;
        private ReactionEffectAuthoring pendingReactionAuthoring;
        private EffectRoot reactionTransitionSource;
        private float reactionTransitionSourceOpacity;
        private float reactionTransitionProgress;
        private float reactionRemainingSeconds;
        private float pendingReactionDurationSeconds;
        private float opacityProgress;
        private readonly List<OverlayReaction> overlayReactions = new List<OverlayReaction>();

        public void Bind(EnemyElementState state)
        {
            EnsureInitialized();
            ResetEffects();
            desiredEffect = GetDesiredEffect(state);
            ShowDesiredEffect();
        }

        public void Render(EnemyElementState state, float deltaTime)
        {
            EnsureInitialized();
            TickOverlayReactions(deltaTime);
            if (pendingReactionEffect != null)
            {
                TickReactionTransition(deltaTime);
                return;
            }

            if (activeReactionEffect != null)
            {
                TickActiveReaction(deltaTime);
                return;
            }

            desiredEffect = GetDesiredEffect(state);
            ShowDesiredEffect();
            TickElementTransition(deltaTime);
        }

        public void ShowReaction(ElementReactionEvent reaction)
        {
            EnsureInitialized();
            ReactionEffectAuthoring authoring = GetReactionEffect(reaction.ReactionId);
            if (authoring == null || authoring.Effect == null)
            {
                return;
            }

            float durationSeconds = reaction.DurationSeconds > 0f
                ? reaction.DurationSeconds
                : authoring.DurationSeconds;
            if (!IsElementReplacementReaction(authoring))
            {
                StartOverlayReaction(authoring, reaction, durationSeconds);
                return;
            }

            if (activeReactionAuthoring != null)
            {
                if (authoring.Priority < activeReactionAuthoring.Priority)
                {
                    return;
                }

                if (authoring.ReactionId == activeReactionAuthoring.ReactionId)
                {
                    reactionRemainingSeconds = durationSeconds;
                    return;
                }
            }

            if (pendingReactionAuthoring != null)
            {
                if (authoring.Priority < pendingReactionAuthoring.Priority)
                {
                    return;
                }

                if (authoring.ReactionId == pendingReactionAuthoring.ReactionId)
                {
                    pendingReactionDurationSeconds = durationSeconds;
                    return;
                }
            }

            StartReactionTransition(authoring, reaction, durationSeconds);
        }

        public void Release()
        {
            EnsureInitialized();
            ResetEffects();
        }

        private void EnsureInitialized()
        {
            if (properties == null)
            {
                properties = new MaterialPropertyBlock();
            }

            if (effectsByElement != null)
            {
                return;
            }

            effectsByElement = new[]
            {
                CreateEffectRoot(fireEffect),
                CreateEffectRoot(waterEffect),
                CreateEffectRoot(windEffect)
            };
            ResetEffects();
        }

        private static EffectRoot CreateEffectRoot(GameObject root)
        {
            return root != null ? new EffectRoot(root) : null;
        }

        private ReactionEffectAuthoring GetReactionEffect(ElementReactionId reactionId)
        {
            if (reactionEffects == null)
            {
                return null;
            }

            for (int index = 0; index < reactionEffects.Length; index++)
            {
                if (reactionEffects[index] != null && reactionEffects[index].ReactionId == reactionId)
                {
                    return reactionEffects[index];
                }
            }

            return null;
        }

        private static bool IsElementReplacementReaction(ReactionEffectAuthoring authoring) => false;

        private EffectRoot GetDesiredEffect(EnemyElementState state)
        {
            return state.Phase == EnemyElementPhase.Marked
                ? effectsByElement[(int)state.Element]
                : null;
        }

        private void ShowDesiredEffect()
        {
            if (activeEffect != null || desiredEffect == null)
            {
                return;
            }

            activeEffect = desiredEffect;
            opacityProgress = 0f;
            activeEffect.Root.transform.localScale = activeEffect.AuthoredScale;
            activeEffect.Root.SetActive(true);
            SetOpacity(activeEffect, 0f);
            PlayParticles(activeEffect);
        }

        private void TickElementTransition(float deltaTime)
        {
            if (activeEffect == null)
            {
                return;
            }

            float targetOpacity = activeEffect == desiredEffect ? 1f : 0f;
            opacityProgress = Mathf.MoveTowards(
                opacityProgress,
                targetOpacity,
                deltaTime / transitionDurationSeconds);
            SetOpacity(activeEffect, opacityProgress);
            if (opacityProgress > 0f || targetOpacity > 0f)
            {
                return;
            }

            Deactivate(activeEffect);
            activeEffect = null;
            ShowDesiredEffect();
        }

        private void StartReactionTransition(
            ReactionEffectAuthoring authoring,
            ElementReactionEvent reaction,
            float durationSeconds)
        {
            if (pendingReactionEffect != null)
            {
                Deactivate(pendingReactionEffect);
                pendingReactionEffect = null;
            }

            reactionTransitionSource = activeReactionEffect != null ? activeReactionEffect : activeEffect;
            reactionTransitionSourceOpacity = activeReactionEffect != null ? 1f : opacityProgress;
            desiredEffect = null;
            pendingReactionAuthoring = authoring;
            pendingReactionDurationSeconds = durationSeconds;
            pendingReactionEffect = new EffectRoot(authoring.Effect);
            PositionReactionEffect(pendingReactionEffect, authoring, reaction);
            pendingReactionEffect.Root.transform.localScale = pendingReactionEffect.AuthoredScale;
            pendingReactionEffect.Root.SetActive(true);
            SetOpacity(pendingReactionEffect, 0f);
            PlayParticles(pendingReactionEffect);

            reactionTransitionProgress = 0f;
        }

        private void StartOverlayReaction(
            ReactionEffectAuthoring authoring,
            ElementReactionEvent reaction,
            float durationSeconds)
        {
            for (int index = 0; index < overlayReactions.Count; index++)
            {
                OverlayReaction existing = overlayReactions[index];
                if (existing.Authoring.ReactionId != authoring.ReactionId)
                {
                    continue;
                }

                existing.RemainingSeconds = durationSeconds;
                existing.Opacity = 0f;
                existing.IsFadingOut = false;
                PositionReactionEffect(existing.Effect, authoring, reaction);
                SetOpacity(existing.Effect, 0f);
                PlayParticles(existing.Effect);
                return;
            }

            var effect = new EffectRoot(authoring.Effect);
            PositionReactionEffect(effect, authoring, reaction);
            effect.Root.transform.localScale = effect.AuthoredScale;
            effect.Root.SetActive(true);
            SetOpacity(effect, 0f);
            PlayParticles(effect);
            overlayReactions.Add(new OverlayReaction(authoring, effect, durationSeconds));
        }

        private void PositionReactionEffect(
            EffectRoot effect,
            ReactionEffectAuthoring authoring,
            ElementReactionEvent reaction)
        {
            if (authoring.Placement == EnemyElementEffectPlacement.FollowEnemy)
            {
                return;
            }

            effect.Root.transform.SetParent(null, true);
            effect.Root.transform.position = ResolvePlacementPosition(authoring, reaction);
        }

        private Vector3 ResolvePlacementPosition(
            ReactionEffectAuthoring authoring,
            ElementReactionEvent reaction)
        {
            switch (authoring.Placement)
            {
                case EnemyElementEffectPlacement.EnemyPosition:
                    return GetReactionEffectSpawnPosition();
                case EnemyElementEffectPlacement.EnemyFeet:
                    return GetEnemyFeetPosition();
                default:
                    return reaction.Position;
            }
        }

        /// <summary>
        /// Ground position under the enemy. Read at trigger time and then detached, so an
        /// effect placed here stays on the ground while a lift throws the enemy upwards.
        /// </summary>
        private Vector3 GetEnemyFeetPosition()
        {
            if (enemyRoot == null)
            {
                var view = GetComponentInParent<EnemyView>();
                enemyRoot = view != null ? view.transform : transform;
            }

            return enemyRoot.position;
        }

        private void TickReactionTransition(float deltaTime)
        {
            reactionTransitionProgress = Mathf.MoveTowards(
                reactionTransitionProgress,
                1f,
                deltaTime / transitionDurationSeconds);

            if (reactionTransitionSource != null)
            {
                SetOpacity(
                    reactionTransitionSource,
                    Mathf.Lerp(reactionTransitionSourceOpacity, 0f, reactionTransitionProgress));
            }

            SetOpacity(pendingReactionEffect, reactionTransitionProgress);
            if (reactionTransitionProgress < 1f)
            {
                return;
            }

            if (activeEffect != null)
            {
                Deactivate(activeEffect);
                activeEffect = null;
            }

            if (activeReactionEffect != null)
            {
                Deactivate(activeReactionEffect);
            }

            activeReactionEffect = pendingReactionEffect;
            activeReactionAuthoring = pendingReactionAuthoring;
            reactionRemainingSeconds = pendingReactionDurationSeconds;
            pendingReactionEffect = null;
            pendingReactionAuthoring = null;
            pendingReactionDurationSeconds = 0f;
            reactionTransitionSource = null;
            reactionTransitionSourceOpacity = 0f;
        }

        private void TickOverlayReactions(float deltaTime)
        {
            for (int index = overlayReactions.Count - 1; index >= 0; index--)
            {
                OverlayReaction overlay = overlayReactions[index];
                if (!overlay.IsFadingOut)
                {
                    overlay.RemainingSeconds = Mathf.Max(
                        0f,
                        overlay.RemainingSeconds - deltaTime);
                    overlay.Opacity = Mathf.MoveTowards(
                        overlay.Opacity,
                        1f,
                        deltaTime / transitionDurationSeconds);
                    if (overlay.RemainingSeconds <= 0f)
                    {
                        overlay.IsFadingOut = true;
                    }
                }
                else
                {
                    overlay.Opacity = Mathf.MoveTowards(
                        overlay.Opacity,
                        0f,
                        deltaTime / transitionDurationSeconds);
                    if (overlay.Opacity <= 0f)
                    {
                        Deactivate(overlay.Effect);
                        overlayReactions.RemoveAt(index);
                        continue;
                    }
                }

                SetOpacity(overlay.Effect, overlay.Opacity);
            }
        }

        private Vector3 GetReactionEffectSpawnPosition()
        {
            return reactionEffectSpawnPoint != null
                ? reactionEffectSpawnPoint.position
                : transform.position;
        }

        private void TickActiveReaction(float deltaTime)
        {
            reactionRemainingSeconds = Mathf.Max(0f, reactionRemainingSeconds - deltaTime);
            SetOpacity(activeReactionEffect, 1f);
            if (reactionRemainingSeconds > 0f)
            {
                return;
            }

            Deactivate(activeReactionEffect);
            activeReactionEffect = null;
            activeReactionAuthoring = null;
        }

        private void ResetEffects()
        {
            if (effectsByElement != null)
            {
                for (int index = 0; index < effectsByElement.Length; index++)
                {
                    EffectRoot effect = effectsByElement[index];
                    if (effect != null)
                    {
                        Deactivate(effect);
                    }
                }
            }

            if (activeEffect != null)
            {
                Deactivate(activeEffect);
            }

            if (activeReactionEffect != null)
            {
                Deactivate(activeReactionEffect);
            }

            if (pendingReactionEffect != null)
            {
                Deactivate(pendingReactionEffect);
            }

            for (int index = 0; index < overlayReactions.Count; index++)
            {
                Deactivate(overlayReactions[index].Effect);
            }

            overlayReactions.Clear();

            activeEffect = null;
            desiredEffect = null;
            activeReactionEffect = null;
            pendingReactionEffect = null;
            activeReactionAuthoring = null;
            pendingReactionAuthoring = null;
            reactionTransitionSource = null;
            reactionTransitionSourceOpacity = 0f;
            reactionTransitionProgress = 0f;
            reactionRemainingSeconds = 0f;
            pendingReactionDurationSeconds = 0f;
            opacityProgress = 0f;
        }

        private void SetOpacity(EffectRoot effect, float opacity)
        {
            if (effect == null)
            {
                return;
            }

            for (int index = 0; index < effect.Renderers.Length; index++)
            {
                Renderer renderer = effect.Renderers[index];
                if (renderer == null
                    || renderer.sharedMaterial == null
                    || !renderer.sharedMaterial.HasProperty(BaseColorProperty))
                {
                    continue;
                }

                Color color = effect.AuthoredColors[index];
                color.a *= opacity;
                properties.SetColor(BaseColorProperty, color);
                renderer.SetPropertyBlock(properties);
            }
        }

        private static void PlayParticles(EffectRoot effect)
        {
            for (int index = 0; index < effect.Particles.Length; index++)
            {
                ParticleSystem particle = effect.Particles[index];
                if (!HasParticleSystemAncestor(effect.Root.transform, particle.transform))
                {
                    particle.Play(true);
                }
            }
        }

        private static void StopParticles(EffectRoot effect)
        {
            for (int index = 0; index < effect.Particles.Length; index++)
            {
                ParticleSystem particle = effect.Particles[index];
                if (!HasParticleSystemAncestor(effect.Root.transform, particle.transform))
                {
                    particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static bool HasParticleSystemAncestor(Transform root, Transform particle)
        {
            if (particle == root)
            {
                return false;
            }

            Transform parent = particle.parent;
            while (parent != null)
            {
                if (parent.GetComponent<ParticleSystem>() != null)
                {
                    return true;
                }

                if (parent == root)
                {
                    return false;
                }

                parent = parent.parent;
            }

            return false;
        }

        private static void Deactivate(EffectRoot effect)
        {
            StopParticles(effect);

            for (int index = 0; index < effect.Renderers.Length; index++)
            {
                Renderer renderer = effect.Renderers[index];
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            effect.Root.transform.SetParent(effect.AuthoredParent, false);
            effect.Root.transform.localPosition = effect.AuthoredLocalPosition;
            effect.Root.transform.localRotation = effect.AuthoredLocalRotation;
            effect.Root.transform.localScale = effect.AuthoredScale;
            effect.Root.SetActive(false);
        }
    }
}
