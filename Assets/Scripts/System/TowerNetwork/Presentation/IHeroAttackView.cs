using System;
using TowerDefense3D.Enemies;

namespace TowerDefense3D.Towers
{
    public interface IHeroAttackView
    {
        event Action HitEffectPlayed;
        void SetAnimationSpeed(float speed);
        void PlayAttack(HeroAttackEvent attack);
    }
}
