using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public sealed class BoardPainterWindow : EditorWindow
    {
        private const float HeaderSize = 28f;
        private const float MinimumCellSize = 18f;
        private const float MaximumCellSize = 56f;
        private static readonly string[] BrushSizeLabels =
            { "1 x 1", "3 x 3", "5 x 5" };
        private static readonly int[] BrushSizes = { 1, 3, 5 };

        private BoardDefinition boardAsset;
        private BoardAuthoringDocument document;
        private BoardPaintPreset selectedPreset = BoardPaintPreset.Buildable;
        private int brushSize = 1;
        private Vector2 scrollPosition;
        private int selectedLevel;
        private int pendingWidth = 1;
        private int pendingDepth = 1;
        private int pendingHeight = 1;
        private float pendingCellSize = 1f;
        private float pendingHeightUnit = 1f;
        private int pendingMaxCameraGridXSpan;
        private int pendingMaxCameraGridYSpan;
        private float visualCellSize = 32f;
        private bool strokeActive;
        private bool strokeChanged;
        private GridCell lastPaintedCell;
        private int gridControlId;

        [MenuItem("Tools/Tower Defense/Board Painter")]
        public static void Open()
        {
            GetWindow<BoardPainterWindow>("Board Painter");
        }

        public static void Open(BoardDefinition board)
        {
            BoardPainterWindow window = GetWindow<BoardPainterWindow>("Board Painter");
            window.SetBoard(board);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            Undo.undoRedoPerformed += HandleUndoRedo;
            if (boardAsset != null)
            {
                SetBoard(boardAsset);
            }
        }

        private void OnDisable()
        {
            CommitStroke();
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        private void OnLostFocus()
        {
            CommitStroke();
        }

        private void OnGUI()
        {
            DrawBoardSelector();
            if (document == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a BoardDefinition asset to begin authoring.",
                    MessageType.Info);
                return;
            }

            DrawBoardSettings();
            DrawLayerSelector();
            DrawPalette();
            DrawGrid();
            DrawStatus();
        }

        private void DrawBoardSelector()
        {
            EditorGUI.BeginChangeCheck();
            BoardDefinition selected = (BoardDefinition)EditorGUILayout.ObjectField(
                "Board Asset",
                boardAsset,
                typeof(BoardDefinition),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                CommitStroke();
                SetBoard(selected);
            }
        }

        private void DrawBoardSettings()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Board Dimensions", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                pendingWidth = Mathf.Max(1, EditorGUILayout.IntField("Width (X)", pendingWidth));
                pendingDepth = Mathf.Max(1, EditorGUILayout.IntField("Depth (Z)", pendingDepth));
                pendingHeight = Mathf.Max(1, EditorGUILayout.IntField("Levels (Y)", pendingHeight));
                if (GUILayout.Button("Apply Resize", GUILayout.Width(110f)))
                {
                    ApplyResize();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                pendingCellSize = Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField("Cell Size", pendingCellSize));
                pendingHeightUnit = Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField("Height Unit", pendingHeightUnit));
                if (GUILayout.Button("Apply Metrics", GUILayout.Width(110f)))
                {
                    document.SetMetrics(pendingCellSize, pendingHeightUnit);
                    document.Commit("Change Board Metrics");
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Camera Framing Limits", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                pendingMaxCameraGridXSpan = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        "Max Width (Grid X)",
                        pendingMaxCameraGridXSpan));
                pendingMaxCameraGridYSpan = Mathf.Max(
                    0,
                    EditorGUILayout.IntField(
                        "Max Height (Grid Y)",
                        pendingMaxCameraGridYSpan));
                if (GUILayout.Button("Apply Camera Limits", GUILayout.Width(140f)))
                {
                    document.SetCameraGridSpans(
                        pendingMaxCameraGridXSpan,
                        pendingMaxCameraGridYSpan);
                    document.Commit("Change Camera Limits");
                    SyncPendingValues();
                }
            }

            EditorGUILayout.LabelField(
                "0 = Unlimited. Grid Y maps to world Z.",
                EditorStyles.miniLabel);
        }

        private void DrawLayerSelector()
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = selectedLevel > 0;
                if (GUILayout.Button("<", GUILayout.Width(32f)))
                {
                    selectedLevel--;
                }

                GUI.enabled = true;
                int maximumLevel = Mathf.Max(0, document.Dimensions.Height - 1);
                selectedLevel = EditorGUILayout.IntSlider(
                    $"Level Y = {selectedLevel} / {maximumLevel}",
                    selectedLevel,
                    0,
                    maximumLevel);

                GUI.enabled = selectedLevel < maximumLevel;
                if (GUILayout.Button(">", GUILayout.Width(32f)))
                {
                    selectedLevel++;
                }

                GUI.enabled = true;
                visualCellSize = EditorGUILayout.Slider(
                    "Zoom",
                    visualCellSize,
                    MinimumCellSize,
                    MaximumCellSize,
                    GUILayout.MaxWidth(280f));
            }
        }

        private void DrawPalette()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (BoardPaintPreset preset in System.Enum.GetValues(typeof(BoardPaintPreset)))
                {
                    Color previous = GUI.backgroundColor;
                    GUI.backgroundColor = BoardPaintPresetUtility.GetColor(preset);
                    bool selected = selectedPreset == preset;
                    if (GUILayout.Toggle(
                            selected,
                            BoardPaintPresetUtility.GetLabel(preset),
                            "Button",
                            GUILayout.MinWidth(96f)))
                    {
                        selectedPreset = preset;
                    }

                    GUI.backgroundColor = previous;
                }
            }

            brushSize = EditorGUILayout.IntPopup(
                "Brush Size",
                brushSize,
                BrushSizeLabels,
                BrushSizes,
                GUILayout.MaxWidth(240f));
            EditorGUILayout.LabelField(
                "Left-click/drag paints the selected preset. Right-click/drag erases. "
                + "The brush is centered and clipped at Board edges. Z=0 is the bottom row.",
                EditorStyles.miniLabel);
        }

        private void DrawGrid()
        {
            GridDimensions dimensions = document.Dimensions;
            float contentWidth = HeaderSize + dimensions.Width * visualCellSize;
            float contentHeight = HeaderSize + dimensions.Depth * visualCellSize;

            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                GUILayout.ExpandHeight(true));
            Rect gridRect = GUILayoutUtility.GetRect(
                contentWidth,
                contentHeight,
                GUILayout.ExpandWidth(false),
                GUILayout.ExpandHeight(false));
            gridControlId = GUIUtility.GetControlID(
                "BoardPainterGrid".GetHashCode(),
                FocusType.Passive,
                gridRect);

            DrawAxisLabels(gridRect, dimensions);
            DrawCells(gridRect, dimensions);
            HandleGridInput(gridRect, dimensions, Event.current);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAxisLabels(Rect gridRect, GridDimensions dimensions)
        {
            GUI.Label(
                new Rect(gridRect.x, gridRect.y, HeaderSize, HeaderSize),
                "Z\\X",
                EditorStyles.centeredGreyMiniLabel);

            for (int x = 0; x < dimensions.Width; x++)
            {
                GUI.Label(
                    new Rect(
                        gridRect.x + HeaderSize + x * visualCellSize,
                        gridRect.y,
                        visualCellSize,
                        HeaderSize),
                    x.ToString(),
                    EditorStyles.centeredGreyMiniLabel);
            }

            for (int z = 0; z < dimensions.Depth; z++)
            {
                int row = dimensions.Depth - 1 - z;
                GUI.Label(
                    new Rect(
                        gridRect.x,
                        gridRect.y + HeaderSize + row * visualCellSize,
                        HeaderSize,
                        visualCellSize),
                    z.ToString(),
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawCells(Rect gridRect, GridDimensions dimensions)
        {
            for (int z = 0; z < dimensions.Depth; z++)
            {
                for (int x = 0; x < dimensions.Width; x++)
                {
                    GridCell coordinate = new GridCell(x, z, selectedLevel);
                    BoardCellFlags flags = document.GetFlags(coordinate);
                    BoardPaintPreset preset = BoardPaintPresetUtility.GetClosestPreset(flags);
                    Rect cellRect = GetCellRect(gridRect, dimensions, x, z);
                    EditorGUI.DrawRect(cellRect, BoardPaintPresetUtility.GetColor(preset));
                    GUI.Box(cellRect, GUIContent.none);

                    if (flags != BoardPaintPresetUtility.GetFlags(preset))
                    {
                        GUI.Label(cellRect, "?", EditorStyles.centeredGreyMiniLabel);
                    }
                    else if ((flags & BoardCellFlags.StaticBlocker) != 0)
                    {
                        GUI.Label(cellRect, "X", EditorStyles.whiteBoldLabel);
                    }

                    if (cellRect.Contains(Event.current.mousePosition))
                    {
                        GUI.Label(
                            cellRect,
                            new GUIContent(string.Empty, $"{coordinate}: {flags}"));
                    }
                }
            }
        }

        private void HandleGridInput(Rect gridRect, GridDimensions dimensions, Event current)
        {
            if (current.type == EventType.MouseDown
                && (current.button == 0 || current.button == 1)
                && TryGetCell(gridRect, dimensions, current.mousePosition, out GridCell coordinate))
            {
                strokeActive = true;
                strokeChanged = false;
                GUIUtility.hotControl = gridControlId;
                PaintCell(coordinate, current.button == 1 ? BoardPaintPreset.Empty : selectedPreset);
                current.Use();
            }
            else if (current.type == EventType.MouseDrag
                && strokeActive
                && GUIUtility.hotControl == gridControlId)
            {
                if (TryGetCell(
                        gridRect,
                        dimensions,
                        current.mousePosition,
                        out GridCell dragCoordinate))
                {
                    PaintCell(
                        dragCoordinate,
                        current.button == 1 ? BoardPaintPreset.Empty : selectedPreset);
                }

                current.Use();
            }
            else if (current.type == EventType.MouseUp
                && strokeActive
                && GUIUtility.hotControl == gridControlId)
            {
                GUIUtility.hotControl = 0;
                CommitStroke();
                current.Use();
            }
        }

        private void PaintCell(GridCell coordinate, BoardPaintPreset preset)
        {
            if (strokeChanged && coordinate == lastPaintedCell)
            {
                return;
            }

            lastPaintedCell = coordinate;
            strokeChanged |= PaintBrush(document, coordinate, brushSize, preset);
            Repaint();
        }

        internal static bool PaintBrush(
            BoardAuthoringDocument targetDocument,
            GridCell center,
            int size,
            BoardPaintPreset preset)
        {
            GridDimensions dimensions = targetDocument.Dimensions;
            int radius = size / 2;
            int minX = Mathf.Max(0, center.X - radius);
            int maxX = Mathf.Min(dimensions.Width - 1, center.X + radius);
            int minZ = Mathf.Max(0, center.Z - radius);
            int maxZ = Mathf.Min(dimensions.Depth - 1, center.Z + radius);
            bool changed = false;

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var coordinate = new GridCell(x, z, center.Y);
                    BoardCellFlags before = targetDocument.GetFlags(coordinate);
                    targetDocument.Paint(coordinate, preset);
                    changed |= before != targetDocument.GetFlags(coordinate);
                }
            }

            return changed;
        }

        private void CommitStroke()
        {
            if (!strokeActive)
            {
                return;
            }

            if (strokeChanged && document != null)
            {
                document.Commit("Paint Board Cells");
            }

            strokeActive = false;
            strokeChanged = false;
            if (GUIUtility.hotControl == gridControlId)
            {
                GUIUtility.hotControl = 0;
            }
        }

        private void ApplyResize()
        {
            var newDimensions = new GridDimensions(
                pendingWidth,
                pendingDepth,
                pendingHeight);
            int removedCells = document.CountCellsOutside(newDimensions);
            if (removedCells > 0
                && !EditorUtility.DisplayDialog(
                    "Resize Board",
                    $"This resize will remove {removedCells} authored cells outside the new bounds.",
                    "Resize and Remove",
                    "Cancel"))
            {
                return;
            }

            document.Resize(newDimensions);
            document.Commit("Resize Board");
            selectedLevel = Mathf.Clamp(selectedLevel, 0, newDimensions.Height - 1);
            SyncPendingValues();
        }

        private void DrawStatus()
        {
            IReadOnlyList<string> issues = document.Validate();
            EditorGUILayout.LabelField(
                $"Active cells: {document.ActiveCellCount}    "
                + $"Dimensions: {document.Dimensions}",
                EditorStyles.boldLabel);

            for (int i = 0; i < issues.Count; i++)
            {
                EditorGUILayout.HelpBox(issues[i], MessageType.Warning);
            }
        }

        private void SetBoard(BoardDefinition board)
        {
            boardAsset = board;
            document = board != null ? new BoardAuthoringDocument(board) : null;
            selectedLevel = 0;
            scrollPosition = Vector2.zero;
            SyncPendingValues();
            Repaint();
        }

        private void SyncPendingValues()
        {
            if (document == null)
            {
                return;
            }

            pendingWidth = document.Dimensions.Width;
            pendingDepth = document.Dimensions.Depth;
            pendingHeight = document.Dimensions.Height;
            pendingCellSize = document.CellSize;
            pendingHeightUnit = document.HeightUnit;
            pendingMaxCameraGridXSpan = document.MaxCameraGridXSpan;
            pendingMaxCameraGridYSpan = document.MaxCameraGridYSpan;
        }

        private void HandleUndoRedo()
        {
            if (document == null)
            {
                return;
            }

            document.Reload();
            BoardChangeScheduler.Queue(document.Asset);
            selectedLevel = Mathf.Clamp(selectedLevel, 0, document.Dimensions.Height - 1);
            SyncPendingValues();
            Repaint();
        }

        private Rect GetCellRect(Rect gridRect, GridDimensions dimensions, int x, int z)
        {
            int row = dimensions.Depth - 1 - z;
            const float gap = 1f;
            return new Rect(
                gridRect.x + HeaderSize + x * visualCellSize + gap,
                gridRect.y + HeaderSize + row * visualCellSize + gap,
                visualCellSize - gap * 2f,
                visualCellSize - gap * 2f);
        }

        private bool TryGetCell(
            Rect gridRect,
            GridDimensions dimensions,
            Vector2 mousePosition,
            out GridCell coordinate)
        {
            float localX = mousePosition.x - gridRect.x - HeaderSize;
            float localY = mousePosition.y - gridRect.y - HeaderSize;
            int x = Mathf.FloorToInt(localX / visualCellSize);
            int row = Mathf.FloorToInt(localY / visualCellSize);
            int z = dimensions.Depth - 1 - row;

            if (localX >= 0f && localY >= 0f
                && x >= 0 && x < dimensions.Width
                && z >= 0 && z < dimensions.Depth)
            {
                coordinate = new GridCell(x, z, selectedLevel);
                return true;
            }

            coordinate = default;
            return false;
        }
    }
}
