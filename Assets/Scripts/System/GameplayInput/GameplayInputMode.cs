namespace TowerDefense3D.GameplayInput
{
    public enum GameplayInputMode
    {
        None,
        GridPlacement,
        TowerInteraction,

        /// <summary>
        /// A camera pinch or pan owns the pointer. Placement and tower picking already stand down
        /// for any mode that is not their own, so claiming this suppresses both.
        /// </summary>
        CameraGesture
    }
}
