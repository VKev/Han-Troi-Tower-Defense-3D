using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        private Animator animator;
        private Quaternion authoredRotationOffset;

        public long EnemyId { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            authoredRotationOffset = transform.localRotation;
        }

        public void Bind(EnemySnapshot enemy)
        {
            EnemyId = enemy.EnemyId;
            string prefix = enemy.IsSummoned ? "Summoned Enemy" : "Enemy";
            gameObject.name = $"{prefix} {enemy.EnemyId} - {enemy.Definition.DisplayName}";
            transform.position = enemy.Position;
            transform.localRotation = authoredRotationOffset;
            gameObject.SetActive(true);
            SetMoving(true);
        }

        public void Render(EnemySnapshot enemy, float interpolationAlpha)
        {
            transform.position = Vector3.Lerp(
                enemy.PreviousPosition,
                enemy.Position,
                interpolationAlpha);

            Vector3 movement = enemy.Position - enemy.PreviousPosition;
            movement.y = 0f;
            if (movement.sqrMagnitude == 0f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(movement, Vector3.up) * authoredRotationOffset;
        }

        public void Release()
        {
            SetMoving(false);
            EnemyId = 0L;
            gameObject.SetActive(false);
        }

        private void SetMoving(bool value)
        {
            Animator targetAnimator = animator != null ? animator : GetComponent<Animator>();
            if (targetAnimator.runtimeAnimatorController != null)
            {
                targetAnimator.SetBool(IsMoving, value);
            }
        }
    }
}
