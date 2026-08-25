using UnityEngine;

namespace TowerDefense3D.Enemies
{
    [DisallowMultipleComponent]
    public sealed class EnemyView : MonoBehaviour
    {
        public long EnemyId { get; private set; }

        public void Bind(EnemySnapshot enemy)
        {
            EnemyId = enemy.EnemyId;
            string prefix = enemy.IsSummoned ? "Summoned Enemy" : "Enemy";
            gameObject.name = $"{prefix} {enemy.EnemyId} - {enemy.Definition.DisplayName}";
            transform.position = enemy.Position;
            gameObject.SetActive(true);
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
            EnemyId = 0L;
            gameObject.SetActive(false);
        }
    }
}
