using System;
using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    /// <summary>
    /// Marks every tower that is not part of a chain that works, and clears the mark the moment
    /// one does.
    /// </summary>
    /// <remarks>
    /// Driven off the network's own state change rather than polled: a chain becomes valid or
    /// stops being valid when a link is made, cut or rewired, and that is exactly what raises the
    /// event. Every tower is answered on each change rather than only the ones that moved, because
    /// one link can turn a whole route on or off behind it.
    /// </remarks>
    public sealed class TowerChainStatePresentationSystem : IDisposable
    {
        /// <summary>
        /// Whether the invalid-chain warning sign is shown at all.
        /// </summary>
        /// <remarks>
        /// Off for now, by request: every tower is reported as valid so no tower carries the sign,
        /// whatever its chain is doing. The rest of the feature is left standing - turning this
        /// back to true is the whole of switching it back on.
        /// </remarks>
        private const bool InvalidChainWarningEnabled = false;

        private readonly TowerNetworkSystem towerNetworkSystem;

        private bool isStarted;

        public TowerChainStatePresentationSystem(TowerNetworkSystem towerNetworkSystem)
        {
            this.towerNetworkSystem = towerNetworkSystem
                ?? throw new ArgumentNullException(nameof(towerNetworkSystem));
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            towerNetworkSystem.StateChanged += Refresh;
            isStarted = true;
            Refresh();
        }

        public void Dispose()
        {
            if (!isStarted)
            {
                return;
            }

            towerNetworkSystem.StateChanged -= Refresh;
            isStarted = false;
        }

        private void Refresh()
        {
            IReadOnlyList<ITowerRuntimeView> towers = towerNetworkSystem.CreateTowerViewSnapshot();
            for (int index = 0; index < towers.Count; index++)
            {
                ITowerRuntimeView tower = towers[index];
                if (tower == null)
                {
                    continue;
                }

                tower.SetChainValid(
                    !InvalidChainWarningEnabled
                    || IsAlwaysLit(tower)
                    || towerNetworkSystem.IsInValidChain(tower));
            }
        }

        /// <summary>
        /// Towers the mark does not apply to, whatever the network says.
        /// </summary>
        /// <remarks>
        /// A hero fights on its own - it needs no ammo routed to it - so it is never idle for want
        /// of a chain. Marking it would be telling the player something untrue about a tower that
        /// is working perfectly well.
        /// </remarks>
        private static bool IsAlwaysLit(ITowerRuntimeView tower)
        {
            return tower.CombatDefinition?.Family == TowerFamily.Hero;
        }
    }
}
