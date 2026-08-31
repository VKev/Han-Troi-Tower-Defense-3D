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
        [SerializeField, Min(0f)] private float impactHeightMeters = 0.65f;
        [SerializeField, Min(0f)] private float minimumArcHeightMeters = 0.75f;

        private Animator animator;
        private TowerRuntimeView towerView;
        private GameObject firingClawInstance;
        private Coroutine sequence;
        private Vector3 aimPosition;
        private bool isAiming;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            towerView = GetComponentInParent<TowerRuntimeView>();
        }

        private void OnDisable()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            isAiming = false;
            SetFiringClawVisible(false);
        }

        private void Update()
        {
            if (isAiming && towerView != null)
            {
                towerView.FaceTowards(aimPosition);
            }
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
                yield return MoveClawTargetAlongArc(attack.ImpactPosition, attack.LungeDurationSeconds);
                yield return new WaitForSeconds(attack.ImpactHoldDurationSeconds);
                yield return MoveClawTargetAlongArc(GetOriginalTipPosition(), attack.ReturnDurationSeconds);
            }

            SetFiringClawVisible(false);
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
            firingClawInstance.transform.localScale = Vector3.one;
            CopyPose(sourceSkeleton, clawSkeleton);
            SetClawControlsForCurrentPose();
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
            SetFiringClawVisible(false);
            Animator clawAnimator = firingClawInstance.GetComponent<Animator>();
            if (clawAnimator != null)
            {
                clawAnimator.runtimeAnimatorController = null;
            }
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

        private IEnumerator MoveClawTargetAlongArc(Vector3 endPosition, float durationSeconds)
        {
            Transform target = firingClawInstance.transform.Find("Animation Rig/IK Target");
            if (target == null || durationSeconds <= 0f)
            {
                yield break;
            }

            Vector3 startPosition = target.position;
            Vector3 end = endPosition + Vector3.up * impactHeightMeters;
            float arcHeight = Mathf.Max(
                minimumArcHeightMeters,
                Vector3.Distance(startPosition, end) * 0.2f);
            Vector3 control = Vector3.Lerp(startPosition, end, 0.5f) + Vector3.up * arcHeight;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                target.position = CalculateQuadraticBezier(
                    startPosition,
                    control,
                    end,
                    Mathf.Clamp01(elapsedSeconds / durationSeconds));
                yield return null;
            }

            target.position = end;
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
