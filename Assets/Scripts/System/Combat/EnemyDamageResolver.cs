using UnityEngine;
using TowerDefense3D.Towers;

namespace TowerDefense3D.Enemies
{
    public static class EnemyDamageResolver
    {
        public const float MinimumResistancePoints = -100f;
        public const float MaximumResistancePoints = 90f;

        public static float Resolve(
            float rawDamage,
            DamageType damageType,
            EnemyDefinition enemy,
            float resistanceModifierPoints = 0f)
        {
            if (damageType == DamageType.True)
            {
                return rawDamage;
            }

            float baseResistance = damageType == DamageType.Physical
                ? enemy.BasePhysicalResistance
                : enemy.BaseMagicResistance;
            float effectiveResistance = Mathf.Clamp(
                baseResistance + resistanceModifierPoints,
                MinimumResistancePoints,
                MaximumResistancePoints);
            return rawDamage * (1f - effectiveResistance / 100f);
        }

        public static ResolvedDamage Resolve(
            DamageChannels rawDamage,
            EnemyDefinition enemy,
            float physicalResistanceModifierPoints = 0f,
            float magicResistanceModifierPoints = 0f)
        {
            return new ResolvedDamage(
                Resolve(
                    rawDamage.Physical,
                    DamageType.Physical,
                    enemy,
                    physicalResistanceModifierPoints),
                Resolve(
                    rawDamage.Magic,
                    DamageType.Magic,
                    enemy,
                    magicResistanceModifierPoints),
                rawDamage.True);
        }
    }
}
