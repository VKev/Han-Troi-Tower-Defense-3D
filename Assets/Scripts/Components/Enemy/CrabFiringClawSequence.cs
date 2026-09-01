using System.Collections;
using TowerDefense3D.Towers;
using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CrabFiringClawSequence : MonoBehaviour, IHeroAttackView
    {
        private static readonly int PrepareState = Animator.StringToHash("Prepare");
        private static readonly int IdleState = Animator.StringToHash("Idle");

        [SerializeField] private GameObject firingClawPrefab;
        [SerializeField, Min(0f)] private float impactHeightMeters;
        [SerializeField, Min(0f)] private float minimumArcHeightMeters = 0.75f;
        [SerializeField, Min(0.01f)] private float minimumReachScale = 0.2f;
        [SerializeField, Min(0.01f)] private float maximumReachScale = 10f;
        [SerializeField, Range(0.1f, 0.9f)] private float raiseFractionOfLunge = 0.65f;
        [SerializeField, Range(0f, 1f)] private float raisedScaleFraction = 0.45f;
        [SerializeField, Min(0f)] private float bodyTurnSpeedDegreesPerSecond = 540f;

        private Animator animator;
        private TowerRuntimeView towerView;
        private HeroTowerDefinition authoredHero;
        private Transform bodyAimTransform;
        private GameObject firingClawInstance;
        private Coroutine sequence;
        private Vector3 aimPosition;
        private bool isAiming;
        private Transform originalFiringClawRoot;
        private Vector3 originalFiringClawScale;
        private bool originalFiringClawHidden;
        private Vector3 firingClawAuthoredScale;
        private Vector3 firingClawReturnPosition;
        private float firingClawReachScale = 1f;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            towerView = GetComponentInParent<TowerRuntimeView>();
            AuthoredTowerView authoredTower = GetComponentInParent<AuthoredTowerView>();
            authoredHero = authoredTower != null
                ? authoredTower.Definition as HeroTowerDefinition
                : null;
            EnsureBodyAimTransform();
        }

        private void OnDisable()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            isAiming = false;
            ResetFiringClawTransform();
            RestoreOriginalFiringClaw();
            if (animator != null)
            {
                animator.enabled = true;
            }

            SetFiringClawVisible(false);
        }

        private void Update()
        {
            if (isAiming && towerView != null)
            {
                FaceCrabTowards(GetFacingPosition());
            }
        }

        private void FaceCrabTowards(Vector3 worldPosition)
        {
            EnsureBodyAimTransform();
            Vector3 direction = worldPosition - bodyAimTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            bodyAimTransform.rotation = bodyTurnSpeedDegreesPerSecond <= 0f
                ? targetRotation
                : Quaternion.RotateTowards(
                    bodyAimTransform.rotation,
                    targetRotation,
                    bodyTurnSpeedDegreesPerSecond * Time.deltaTime);
        }

        private void EnsureBodyAimTransform()
        {
            if (bodyAimTransform != null)
            {
                return;
            }

            bodyAimTransform = transform.parent != null
                && transform.parent.name == "Crab Aim Pivot"
                ? transform.parent
                : transform;
        }

        private Vector3 GetFacingPosition()
        {
            if (authoredHero == null || !TryGetActiveEnemyInRange(out Vector3 enemyPosition))
            {
                return aimPosition;
            }

            return enemyPosition;
        }

        private bool TryGetActiveEnemyInRange(out Vector3 enemyPosition)
        {
            EnemyView[] enemies = FindObjectsByType<EnemyView>(FindObjectsSortMode.None);
            EnemyView selectedEnemy = null;
            float rangeSquared = authoredHero.AttackRangeMeters * authoredHero.AttackRangeMeters;
            for (int index = 0; index < enemies.Length; index++)
            {
                EnemyView candidate = enemies[index];
                Vector3 offset = candidate.RenderedRootPosition - towerView.transform.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > rangeSquared
                    || (selectedEnemy != null && candidate.EnemyId >= selectedEnemy.EnemyId))
                {
                    continue;
                }

                selectedEnemy = candidate;
            }

            enemyPosition = selectedEnemy != null
                ? selectedEnemy.RenderedRootPosition
                : default;
            return selectedEnemy != null;
        }

        public void PlayAttack(HeroAttackEvent attack)
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                return;
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (animator == null)
            {
                return;
            }

            if (sequence != null)
            {
                StopCoroutine(sequence);
            }

            SetFiringClawVisible(false);
            ResetFiringClawTransform();
            RestoreOriginalFiringClaw();
            sequence = StartCoroutine(RunAttack(attack));
        }

        private IEnumerator RunAttack(HeroAttackEvent attack)
        {
            aimPosition = attack.ImpactPosition;
            isAiming = true;
            animator.Play(PrepareState, 0, 0f);
            yield return new WaitForSeconds(attack.PrepareDurationSeconds);

            animator.Play(PrepareState, 0, 1f);
            animator.Update(0f);
            if (TryShowFiringClawAtCurrentPose())
            {
                yield return MoveClawTargetAlongArc(
                    attack.ImpactPosition,
                    attack.LungeDurationSeconds,
                    true);
                yield return new WaitForSeconds(attack.ImpactHoldDurationSeconds);
                yield return MoveClawTargetAlongArc(
                    firingClawReturnPosition,
                    attack.ReturnDurationSeconds,
                    false,
                    true);
            }

            SetFiringClawVisible(false);
            ResetFiringClawTransform();
            RestoreOriginalFiringClaw();
            animator.enabled = true;
            animator.Play(IdleState, 0, 0f);
            animator.Update(0f);
            isAiming = false;
            sequence = null;
        }

        private bool TryShowFiringClawAtCurrentPose()
        {
            if (firingClawPrefab == null)
            {
                return false;
            }

            EnsureFiringClawInstance();
            Transform sourceSkeleton = transform.Find("UniRigArmature");
            Transform clawSkeleton = firingClawInstance.transform.Find("UniRigArmature");
            if (sourceSkeleton == null || clawSkeleton == null)
            {
                return false;
            }

            firingClawInstance.transform.localPosition = Vector3.zero;
            firingClawInstance.transform.localRotation = Quaternion.identity;
            firingClawInstance.transform.localScale = firingClawAuthoredScale;
            CopyPose(sourceSkeleton, clawSkeleton);
            firingClawReturnPosition = GetOriginalTipPosition();
            CalculateFiringClawReachScale(aimPosition);
            SetClawControlsForCurrentPose();
            HideOriginalFiringClaw();
            SetFiringClawVisible(true);
            return true;
        }

        private void EnsureFiringClawInstance()
        {
            if (firingClawInstance != null)
            {
                return;
            }

            firingClawInstance = Instantiate(firingClawPrefab, transform);
            firingClawInstance.name = "Firing Claw IK (runtime)";
            firingClawAuthoredScale = firingClawInstance.transform.localScale;
            SetFiringClawVisible(false);
            Animator clawAnimator = firingClawInstance.GetComponent<Animator>();
            if (clawAnimator != null)
            {
                clawAnimator.runtimeAnimatorController = null;
            }
        }

        private void CalculateFiringClawReachScale(Vector3 impactPosition)
        {
            Transform root = FindClawBone("Bone_039");
            Transform tip = FindClawBone("Bone_036");
            if (root == null || tip == null)
            {
                firingClawReachScale = 1f;
                return;
            }

            float authoredReach = Vector3.Distance(root.position, tip.position);
            float requiredReach = Vector3.Distance(root.position, impactPosition);
            if (authoredReach <= 0.0001f)
            {
                firingClawReachScale = 1f;
                return;
            }

            firingClawReachScale = Mathf.Clamp(
                requiredReach / authoredReach,
                minimumReachScale,
                maximumReachScale);
        }

        private void ResetFiringClawTransform()
        {
            if (firingClawInstance == null)
            {
                return;
            }

            firingClawInstance.transform.localPosition = Vector3.zero;
            firingClawInstance.transform.localRotation = Quaternion.identity;
            firingClawInstance.transform.localScale = firingClawAuthoredScale;
            firingClawReachScale = 1f;
        }

        private void HideOriginalFiringClaw()
        {
            if (originalFiringClawHidden)
            {
                return;
            }

            originalFiringClawRoot = transform.Find(
                "UniRigArmature/Bone_000/Bone_001/Bone_039");
            if (originalFiringClawRoot == null)
            {
                return;
            }

            originalFiringClawScale = originalFiringClawRoot.localScale;
            originalFiringClawRoot.localScale = Vector3.zero;
            originalFiringClawHidden = true;
            animator.enabled = false;
        }

        private void RestoreOriginalFiringClaw()
        {
            if (!originalFiringClawHidden || originalFiringClawRoot == null)
            {
                return;
            }

            originalFiringClawRoot.localScale = originalFiringClawScale;
            originalFiringClawHidden = false;
        }

        private void SetClawControlsForCurrentPose()
        {
            Transform root = FindClawBone("Bone_039");
            Transform mid = FindClawBone("Bone_038");
            Transform tip = FindClawBone("Bone_036");
            Transform target = firingClawInstance.transform.Find("Animation Rig/IK Target");
            Transform hint = firingClawInstance.transform.Find("Animation Rig/IK Hint");
            if (root == null || mid == null || tip == null || target == null || hint == null)
            {
                return;
            }

            target.position = tip.position;
            target.rotation = tip.rotation;
            Vector3 bendNormal = Vector3.Cross(mid.position - root.position, tip.position - mid.position).normalized;
            hint.position = mid.position + bendNormal * 0.5f;
            hint.rotation = Quaternion.identity;
        }

        private IEnumerator MoveClawTargetAlongArc(
            Vector3 endPosition,
            float durationSeconds,
            bool isAttackLunge,
            bool isReturning = false)
        {
            Transform target = firingClawInstance.transform.Find("Animation Rig/IK Target");
            if (target == null)
            {
                yield break;
            }

            Vector3 startPosition = target.position;
            Vector3 end = endPosition + Vector3.up * impactHeightMeters;
            if (durationSeconds <= 0f)
            {
                target.position = end;
                if (isAttackLunge)
                {
                    ApplyFiringClawScale(1f);
                }
                else if (isReturning)
                {
                    ApplyFiringClawScale(0f);
                }

                yield break;
            }

            float arcHeight = Mathf.Max(
                minimumArcHeightMeters,
                Vector3.Distance(startPosition, end) * 0.2f);
            Vector3 apex = Vector3.Lerp(startPosition, end, 0.45f) + Vector3.up * arcHeight;
            if (isAttackLunge)
            {
                yield return RaiseThenStrike(target, startPosition, apex, end, durationSeconds);
                yield break;
            }

            float elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
                target.position = CalculateQuadraticBezier(startPosition, apex, end, progress);
                if (isReturning)
                {
                    ApplyFiringClawScale(1f - Mathf.SmoothStep(0f, 1f, progress));
                }

                yield return null;
            }

            target.position = end;
            if (isReturning)
            {
                ApplyFiringClawScale(0f);
            }
        }

        private IEnumerator RaiseThenStrike(
            Transform target,
            Vector3 startPosition,
            Vector3 apex,
            Vector3 impactPosition,
            float durationSeconds)
        {
            float raiseDuration = durationSeconds * raiseFractionOfLunge;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < raiseDuration)
            {
                elapsedSeconds += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedSeconds / raiseDuration);
                float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
                target.position = Vector3.Lerp(startPosition, apex, easedProgress);
                ApplyFiringClawScale(raisedScaleFraction * easedProgress);
                yield return null;
            }

            target.position = apex;
            ApplyFiringClawScale(raisedScaleFraction);

            float strikeDuration = durationSeconds - raiseDuration;
            elapsedSeconds = 0f;
            while (elapsedSeconds < strikeDuration)
            {
                elapsedSeconds += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsedSeconds / strikeDuration);
                target.position = Vector3.Lerp(apex, impactPosition, progress * progress);
                float fastScaleProgress = 1f - Mathf.Pow(1f - progress, 3f);
                ApplyFiringClawScale(Mathf.Lerp(
                    raisedScaleFraction,
                    1f,
                    fastScaleProgress));
                yield return null;
            }

            target.position = impactPosition;
            ApplyFiringClawScale(1f);
        }

        private void ApplyFiringClawScale(float progress)
        {
            firingClawInstance.transform.localScale = firingClawAuthoredScale * Mathf.Lerp(
                1f,
                firingClawReachScale,
                progress);
        }

        private Vector3 GetOriginalTipPosition()
        {
            Transform tip = transform.Find(
                "UniRigArmature/Bone_000/Bone_001/Bone_039/Bone_038/Bone_037/Bone_036");
            return tip != null ? tip.position : transform.position;
        }

        private Transform FindClawBone(string boneName)
        {
            Transform[] bones = firingClawInstance.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < bones.Length; index++)
            {
                if (bones[index].name == boneName)
                {
                    return bones[index];
                }
            }

            return null;
        }

        private void SetFiringClawVisible(bool visible)
        {
            if (firingClawInstance != null)
            {
                firingClawInstance.SetActive(visible);
            }
        }

        private static Vector3 CalculateQuadraticBezier(
            Vector3 start,
            Vector3 control,
            Vector3 end,
            float progress)
        {
            float inverse = 1f - progress;
            return inverse * inverse * start + 2f * inverse * progress * control + progress * progress * end;
        }

        private static void CopyPose(Transform source, Transform destination)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
            foreach (Transform sourceChild in source)
            {
                Transform destinationChild = destination.Find(sourceChild.name);
                if (destinationChild != null)
                {
                    CopyPose(sourceChild, destinationChild);
                }
            }
        }
    }
}
