using System;
using System.Collections.Generic;

namespace TowerDefense3D.GameFlow
{
    /// <summary>
    /// Projects application-flow state and callbacks onto the authored application view.
    /// </summary>
    public sealed class ApplicationUISystem : IDisposable
    {
        private readonly IApplicationUIView view;

        public ApplicationUISystem(IApplicationUIView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public bool IsStarted { get; private set; }

        public void Start()
        {
            view.Reset();
            IsStarted = true;
        }

        public void Dispose()
        {
            view.Reset();
            IsStarted = false;
        }

        public void ShowLevelMenu(IReadOnlyList<LevelMenuItemState> levels, Action<int> onLevelSelected)
        {
            view.ShowLevelMenu(levels, onLevelSelected);
        }

        public void HideLevelMenu()
        {
            view.HideLevelMenu();
        }

        public void ShowLoading(string message)
        {
            view.ShowLoading(message);
        }

        public void HideLoading()
        {
            view.HideLoading();
        }

        public void ShowBlockingError(string message, Action retry, Action startNew)
        {
            view.ShowBlockingError(message, retry, startNew);
        }

        public void HideBlockingError()
        {
            view.HideBlockingError();
        }

        public void ShowSaveWarning(string message, Action retrySave)
        {
            view.ShowSaveWarning(message, retrySave);
        }

        public void HideSaveWarning()
        {
            view.HideSaveWarning();
        }

        public void SetInputBlocked(bool isBlocked)
        {
            view.SetInputBlocked(isBlocked);
        }
    }
}
