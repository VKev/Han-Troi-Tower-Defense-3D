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

        private Animator animator;
        private EnemyDamageFlashView damageFlashView;
        private EnemyElementStatusView elementStatusView;
        private Quaternion spawnLocalRotation;
        private bool hasFacingDirection;

        public long EnemyId { get; private set; }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            damageFlashView = GetComponent<EnemyDamageFlashView>();
            elementStatusView = GetComponentInChildren<EnemyElementStatusView>(true);
            spawnLocalRotation = transform.localRotation;
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
            SetMoving(true);
            GetDamageFlashView().Bind(enemy);
            GetElementStatusView().Bind(enemy.ElementState);
        }

        public void Render(EnemySnapshot enemy, float interpolationAlpha)
        {
            GetDamageFlashView().Render(enemy, Time.deltaTime);
            GetElementStatusView().Render(enemy.ElementState, Time.deltaTime);
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

            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            if (!hasFacingDirection)
            {
                transform.rotation = targetRotation;
                hasFacingDirection = true;
                return;
            }

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                TurnSpeedDegreesPerSecond * Time.deltaTime);
        }

        public void Release()
        {
            SetMoving(false);
            GetDamageFlashView().Release();
            GetElementStatusView().Release();
            EnemyId = 0L;
            hasFacingDirection = false;
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

        private EnemyDamageFlashView GetDamageFlashView()
        {
            if (damageFlashView == null)
            {
                damageFlashView = GetComponent<EnemyDamageFlashView>();
            }

            return damageFlashView;
        }

        public void ShowReaction(ElementPair pair)
        {
            GetElementStatusView().ShowReaction(pair);
        }

        private EnemyElementStatusView GetElementStatusView()
        {
            if (elementStatusView == null)
            {
                elementStatusView = GetComponentInChildren<EnemyElementStatusView>(true);
            }

            return elementStatusView;
        }
    }
}
