using System;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Builds the level-owned runtime board and projects authored visibility to its Unity view.
    /// </summary>
    public sealed class BoardSystem
    {
        private readonly IBoardView view;

        public BoardSystem(IBoardView view)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            if (view.Board == null)
            {
                throw new InvalidOperationException("BoardView requires a BoardDefinition.");
            }

            Board = new GridBoard(view.Board, view.WorldOrigin);
        }

        public GridBoard Board { get; }
        public BoardDefinition Definition => view.Board;

        public void Start()
        {
            view.ApplyVisibility(view.Board.VisualizeInScene);
        }
    }
}
