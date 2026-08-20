using System;
using UnityEngine;

namespace TowerDefense3D.Towers
{
    [Serializable]
    public sealed class ElementUpgradeCostProfile
    {
        [SerializeField, Min(0)] private int tierOneCost = 60;
        [SerializeField, Min(0)] private int tierTwoCost = 90;
        [SerializeField, Min(0)] private int tierThreeCost = 140;

        public int TierOneCost => tierOneCost;
        public int TierTwoCost => tierTwoCost;
        public int TierThreeCost => tierThreeCost;
    }
}
