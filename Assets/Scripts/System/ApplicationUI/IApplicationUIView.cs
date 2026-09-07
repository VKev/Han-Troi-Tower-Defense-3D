using System;
using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Renders application UI state and forwards authored UI callbacks.
    /// </summary>
    public interface IApplicationUIView
    {
        void Reset();
        void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected);
        void HideLevelMenu();
        void ShowLoading(string message);
        void HideLoading();

        /// <summary>
        /// Fades the screen to black and reports once it is covered. This is what hides a scene
        /// swap on the transitions that have one, in place of a loading panel.
        /// </summary>
        /// <remarks>
        /// The callback always fires, including when no curtain is authored - a caller that waits
        /// for the cover before loading must never be left waiting on a view that has nothing to
        /// fade.
        /// </remarks>
        void CoverScreen(Action onCovered);

        /// <summary>Fades the screen back in and reports once it is clear.</summary>
        void UncoverScreen(Action onUncovered);
        void ShowBlockingError(string message, Action retry, Action startNew);
        void HideBlockingError();
        void ShowSaveWarning(string message, Action retrySave);
        void HideSaveWarning();
        void SetInputBlocked(bool isBlocked);
    }
}
