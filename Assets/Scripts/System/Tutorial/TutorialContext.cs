using System;
using System.Collections.Generic;

namespace TowerDefense3D.Tutorials
{
    public sealed class TutorialContext
    {
        private readonly Func<string, bool> targetExists;
        private readonly Func<string, object> targetValue;
        private readonly Func<string, bool> condition;
        private readonly Func<bool> pointerPressed;
        private readonly Action<TutorialGameplayUiMode> setGameplayUiMode;

        public TutorialContext(
            int levelNumber,
            Func<string, bool> targetExists,
            Func<string, object> targetValue = null,
            Func<string, bool> condition = null,
            Action<TutorialGameplayUiMode> setGameplayUiMode = null,
            Func<bool> pointerPressed = null)
        {
            if (levelNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelNumber));
            }

            LevelNumber = levelNumber;
            this.targetExists = targetExists ?? throw new ArgumentNullException(nameof(targetExists));
            this.targetValue = targetValue ?? (_ => null);
            this.condition = condition ?? (_ => false);
            this.setGameplayUiMode = setGameplayUiMode ?? (_ => { });
            this.pointerPressed = pointerPressed ?? (() => false);
        }

        public int LevelNumber { get; }
        public bool HasTarget(string id) => targetExists(id);
        public object GetTarget(string id) => targetValue(id);
        public bool IsTrue(string id) => condition(id);
        public bool WasPointerPressed => pointerPressed();
        public void SetGameplayUiMode(TutorialGameplayUiMode mode) => setGameplayUiMode(mode);
    }
}
