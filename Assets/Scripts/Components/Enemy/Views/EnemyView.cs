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

        [SerializeField, Min(0.01f)] private float spawnScaleDurationSeconds = 0.4f;
        [SerializeField, Min(0.01f)] private float deathScaleDurationSeconds = 0.2f;

        private Animator animator;
        private EnemyDamageFlashView damageFlashView;
        private EnemyElementStatusView elementStatusView;
        private EnemyElementEffectView elementEffectView;
        private EnemyThermalShieldView thermalShieldView;
        private Quaternion spawnLocalRotation;
        private Vector3 spawnLocalScale;
        private Vector3 scalePivotLocal;
        private Vector3 scaleAnchorWorld;
        private float scaleProgress = 1f;
        private bool isSpawning;
        private bool isDying;
        private Action deathCompletion;
        private bool hasFacingDirection;

        public long EnemyId { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            damageFlashView = GetComponent<EnemyDamageFlashView>();
            elementStatusView = GetComponentInChildren<EnemyElementStatusView>(true);
            elementEffectView = GetComponentInChildren<EnemyElementEffectView>(true);
            thermalShieldView = GetComponentInChildren<EnemyThermalShieldView>(true);
            spawnLocalRotation = transform.localRotation;
            spawnLocalScale = transform.localScale;
        }

        public void Configure(Camera worldCamera)
        {
            GetElementStatusView().Configure(worldCamera);
        }

        public void Bind(EnemySnapshot enemy)
        {
            EnemyId = enemy.EnemyId;
            string prefix = enemy.IsSummoned ? "Summoned Enemy" : "Enemy";
            gameObject.name = $"{prefix} {enemy.EnemyId} - {enemy.Definition.DisplayName}";
            transform.position = enemy.Position;
            transform.localRotation = spawnLocalRotation;
            hasFacingDirection = false;
            gameObject.SetActive(true);
            CaptureScalePivot();
            scaleProgress = 0f;
            isSpawning = true;
            isDying = false;
            deathCompletion = null;
            scaleAnchorWorld = transform.TransformPoint(scalePivotLocal);
            ApplyScaleAroundAnchor(0f);
            SetMoving(true);
            GetDamageFlashView().Bind(enemy);
            GetElementStatusView().Bind(enemy.ElementState);
            GetElementEffectView().Bind(enemy.ElementState);
            GetThermalShieldView()?.Bind(enemy);
        }

        public void Render(EnemySnapshot enemy, float interpolationAlpha)
        {
            GetDamageFlashView().Render(enemy, Time.deltaTime);
            GetElementStatusView().Render(enemy.ElementState, Time.deltaTime);
            GetElementEffectView().Render(enemy.ElementState, Time.deltaTime);
            GetThermalShieldView()?.Render(enemy, Time.deltaTime);
            transform.position = Vector3.Lerp(
                enemy.PreviousPosition,
                enemy.Position,
                interpolationAlpha) + Vector3.up * enemy.LiftHeightMeters;

            Vector3 movement = enemy.Position - enemy.PreviousPosition;
            movement.y = 0f;
            if (movement.sqrMagnitude == 0f)
            {
                TickScaleTransition();
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            if (!hasFacingDirection)
            {
                transform.rotation = targetRotation;
                hasFacingDirection = true;
                TickScaleTransition();
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                TurnSpeedDegreesPerSecond * Time.deltaTime);
            TickScaleTransition();
        }

        public void BeginDeath(Action onComplete)
        {
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
        }

        public void TickLifecycle(float deltaTime)
        {
            TickScaleTransition(deltaTime);
        }

        public void Release()
        {
            deathCompletion = null;
            isSpawning = false;
            isDying = false;
            scaleProgress = 1f;
            transform.localScale = spawnLocalScale;
            SetMoving(false);
            GetDamageFlashView().Release();
            GetElementStatusView().Release();
            GetElementEffectView().Release();
            GetThermalShieldView()?.Release();
            EnemyId = 0L;
            hasFacingDirection = false;
            gameObject.SetActive(false);
        }

        private void TickScaleTransition()
        {
            TickScaleTransition(Time.deltaTime);
        }

        private void TickScaleTransition(float deltaTime)
        {
            if (!isSpawning && !isDying)
            {
                return;
            }

            if (isSpawning)
            {
                scaleAnchorWorld = transform.TransformPoint(scalePivotLocal);
                scaleProgress = Mathf.MoveTowards(
                    scaleProgress,
                    1f,
                    deltaTime / spawnScaleDurationSeconds);
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
                if (renderer is ParticleSystemRenderer
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
