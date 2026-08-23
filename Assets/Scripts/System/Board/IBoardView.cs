using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    public interface IBoardView
    {
        BoardDefinition Board { get; }
        Vector3 WorldOrigin { get; }

        void ApplyVisibility(bool visible);
    }
}
