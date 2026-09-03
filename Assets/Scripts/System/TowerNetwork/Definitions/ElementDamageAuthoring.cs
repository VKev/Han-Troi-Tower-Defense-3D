using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Shared authoring rules for the damage knobs every element tower carries.
    /// </summary>
    /// <remarks>
    /// Deliberately permits zero. Fire demands positive damage because damage *is* Fire, but
    /// Water and Wind lead with reveal and push, so their damage columns start at zero and a
    /// designer raises them from the Game Balance Center. A validator that rejected zero would
    /// stop every level from loading the moment those towers were given the columns at all.
    ///
    /// What it does catch is a half-authored burn: a Damage Per Tick with no interval or no
    /// duration silently never ticks, which reads in game as "the number I typed did nothing".
    /// </remarks>
    internal static class ElementDamageAuthoring
    {
        public static void CollectErrors(
            string towerLabel,
            DamageProfile directDamage,
            BurnProfile burn,
            ICollection<string> errors)
        {
            if (directDamage == null)
            {
                errors.Add($"{towerLabel} direct damage profile is missing.");
            }
            else if (directDamage.Amount < 0f)
            {
                errors.Add($"{towerLabel} direct damage cannot be negative.");
            }

            if (burn == null)
            {
                errors.Add($"{towerLabel} burn profile is missing.");
                return;
            }

            if (burn.DamagePerTick < 0f)
            {
                errors.Add($"{towerLabel} burn damage per tick cannot be negative.");
            }

            if (burn.DamagePerTick > 0f
                && (burn.TickIntervalSeconds <= 0f || burn.DurationSeconds <= 0f))
            {
                errors.Add(
                    $"{towerLabel} burn deals damage per tick, so it needs a positive tick "
                    + "interval and duration - otherwise it never ticks.");
            }
        }
    }
}
