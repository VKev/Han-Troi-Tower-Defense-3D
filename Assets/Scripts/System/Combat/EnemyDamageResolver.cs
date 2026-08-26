using UnityEngine;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    public static class EnemyDamageResolver
    {
        public const float MinimumResistance = -1f;
        public const float MaximumResistance = 0.9f;

        public static float Resolve(
            float rawDamage,
            DamageType damageType,
            EnemyDefinition enemy,
            float resistanceModifier = 0f)
        {
            if (damageType == DamageType.True)
            {
                return rawDamage;
            }

            float baseResistance = damageType == DamageType.Physical
                ? enemy.BasePhysicalResistance
                : enemy.BaseMagicResistance;
            float effectiveResistance = Mathf.Clamp(
                baseResistance + resistanceModifier,
                MinimumResistance,
                MaximumResistance);
            return rawDamage * (1f - effectiveResistance);
        }
    }
}
