using System.Collections.Generic;

namespace TowerDefense3D.Towers
{
    public interface ITowerNetworkSceneRegistry
    {
        IReadOnlyList<TowerRuntimeView> CreateTowerViewSnapshot();
        bool TryGetTowerView(TowerNodeId nodeId, out TowerRuntimeView view);
        bool TryRewire(TowerRuntimeView source, TowerRuntimeView target, out string error);
    }
}
