using System.Collections.Generic;

namespace TowerDefense3D.Enemies
{
    public interface IEnemyViewPool
    {
        void Initialize();
        void Spawn(EnemySnapshot enemy);
        void Despawn(long enemyId);
        void Render(IReadOnlyList<EnemySnapshot> enemies, float interpolationAlpha);
        void ReleaseAll();
    }
}
