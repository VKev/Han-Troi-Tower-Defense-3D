namespace TowerDefense3D.GameFlow
{
    public readonly struct PauseMenuHudState
    {
        public PauseMenuHudState(bool isVisible)
        {
            IsVisible = isVisible;
        }

        /// <summary>Whether the menu is on screen, which is exactly while the game is paused.</summary>
        public bool IsVisible { get; }
    }
}
