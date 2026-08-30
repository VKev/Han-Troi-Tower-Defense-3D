using System;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(EnemyDamageFlashView))]
    public sealed class EnemyView : MonoBehaviour
    {
        private const float TurnSpeedDegreesPerSecond = 360f;
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        [SerializeField, Min(0.01f)] private float deathScaleDurationSeconds = 0.2f;

        private Animator animator;
        private EnemyDamageFlashView damageFlashView;
        private EnemyElementStatusView elementStatusView;
        private EnemyElementEffectView elementEffectView;
        private EnemyThermalShieldView thermalShieldView;
        private EnemyStealthView stealthView;
        private EnemySkillEffectView skillEffectView;
        private EnemySpeedTrailView speedTrailView;
        private Quaternion spawnLocalRotation;
        private Vector3 spawnLocalScale;
        private Vector3 scalePivotLocal;
        private Vector3 scaleAnchorWorld;
        private Vector3 renderedRootPosition;
        private Vector3 renderedMoveDirection;
        private float scaleProgress = 1f;
        private float spawnScaleDelayRemainingSeconds;
        private bool isSpawning;
        private bool isDying;
        private bool isAwaitingActivation;
        private bool isAwaitingScaleStart;
        private Action deathCompletion;
        private bool hasFacingDirection;
        private int renderedSkillCastVersion;

        public long EnemyId { get; private set; }

        /// <summary>
        /// World position on the road where the model's bottom-center pivot is planted.
        /// </summary>
        public Vector3 RenderedRootPosition => renderedRootPosition;

        /// <summary>
        /// Unit heading of the last movement the view rendered, or zero before the enemy has
        /// moved. Zero on the spawn frame, since the enemy has no previous position yet.
        /// </summary>
        public Vector3 RenderedMoveDirection => renderedMoveDirection;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            damageFlashView = GetComponent<EnemyDamageFlashView>();
            elementStatusView = GetComponentInChildren<EnemyElementStatusView>(true);
            elementEffectView = GetComponentInChildren<EnemyElementEffectView>(true);
            thermalShieldView = GetComponentInChildren<EnemyThermalShieldView>(true);
            stealthView = GetComponentInChildren<EnemyStealthView>(true);
            skillEffectView = GetComponentInChildren<EnemySkillEffectView>(true);
            speedTrailView = GetComponentInChildren<EnemySpeedTrailView>(true);
            spawnLocalRotation = transform.localRotation;
            spawnLocalScale = transform.localScale;
        }

        public void Configure(Camera worldCamera, Vfx.GlobalEffectEmitterView reactionEffectEmitter)
        {
            GetElementStatusView().Configure(worldCamera);
            GetElementEffectView().ConfigureReactionEmitter(reactionEffectEmitter);
            GetSkillEffectView()?.ConfigureEmitter(reactionEffectEmitter);
        }

        public void Bind(EnemySnapshot enemy, bool activateImmediately = true)
        {
            SetRenderingVisible(false);
            EnemyId = enemy.EnemyId;
            string prefix = enemy.IsSummoned ? "Summoned Enemy" : "Enemy";
            gameObject.name = $"{prefix} {enemy.EnemyId} - {enemy.Definition.DisplayName}";
            renderedRootPosition = enemy.Position;
            transform.position = renderedRootPosition;
            transform.localRotation = spawnLocalRotation;
            hasFacingDirection = false;
            renderedSkillCastVersion = enemy.SkillCastVersion;
            isAwaitingActivation = true;
            isAwaitingScaleStart = false;
            if (!activateImmediately)
            {
                return;
            }

            ActivateHidden(enemy);
            StartScaleTransition();
            SetRenderingVisible(true);
        }

        public void Render(EnemySnapshot enemy, float interpolationAlpha)
        {
            Vector3 interpolatedRootPosition = Vector3.Lerp(
                enemy.PreviousPosition,
                enemy.Position,
                interpolationAlpha) + Vector3.up * enemy.LiftHeightMeters;
            if (isAwaitingActivation)
            {
                renderedRootPosition = interpolatedRootPosition;
                transform.position = renderedRootPosition;
                ActivateHidden(enemy);
                isAwaitingScaleStart = true;
                return;
            }

            if (isAwaitingScaleStart)
            {
                StartScaleTransition();
            }

            GetDamageFlashView().Render(enemy, Time.deltaTime);
            GetStealthView()?.Render(enemy, Time.deltaTime);
            bool skillCastStarted = enemy.SkillCastVersion != renderedSkillCastVersion;
            if (skillCastStarted)
            {
                renderedSkillCastVersion = enemy.SkillCastVersion;
                Animator targetAnimator = animator != null ? animator : GetComponent<Animator>();
                targetAnimator?.Play("Skill", 0, 0f);
            }
            GetElementStatusView().Render(enemy.ElementState, Time.deltaTime);
            GetElementEffectView().Render(enemy.ElementState, Time.deltaTime);
            GetThermalShieldView()?.Render(enemy, Time.deltaTime);
            renderedRootPosition = interpolatedRootPosition;
            transform.position = renderedRootPosition;
            if (skillCastStarted)
            {
                GetSkillEffectView()?.Play(enemy.SkillCastVersion);
            }
            GetSpeedTrailView()?.Render(enemy.IsSpeedBuffed);

            Vector3 movement = enemy.Position - enemy.PreviousPosition;
            movement.y = 0f;
            if (movement.sqrMagnitude == 0f)
            {
                TickScaleTransition();
                SetRenderingVisible(true);
                return;
            }

            renderedMoveDirection = movement.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            if (!hasFacingDirection)
            {
                transform.rotation = targetRotation;
                hasFacingDirection = true;
                TickScaleTransition();
                SetRenderingVisible(true);
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                TurnSpeedDegreesPerSecond * Time.deltaTime);
            TickScaleTransition();
            SetRenderingVisible(true);
        }

        private void ActivateHidden(EnemySnapshot enemy)
        {
            gameObject.SetActive(true);
            isAwaitingActivation = false;
            isDying = false;
            deathCompletion = null;
            SetMoving(true);
            GetDamageFlashView().Bind(enemy);
            GetElementStatusView().Bind(enemy.ElementState);
            GetElementEffectView().Bind(enemy.ElementState);
            GetThermalShieldView()?.Bind(enemy);
            GetStealthView()?.Bind(enemy);
            GetSkillEffectView()?.Bind(enemy.SkillCastVersion);
            GetSpeedTrailView()?.Bind();
            SetRenderingVisible(false);
        }

        private void StartScaleTransition()
        {
            isAwaitingScaleStart = false;
            CaptureScalePivot();
            scaleProgress = 0f;
            spawnScaleDelayRemainingSeconds = EnemySpawnPresentationTiming.SpawnScaleDelaySeconds;
            isSpawning = true;
            scaleAnchorWorld = renderedRootPosition;
            ApplyScaleAroundAnchor(0f);
        }

        public void BeginDeath(Action onComplete)
        {
            if (isAwaitingActivation || isAwaitingScaleStart)
            {
                isAwaitingActivation = false;
                isAwaitingScaleStart = false;
                onComplete?.Invoke();
                return;
            }

            if (isDying)
            {
                return;
            }

            gameObject.SetActive(true);
            isSpawning = false;
            isDying = true;
            deathCompletion = onComplete;
            scaleAnchorWorld = transform.TransformPoint(scalePivotLocal);
            if (scaleProgress < 1f)
            {
                scaleProgress = 1f;
                ApplyScaleAroundAnchor(scaleProgress);
            }

            SetMoving(false);
            GetDamageFlashView().Release();
            GetElementStatusView().Release();
            GetElementEffectView().Release();
            GetStealthView()?.Release();
            GetSkillEffectView()?.Release();
            GetSpeedTrailView()?.Release();
        }

        public void TickLifecycle(float deltaTime)
        {
            TickScaleTransition(deltaTime);
        }

        public void Release()
        {
            SetRenderingVisible(false);
            gameObject.SetActive(false);
            deathCompletion = null;
            isSpawning = false;
            isDying = false;
            isAwaitingActivation = false;
            isAwaitingScaleStart = false;
            scaleProgress = 1f;
            spawnScaleDelayRemainingSeconds = 0f;
            transform.localScale = spawnLocalScale;
            SetMoving(false);
            GetDamageFlashView().Release();
            GetElementStatusView().Release();
            GetElementEffectView().Release();
            GetThermalShieldView()?.Release();
            GetStealthView()?.Release();
            GetSkillEffectView()?.Release();
            GetSpeedTrailView()?.Release();
            EnemyId = 0L;
            renderedSkillCastVersion = 0;
            renderedRootPosition = Vector3.zero;
            renderedMoveDirection = Vector3.zero;
            hasFacingDirection = false;
        }

        private void TickScaleTransition()
        {
            TickScaleTransition(Time.deltaTime);
        }

        private void TickScaleTransition(float deltaTime)
        {
            if (!isSpawning && !isDying)
            {
                scaleAnchorWorld = renderedRootPosition;
                ApplyScaleAroundAnchor(1f);
                return;
            }

            if (isSpawning)
            {
                scaleAnchorWorld = renderedRootPosition;
                if (spawnScaleDelayRemainingSeconds > 0f)
                {
                    float heldSeconds = spawnScaleDelayRemainingSeconds;
                    spawnScaleDelayRemainingSeconds -= deltaTime;
                    if (spawnScaleDelayRemainingSeconds > 0f)
                    {
                        ApplyScaleAroundAnchor(0f);
                        return;
                    }

                    deltaTime -= heldSeconds;
                }

                scaleProgress = Mathf.MoveTowards(
                    scaleProgress,
                    1f,
                    deltaTime / EnemySpawnPresentationTiming.SpawnScaleDurationSeconds);
            }
            else
            {
                scaleProgress = Mathf.MoveTowards(
                    scaleProgress,
                    0f,
                    deltaTime / deathScaleDurationSeconds);
            }

            ApplyScaleAroundAnchor(scaleProgress);
            if (isSpawning && scaleProgress >= 1f)
            {
                isSpawning = false;
                return;
            }

            if (!isDying || scaleProgress > 0f)
            {
                return;
            }

            isDying = false;
            Action completion = deathCompletion;
            deathCompletion = null;
            completion?.Invoke();
        }

        private void CaptureScalePivot()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer
                    || renderer.GetComponentInParent<EnemyElementStatusView>() != null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                scalePivotLocal = Vector3.zero;
                return;
            }

            scalePivotLocal = transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        }

        private void ApplyScaleAroundAnchor(float progress)
        {
            transform.localScale = spawnLocalScale * progress;
            Vector3 currentAnchorWorld = transform.TransformPoint(scalePivotLocal);
            transform.position += scaleAnchorWorld - currentAnchorWorld;
        }

        private void SetRenderingVisible(bool value)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].forceRenderingOff = !value;
            }
        }

        private void SetMoving(bool value)
        {
            Animator targetAnimator = animator != null ? animator : GetComponent<Animator>();
            if (targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.SetBool(IsMoving, value);
            }
        }

        private EnemyDamageFlashView GetDamageFlashView()
        {
            if (damageFlashView == null)
            {
                damageFlashView = GetComponent<EnemyDamageFlashView>();
            }

            return damageFlashView;
        }

        public void ShowReaction(ElementReactionEvent reaction)
        {
            GetElementStatusView().ShowReaction(reaction.Pair);
            GetElementEffectView().ShowReaction(reaction);
            if (reaction.ReactionId == ElementReactionId.ThermalShock)
            {
                GetThermalShieldView()?.ShowThermalShockHit();
            }
        }

        private EnemyThermalShieldView GetThermalShieldView()
        {
            if (thermalShieldView == null)
            {
                thermalShieldView = GetComponentInChildren<EnemyThermalShieldView>(true);
            }

            return thermalShieldView;
        }

        private EnemyStealthView GetStealthView()
        {
            if (stealthView == null)
            {
                stealthView = GetComponentInChildren<EnemyStealthView>(true);
            }

            return stealthView;
        }

        private EnemySkillEffectView GetSkillEffectView()
        {
            if (skillEffectView == null)
            {
                skillEffectView = GetComponentInChildren<EnemySkillEffectView>(true);
            }

            return skillEffectView;
        }

        private EnemySpeedTrailView GetSpeedTrailView()
        {
            if (speedTrailView == null)
            {
                speedTrailView = GetComponentInChildren<EnemySpeedTrailView>(true);
            }

            return speedTrailView;
        }

        private EnemyElementStatusView GetElementStatusView()
        {
            if (elementStatusView == null)
            {
                elementStatusView = GetComponentInChildren<EnemyElementStatusView>(true);
            }

            return elementStatusView;
        }

        private EnemyElementEffectView GetElementEffectView()
        {
            if (elementEffectView == null)
            {
                elementEffectView = GetComponentInChildren<EnemyElementEffectView>(true);
            }

            return elementEffectView;
        }
    }
}
