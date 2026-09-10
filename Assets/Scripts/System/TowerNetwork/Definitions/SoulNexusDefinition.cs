using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [CreateAssetMenu(
        fileName = "SoulNexus",
        menuName = "Tower Defense/Towers/Sinks/Soul Nexus")]
    public sealed class SoulNexusDefinition : TowerCombatDefinition
    {
        [SerializeField] private TowerCoreProfile core = new TowerCoreProfile(
            "soul_nexus",
            "Soul Nexus",
            new TowerNetworkProfile(2, 0, 0, false),
            new TowerThroughputProfile(0.75f, 1, 1),
            new TowerEconomyProfile(0, 1, false));
        [SerializeField, Min(1)] private int maximumSoul = 50;
        [SerializeField] private bool soulUsesUniqueDirectHitCount = true;
        [SerializeField] private bool consumesWhenSoulIsFull = true;
        [SerializeField] private bool discardsOverflowSoul = true;
        [SerializeField] private bool emptyInputDoesNotBlockOtherInput = true;

        public override TowerFamily Family => TowerFamily.SoulNexus;
        public override TowerNetworkRole NetworkRole => TowerNetworkRole.Sink;
        public override TowerCoreProfile Core => core;
        public int MaximumSoul => maximumSoul;
        public bool SoulUsesUniqueDirectHitCount => soulUsesUniqueDirectHitCount;
        public bool ConsumesWhenSoulIsFull => consumesWhenSoulIsFull;
        public bool DiscardsOverflowSoul => discardsOverflowSoul;
        public bool EmptyInputDoesNotBlockOtherInput => emptyInputDoesNotBlockOtherInput;

        internal override void CollectSpecificValidationErrors(List<string> errors)
        {
            if (maximumSoul <= 0)
            {
                errors.Add("Soul Nexus maximum Soul must be greater than zero.");
            }

            if (!soulUsesUniqueDirectHitCount || !consumesWhenSoulIsFull ||
                !discardsOverflowSoul)
            {
                errors.Add("Soul Nexus must use unique direct hits and consume/discard overflow when full.");
            }
        }
    }
}
