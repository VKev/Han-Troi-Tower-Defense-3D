using TowerDefense3D.Enemies;

namespace TowerDefense3D.Towers
{
    public interface IHeroAttackView
    {
        void PlayAttack(HeroAttackEvent attack);
    }
}
