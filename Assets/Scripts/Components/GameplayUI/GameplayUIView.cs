using UnityEngine;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Authored root boundary for the level-owned gameplay HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayUIView : MonoBehaviour, IGameplayUIView
    {
        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            gameObject.SetActive(true);
        }
    }
}
