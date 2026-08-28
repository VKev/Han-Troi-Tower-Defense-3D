using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public interface IEnemyViewPool
    {
        void Spawn(EnemySnapshot enemy);
        void Despawn(long enemyId);
        void ShowReaction(long enemyId, ElementReactionEvent reaction);
        void Render(IReadOnlyList<EnemySnapshot> enemies, float interpolationAlpha);
        void ReleaseAll();
    }
}
