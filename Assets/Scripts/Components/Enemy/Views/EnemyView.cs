using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class EnemyView : MonoBehaviour
    {
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");

        private Animator animator;

        public long EnemyId { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Bind(EnemySnapshot enemy)
        {
            EnemyId = enemy.EnemyId;
            string prefix = enemy.IsSummoned ? "Summoned Enemy" : "Enemy";
            gameObject.name = $"{prefix} {enemy.EnemyId} - {enemy.Definition.DisplayName}";
            transform.position = enemy.Position;
            gameObject.SetActive(true);
            SetMoving(true);
        }

        public void Render(EnemySnapshot enemy, float interpolationAlpha)
        {
            transform.position = Vector3.Lerp(
                enemy.PreviousPosition,
                enemy.Position,
                interpolationAlpha);
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
