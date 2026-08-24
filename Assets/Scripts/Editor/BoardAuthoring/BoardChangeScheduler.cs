using System;
using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEditor.Compilation;

namespace TowerDefense3D.GridPlacement.Editor
{
    [InitializeOnLoad]
    internal static class BoardChangeScheduler
    {
        private static readonly HashSet<BoardDefinition> PendingBoards =
            new HashSet<BoardDefinition>();

        private static bool flushScheduled;

        static BoardChangeScheduler()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            CompilationPipeline.compilationFinished += HandleCompilationFinished;
        }

        public static void Queue(BoardDefinition board)
        {
            if (board == null)
            {
                return;
            }

            PendingBoards.Add(board);
            ScheduleFlush();
        }

        private static void ScheduleFlush()
        {
            if (flushScheduled)
            {
                return;
            }

            flushScheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            flushScheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling)
            {
                return;
            }

            if (EditorApplication.isUpdating)
            {
                ScheduleFlush();
                return;
            }

            if (PendingBoards.Count == 0)
            {
                return;
            }

            var boards = new BoardDefinition[PendingBoards.Count];
            PendingBoards.CopyTo(boards);
            PendingBoards.Clear();

            for (int index = 0; index < boards.Length; index++)
            {
                BoardDefinition board = boards[index];
                if (board != null)
                {
                    BoardSceneSynchronizer.Synchronize(board);
                }
            }
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && PendingBoards.Count > 0)
            {
                ScheduleFlush();
            }
        }

        private static void HandleCompilationFinished(object context)
        {
            if (PendingBoards.Count > 0)
            {
                ScheduleFlush();
            }
        }
    }

    internal sealed class BoardDefinitionPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            QueueBoards(importedAssets);
            QueueBoards(movedAssets);
        }

        private static void QueueBoards(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return;
            }

            for (int index = 0; index < assetPaths.Length; index++)
            {
                string path = assetPaths[index];
                if (!string.Equals(
                        System.IO.Path.GetExtension(path),
                        ".asset",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BoardDefinition board =
                    AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
                BoardChangeScheduler.Queue(board);
            }
        }
    }
}
