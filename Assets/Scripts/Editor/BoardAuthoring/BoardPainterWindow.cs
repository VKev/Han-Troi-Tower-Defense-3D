using System.Collections.Generic;
using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    public sealed class BoardPainterWindow : EditorWindow
    {
        private enum OverlayPaintMode
        {
            Prefab,
            CameraFocus,
            Road,
            RoadSpawn,
            RoadEnd,
            RoadDirection,
            Route
        }

        private const float HeaderSize = 28f;
        private const float MinimumCellSize = 5f;
        private const float MaximumCellSize = 56f;
        // Floor for auto-fit sizing only (below the manual slider's MinimumCellSize):
        // large boards should shrink cells to stay scroll-free rather than fall back to a scrollbar.
        private const float MinimumFitCellSize = 3f;
        private const float SidePanelWidth = 300f;
        // GUILayout's automatic width calculation for content inside a ScrollView does not
        // account for the vertical scrollbar's own width, so child controls must be laid out
        // against a narrower explicit width or their right edge gets clipped by the scrollbar.
        private const float ScrollbarAllowance = 18f;
        // Minimum pixel gap between axis label centers; smaller cell sizes skip labels
        // (drawing only every Nth one) instead of rendering illegibly overlapping text.
        private const float MinimumLabelSpacing = 24f;
        private const float ZoomScrollSpeed = 2f;
        private static readonly string[] BrushSizeLabels =
            { "1 x 1", "3 x 3", "5 x 5" };
        private static readonly int[] BrushSizes = { 1, 3, 5 };
        internal static readonly string[] BrushCategoryOptions =
            { "Basic Cell", "Overlay Cell" };
        private static readonly Color CameraFocusAccentColor = new Color(0.15f, 0.85f, 0.95f, 1f);
        private static readonly Color GridPlaceableAccentColor =
            new Color(1f, 0.55f, 0.12f, 1f);
        internal static readonly BoardPaintPreset[] BasicCellPresetOptions =
            { BoardPaintPreset.Empty, BoardPaintPreset.Buildable, BoardPaintPreset.NoBuild };
        private static readonly string[] BasicCellPresetLabels =
            System.Array.ConvertAll(BasicCellPresetOptions, BoardPaintPresetUtility.GetLabel);
        internal static readonly string[] OverlayCellOptions =
            {
                "Prefab", "Camera Focus", "Road", "Road Spawn", "Road End", "Route Arrow", "Route"
            };

        private BoardDefinition boardAsset;
        private BoardAuthoringDocument document;
        private BoardPaintPreset selectedPreset = BoardPaintPreset.Buildable;
        private int brushSize = 1;
        private Vector2 sidePanelScroll;
        private Vector2 scrollPosition;
        private int selectedLevel;
        private bool boardSettingsExpanded;
        private bool fitToView = true;
        private int pendingWidth = 1;
        private int pendingDepth = 1;
        private int pendingHeight = 1;
        private float pendingCellSize = 1f;
        private float pendingHeightUnit = 1f;
        private int pendingMaxCameraGridXSpan;
        private int pendingMaxCameraGridYSpan;
        private Vector3 pendingCameraPositionOffset;
        private Vector3 pendingCameraRotationOffsetEuler;
        private float visualCellSize = 32f;
        private bool strokeActive;
        private bool strokeChanged;
        private bool strokeIsCameraFocus;
        private bool strokeIsRoadBrush;
        private bool strokeIsRoadDirectionBrush;
        private bool strokeIsGridPlaceableBrush;
        private bool overlayCellBrushActive;
        private bool cameraFocusAllowed;
        private OverlayPaintMode selectedOverlayMode = OverlayPaintMode.Prefab;
        private RoadExitDirection selectedRoadExitDirection = RoadExitDirection.East;
        private int selectedRouteIndex;
        private bool strokeIsRouteBrush;
        private readonly Dictionary<GridCell, string> routeOrderLabels =
            new Dictionary<GridCell, string>();
        private GridPlaceableAuthoring selectedGridPlaceable;
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
            selectedGridPlaceable ??= FindFirstGridPlaceable();
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

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSidePanel();
                DrawGridPanel();
            }

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

            EditorGUILayout.Space(2f);
        }

        private void DrawSidePanel()
        {
            sidePanelScroll = EditorGUILayout.BeginScrollView(
                sidePanelScroll,
                GUILayout.Width(SidePanelWidth),
                GUILayout.ExpandHeight(true));

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(SidePanelWidth - ScrollbarAllowance)))
            {
                DrawBoardSettings();
                EditorGUILayout.Space(6f);
                DrawBrushSection();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Box(
                GUIContent.none,
                GUILayout.Width(1f),
                GUILayout.ExpandHeight(true));
        }

        private void DrawBoardSettings()
        {
            boardSettingsExpanded = EditorGUILayout.Foldout(
                boardSettingsExpanded,
                "Board Settings",
                true);
            if (!boardSettingsExpanded)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);
                pendingWidth = Mathf.Max(1, EditorGUILayout.IntField("Width (X)", pendingWidth));
                pendingDepth = Mathf.Max(1, EditorGUILayout.IntField("Depth (Z)", pendingDepth));
                pendingHeight = Mathf.Max(1, EditorGUILayout.IntField("Levels (Y)", pendingHeight));
                if (GUILayout.Button("Apply Resize"))
                {
                    ApplyResize();
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Metrics", EditorStyles.boldLabel);
                pendingCellSize = Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField("Cell Size", pendingCellSize));
                pendingHeightUnit = Mathf.Max(
                    0.01f,
                    EditorGUILayout.FloatField("Height Unit", pendingHeightUnit));
                if (GUILayout.Button("Apply Metrics"))
                {
                    document.SetMetrics(pendingCellSize, pendingHeightUnit);
                    document.Commit("Change Board Metrics");
                }

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Camera Framing Limits", EditorStyles.boldLabel);
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
                if (GUILayout.Button("Apply Camera Limits"))
                {
                    document.SetCameraGridSpans(
                        pendingMaxCameraGridXSpan,
                        pendingMaxCameraGridYSpan);
                    document.Commit("Change Camera Limits");
                    SyncPendingValues();
                }

                EditorGUILayout.LabelField(
                    "0 = Unlimited. Grid Y maps to world Z.",
                    EditorStyles.wordWrappedMiniLabel);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Camera Offsets", EditorStyles.boldLabel);
                pendingCameraPositionOffset = EditorGUILayout.Vector3Field(
                    "Position Offset",
                    pendingCameraPositionOffset);
                pendingCameraRotationOffsetEuler = EditorGUILayout.Vector3Field(
                    "Rotation Offset",
                    pendingCameraRotationOffsetEuler);
                if (GUILayout.Button("Apply Camera Offsets"))
                {
                    document.SetCameraOffsets(
                        pendingCameraPositionOffset,
                        pendingCameraRotationOffsetEuler);
                    document.Commit("Change Camera Offsets");
                    SyncPendingValues();
                }
            }
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

            bool lowestLevelKnown = document.TryGetLowestPlayableLevel(out int lowestLevel);
            cameraFocusAllowed = lowestLevelKnown && selectedLevel == lowestLevel;

            int categoryIndex = overlayCellBrushActive ? 1 : 0;
            categoryIndex = EditorGUILayout.Popup(
                "Category",
                categoryIndex,
                BrushCategoryOptions);
            overlayCellBrushActive = categoryIndex == 1;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (overlayCellBrushActive)
                {
                    DrawOverlayCellPanel();
                }
                else
                {
                    DrawBasicCellPanel();
                }
            }
        }

        private void DrawBasicCellPanel()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
                EditorGUI.DrawRect(swatch, BoardPaintPresetUtility.GetColor(selectedPreset));

                int currentIndex = System.Array.IndexOf(BasicCellPresetOptions, selectedPreset);
                int selectedIndex = EditorGUILayout.Popup(
                    "Preset",
                    Mathf.Max(0, currentIndex),
                    BasicCellPresetLabels);
                selectedPreset = BasicCellPresetOptions[selectedIndex];
            }

            brushSize = EditorGUILayout.IntPopup(
                "Brush Size",
                brushSize,
                BrushSizeLabels,
                BrushSizes);
            EditorGUILayout.LabelField(
                "Left-click/drag paints the selected preset. Right-click/drag erases. "
                + "Overlay Cell data on the same coordinates is preserved. "
                + "The brush is centered and clipped at Board edges. Z=0 is the bottom row.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawOverlayCellPanel()
        {
            selectedOverlayMode = (OverlayPaintMode)EditorGUILayout.Popup(
                "Overlay",
                (int)selectedOverlayMode,
                OverlayCellOptions);

            switch (selectedOverlayMode)
            {
                case OverlayPaintMode.CameraFocus:
                    DrawCameraFocusPanel();
                    break;
                case OverlayPaintMode.Road:
                case OverlayPaintMode.RoadSpawn:
                case OverlayPaintMode.RoadEnd:
                    DrawRoadPanel(GetRoadPaintMode(selectedOverlayMode));
                    break;
                case OverlayPaintMode.RoadDirection:
                    DrawRoadDirectionPanel();
                    break;
                case OverlayPaintMode.Route:
                    DrawRoutePanel();
                    break;
                default:
                    DrawGridPlaceablePanel();
                    break;
            }

            brushSize = EditorGUILayout.IntPopup(
                "Brush Size",
                brushSize,
                BrushSizeLabels,
                BrushSizes);
        }

        private void DrawCameraFocusPanel()
        {
            if (!cameraFocusAllowed)
            {
                EditorGUILayout.HelpBox(
                    "Camera Focus can only be painted on the lowest playable level "
                    + "(the lowest Y level containing a Supports Placement cell).",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                "Left-click/drag marks focus cells. Right-click/drag clears them. "
                + "Basic Cell and other overlay data on the same cell are preserved.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRoadPanel(RoadPaintMode roadMode)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect swatch = GUILayoutUtility.GetRect(16f, 16f, GUILayout.Width(16f));
                EditorGUI.DrawRect(swatch, RoadPaintModeUtility.GetColor(roadMode));
                EditorGUILayout.LabelField("Road Role", RoadPaintModeUtility.GetLabel(roadMode));
            }

            EditorGUILayout.LabelField(
                "Left-click/drag paints the selected road role. Right-click/drag erases. "
                + "Basic Cell and other overlay data are preserved. No level restriction.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawRoadDirectionPanel()
        {
            selectedRoadExitDirection = (RoadExitDirection)EditorGUILayout.EnumPopup(
                "Exit Direction",
                selectedRoadExitDirection);
            if (selectedRoadExitDirection == RoadExitDirection.None)
            {
                selectedRoadExitDirection = RoadExitDirection.East;
            }

            EditorGUILayout.LabelField(
                "Left-click a road cell to choose its next cell. Right-click clears its arrow. "
                + "The last road cell must point to Road End; Road End needs no arrow.",
                EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>
        /// A route is the ordered walk itself, so it can step on one cell twice to close a lap or
        /// leave a junction differently from another route. Exit arrows express neither.
        /// </summary>
        private void DrawRoutePanel()
        {
            int routeCount = document.RouteCount;
            if (routeCount == 0)
            {
                EditorGUILayout.LabelField(
                    "No route recorded yet. Drawing on the grid starts one.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                selectedRouteIndex = Mathf.Clamp(selectedRouteIndex, 0, routeCount - 1);
                var labels = new string[routeCount];
                for (int index = 0; index < routeCount; index++)
                {
                    labels[index] = $"Route {index + 1}  (Spawn {document.GetRouteSpawnPointIndex(index)}, "
                        + $"{document.GetRoute(index).Count} cells, Weight {document.GetRouteWeight(index)})";
                }

                selectedRouteIndex = EditorGUILayout.Popup("Route", selectedRouteIndex, labels);
                int weight = EditorGUILayout.IntField(
                    "Weight",
                    document.GetRouteWeight(selectedRouteIndex));
                if (weight != document.GetRouteWeight(selectedRouteIndex))
                {
                    document.SetRouteWeight(selectedRouteIndex, weight);
                    document.Commit("Set Route Weight");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add"))
                {
                    selectedRouteIndex = document.AddRoute();
                    document.Commit("Add Route");
                }

                using (new EditorGUI.DisabledScope(routeCount == 0))
                {
                    if (GUILayout.Button("Undo Cell")
                        && document.RemoveLastRouteCell(selectedRouteIndex))
                    {
                        document.Commit("Remove Route Cell");
                    }

                    if (GUILayout.Button("Clear"))
                    {
                        document.ClearRoute(selectedRouteIndex);
                        document.Commit("Clear Route");
                    }

                    if (GUILayout.Button("Delete"))
                    {
                        document.RemoveRoute(selectedRouteIndex);
                        selectedRouteIndex = Mathf.Max(0, selectedRouteIndex - 1);
                        document.Commit("Delete Route");
                    }
                }
            }

            EditorGUILayout.LabelField(
                "Left-click or drag along the road to record numbered steps. Routes may share "
                + "their early cells, then diverge at a junction; their weights decide which "
                + "branch an enemy must take. Once a board has routes they replace its exit "
                + "arrows at runtime.",
                EditorStyles.wordWrappedMiniLabel);
            DrawRouteIssues();
        }

        private void DrawRouteIssues()
        {
            IReadOnlyList<GridCell> route = document.GetRoute(selectedRouteIndex);
            if (route.Count == 0)
            {
                return;
            }

            if (route.Count < 2)
            {
                EditorGUILayout.HelpBox("A route needs at least two cells.", MessageType.Warning);
                return;
            }

            for (int index = 1; index < route.Count; index++)
            {
                GridCell previous = route[index - 1];
                GridCell current = route[index];
                if (previous.Y != current.Y
                    || Mathf.Abs(previous.X - current.X) + Mathf.Abs(previous.Z - current.Z) != 1)
                {
                    EditorGUILayout.HelpBox(
                        $"Step {index} jumps from {previous} to {current}. Route cells must "
                        + "share an edge.",
                        MessageType.Error);
                    return;
                }
            }

            if ((document.GetFlags(route[0]) & BoardCellFlags.RoadSpawn) == 0)
            {
                EditorGUILayout.HelpBox(
                    "The first cell of the route is not a Road Spawn.",
                    MessageType.Warning);
            }

            if ((document.GetFlags(route[route.Count - 1]) & BoardCellFlags.RoadEnd) == 0)
            {
                EditorGUILayout.HelpBox(
                    "The last cell of the route is not a Road End.",
                    MessageType.Warning);
            }
        }

        private void DrawGridPlaceablePanel()
        {
            EditorGUI.BeginChangeCheck();
            GridPlaceableAuthoring selected = (GridPlaceableAuthoring)EditorGUILayout.ObjectField(
                "Prefab",
                selectedGridPlaceable,
                typeof(GridPlaceableAuthoring),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedGridPlaceable = IsValidGridPlaceableSelection(selected)
                    ? selected
                    : null;
            }

            if (selectedGridPlaceable == null)
            {
                EditorGUILayout.HelpBox(
                    "Choose a prefab asset whose root has a GridPlaceableAuthoring component.",
                    MessageType.Info);
            }

            EditorGUILayout.LabelField(
                "Left-click/drag paints the selected prefab. Right-click/drag erases. "
                + "Prefab cells are visual data and never change Basic Cell or other overlay flags.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void DrawGridPanel()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                DrawGridToolbar();
                DrawGridArea();
            }
        }

        private void DrawGridToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUI.enabled = selectedLevel > 0;
                if (GUILayout.Button("<", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    selectedLevel--;
                }

                GUI.enabled = true;
                int maximumLevel = Mathf.Max(0, document.Dimensions.Height - 1);
                selectedLevel = EditorGUILayout.IntSlider(
                    $"Level Y = {selectedLevel} / {maximumLevel}",
                    selectedLevel,
                    0,
                    maximumLevel,
                    GUILayout.MinWidth(220f));

                GUI.enabled = selectedLevel < maximumLevel;
                if (GUILayout.Button(">", EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    selectedLevel++;
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();

                fitToView = GUILayout.Toggle(
                    fitToView,
                    new GUIContent(
                        "Fit to View",
                        "Automatically size cells so the whole board fits without scrolling."),
                    EditorStyles.toolbarButton,
                    GUILayout.Width(80f));

                using (new EditorGUI.DisabledScope(fitToView))
                {
                    visualCellSize = EditorGUILayout.Slider(
                        visualCellSize,
                        MinimumCellSize,
                        MaximumCellSize,
                        GUILayout.Width(120f));
                }
            }
        }

        private void DrawGridArea()
        {
            GridDimensions dimensions = document.Dimensions;
            Rect viewport = GUILayoutUtility.GetRect(
                10f,
                10f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));

            HandleZoomScroll(viewport, Event.current);

            float cellSize = ResolveCellSize(viewport, dimensions);
            float contentWidth = HeaderSize + dimensions.Width * cellSize;
            float contentHeight = HeaderSize + dimensions.Depth * cellSize;

            scrollPosition = GUI.BeginScrollView(
                viewport,
                scrollPosition,
                new Rect(0f, 0f, contentWidth, contentHeight));

            var gridRect = new Rect(0f, 0f, contentWidth, contentHeight);
            gridControlId = GUIUtility.GetControlID(
                "BoardPainterGrid".GetHashCode(),
                FocusType.Passive,
                gridRect);

            DrawAxisLabels(gridRect, dimensions, cellSize);
            DrawCells(gridRect, dimensions, cellSize);
            HandleGridInput(gridRect, dimensions, cellSize, Event.current);

            GUI.EndScrollView();
        }

        private void HandleZoomScroll(Rect viewport, Event current)
        {
            if (current.type != EventType.ScrollWheel || !viewport.Contains(current.mousePosition))
            {
                return;
            }

            fitToView = false;
            visualCellSize = Mathf.Clamp(
                visualCellSize - current.delta.y * ZoomScrollSpeed,
                MinimumCellSize,
                MaximumCellSize);
            current.Use();
            Repaint();
        }

        private float ResolveCellSize(Rect viewport, GridDimensions dimensions)
        {
            if (!fitToView)
            {
                return visualCellSize;
            }

            float usableWidth = Mathf.Max(1f, viewport.width - HeaderSize);
            float usableHeight = Mathf.Max(1f, viewport.height - HeaderSize);
            float fitWidth = usableWidth / Mathf.Max(1, dimensions.Width);
            float fitHeight = usableHeight / Mathf.Max(1, dimensions.Depth);
            return Mathf.Clamp(Mathf.Min(fitWidth, fitHeight), MinimumFitCellSize, MaximumCellSize);
        }

        private void DrawAxisLabels(Rect gridRect, GridDimensions dimensions, float cellSize)
        {
            GUI.Label(
                new Rect(gridRect.x, gridRect.y, HeaderSize, HeaderSize),
                "Z\\X",
                EditorStyles.centeredGreyMiniLabel);

            int step = Mathf.Max(1, Mathf.CeilToInt(MinimumLabelSpacing / cellSize));

            for (int x = 0; x < dimensions.Width; x += step)
            {
                float labelWidth = Mathf.Min(step, dimensions.Width - x) * cellSize;
                GUI.Label(
                    new Rect(
                        gridRect.x + HeaderSize + x * cellSize,
                        gridRect.y,
                        labelWidth,
                        HeaderSize),
                    x.ToString(),
                    EditorStyles.centeredGreyMiniLabel);
            }

            for (int z = 0; z < dimensions.Depth; z += step)
            {
                int row = dimensions.Depth - 1 - z;
                int rowsInGroup = Mathf.Min(step, dimensions.Depth - z);
                GUI.Label(
                    new Rect(
                        gridRect.x,
                        gridRect.y + HeaderSize + (row - (rowsInGroup - 1)) * cellSize,
                        HeaderSize,
                        rowsInGroup * cellSize),
                    z.ToString(),
                    EditorStyles.centeredGreyMiniLabel);
            }
        }

        private void DrawCells(Rect gridRect, GridDimensions dimensions, float cellSize)
        {
            bool showRouteOrder = overlayCellBrushActive
                && selectedOverlayMode == OverlayPaintMode.Route
                && cellSize >= 14f;
            if (showRouteOrder)
            {
                BuildRouteOrderLabels();
            }

            for (int z = 0; z < dimensions.Depth; z++)
            {
                for (int x = 0; x < dimensions.Width; x++)
                {
                    GridCell coordinate = new GridCell(x, z, selectedLevel);
                    BoardCellFlags flags = document.GetFlags(coordinate);
                    BoardPaintPreset preset = BoardPaintPresetUtility.GetClosestPreset(flags);
                    RoadPaintMode roadRole = RoadPaintModeUtility.GetRoadRole(flags);
                    RoadExitDirection roadExitDirection =
                        document.GetRoadExitDirection(coordinate);
                    GameObject gridPlaceable = document.GetGridPlaceable(coordinate);
                    Rect cellRect = GetCellRect(gridRect, dimensions, x, z, cellSize);
                    Color fillColor = roadRole != RoadPaintMode.None
                        ? RoadPaintModeUtility.GetColor(roadRole)
                        : BoardPaintPresetUtility.GetColor(preset);
                    EditorGUI.DrawRect(cellRect, fillColor);
                    GUI.Box(cellRect, GUIContent.none);

                    const BoardCellFlags mismatchIgnoreMask =
                        BoardCellFlags.StaticBlocker
                        | BoardCellFlags.CameraFocus
                        | RoadPaintModeUtility.RoadRoleMask;
                    if ((flags & ~mismatchIgnoreMask) != BoardPaintPresetUtility.GetFlags(preset))
                    {
                        GUI.Label(cellRect, "?", EditorStyles.centeredGreyMiniLabel);
                    }
                    else if ((flags & BoardCellFlags.StaticBlocker) != 0)
                    {
                        GUI.Label(cellRect, "X", EditorStyles.whiteBoldLabel);
                    }

                    if ((flags & BoardCellFlags.CameraFocus) != 0)
                    {
                        float accentSize = Mathf.Min(6f, cellSize * 0.4f);
                        var accentRect = new Rect(
                            cellRect.xMax - accentSize - 1f,
                            cellRect.y + 1f,
                            accentSize,
                            accentSize);
                        EditorGUI.DrawRect(accentRect, CameraFocusAccentColor);
                    }

                    if (gridPlaceable != null)
                    {
                        float accentSize = Mathf.Min(6f, cellSize * 0.4f);
                        var accentRect = new Rect(
                            cellRect.x + 1f,
                            cellRect.yMax - accentSize - 1f,
                            accentSize,
                            accentSize);
                        EditorGUI.DrawRect(accentRect, GridPlaceableAccentColor);
                    }

                    if (showRouteOrder
                        && routeOrderLabels.TryGetValue(coordinate, out string orderLabel))
                    {
                        GUI.Label(cellRect, orderLabel, EditorStyles.whiteBoldLabel);
                    }
                    else if (roadExitDirection != RoadExitDirection.None)
                    {
                        GUI.Label(
                            cellRect,
                            GetRoadExitArrow(roadExitDirection),
                            EditorStyles.whiteBoldLabel);
                    }

                    if (cellRect.Contains(Event.current.mousePosition))
                    {
                        string tooltip = gridPlaceable != null
                            ? $"{coordinate}: {flags}\nPrefab: {gridPlaceable.name}"
                            : $"{coordinate}: {flags}";
                        GUI.Label(
                            cellRect,
                            new GUIContent(string.Empty, tooltip));
                    }
                }
            }
        }

        private void HandleGridInput(
            Rect gridRect,
            GridDimensions dimensions,
            float cellSize,
            Event current)
        {
            bool useCameraFocusBrush = overlayCellBrushActive
                && selectedOverlayMode == OverlayPaintMode.CameraFocus
                && cameraFocusAllowed;
            bool useRoadBrush = overlayCellBrushActive
                && IsRoadOverlay(selectedOverlayMode);
            bool useRoadDirectionBrush = overlayCellBrushActive
                && selectedOverlayMode == OverlayPaintMode.RoadDirection;
            bool useRouteBrush = overlayCellBrushActive
                && selectedOverlayMode == OverlayPaintMode.Route;
            bool useGridPlaceableBrush = overlayCellBrushActive
                && selectedOverlayMode == OverlayPaintMode.Prefab;
            RoadPaintMode selectedRoadMode = GetRoadPaintMode(selectedOverlayMode);
            bool brushAvailable = !overlayCellBrushActive
                || useCameraFocusBrush
                || useRoadBrush
                || useRoadDirectionBrush
                || useRouteBrush
                || useGridPlaceableBrush;

            if (current.type == EventType.MouseDown
                && (current.button == 0 || current.button == 1)
                && brushAvailable
                && TryGetCell(gridRect, dimensions, current.mousePosition, cellSize, out GridCell coordinate))
            {
                strokeActive = true;
                strokeChanged = false;
                strokeIsCameraFocus = useCameraFocusBrush;
                strokeIsRoadBrush = useRoadBrush;
                strokeIsRoadDirectionBrush = useRoadDirectionBrush;
                strokeIsRouteBrush = useRouteBrush;
                strokeIsGridPlaceableBrush = useGridPlaceableBrush;
                GUIUtility.hotControl = gridControlId;
                if (useCameraFocusBrush)
                {
                    PaintCameraFocusCell(coordinate, current.button != 1);
                }
                else if (useRoadBrush)
                {
                    PaintRoadCell(coordinate, current.button == 1 ? RoadPaintMode.None : selectedRoadMode);
                }
                else if (useRoadDirectionBrush)
                {
                    PaintRoadDirectionCell(
                        coordinate,
                        current.button == 1
                            ? RoadExitDirection.None
                            : selectedRoadExitDirection);
                }
                else if (useRouteBrush)
                {
                    PaintRouteCell(coordinate, current.button == 1);
                }
                else if (useGridPlaceableBrush)
                {
                    if (current.button == 1
                        || selectedGridPlaceable != null)
                    {
                        PaintGridPlaceableCell(
                            coordinate,
                            current.button == 1
                                ? null
                                : selectedGridPlaceable.gameObject);
                    }
                }
                else
                {
                    PaintCell(coordinate, current.button == 1 ? BoardPaintPreset.Empty : selectedPreset);
                }

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
                        cellSize,
                        out GridCell dragCoordinate))
                {
                    if (strokeIsCameraFocus)
                    {
                        PaintCameraFocusCell(dragCoordinate, current.button != 1);
                    }
                    else if (strokeIsRoadBrush)
                    {
                        PaintRoadCell(
                            dragCoordinate,
                            current.button == 1 ? RoadPaintMode.None : selectedRoadMode);
                    }
                    else if (strokeIsRoadDirectionBrush)
                    {
                        PaintRoadDirectionCell(
                            dragCoordinate,
                            current.button == 1
                                ? RoadExitDirection.None
                                : selectedRoadExitDirection);
                    }
                    else if (strokeIsRouteBrush)
                    {
                        if (current.button != 1)
                        {
                            PaintRouteCell(dragCoordinate, false);
                        }
                    }
                    else if (strokeIsGridPlaceableBrush)
                    {
                        if (current.button == 1
                            || selectedGridPlaceable != null)
                        {
                            PaintGridPlaceableCell(
                                dragCoordinate,
                                current.button == 1
                                    ? null
                                    : selectedGridPlaceable.gameObject);
                        }
                    }
                    else
                    {
                        PaintCell(
                            dragCoordinate,
                            current.button == 1 ? BoardPaintPreset.Empty : selectedPreset);
                    }
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

        private void PaintCameraFocusCell(GridCell coordinate, bool enabled)
        {
            if (strokeChanged && coordinate == lastPaintedCell)
            {
                return;
            }

            lastPaintedCell = coordinate;
            strokeChanged |= PaintCameraFocusBrush(document, coordinate, brushSize, enabled);
            Repaint();
        }

        internal static bool PaintCameraFocusBrush(
            BoardAuthoringDocument targetDocument,
            GridCell center,
            int size,
            bool enabled)
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
                    targetDocument.SetCameraFocus(coordinate, enabled);
                    changed |= before != targetDocument.GetFlags(coordinate);
                }
            }

            return changed;
        }

        private void PaintRoadCell(GridCell coordinate, RoadPaintMode mode)
        {
            if (strokeChanged && coordinate == lastPaintedCell)
            {
                return;
            }

            lastPaintedCell = coordinate;
            strokeChanged |= PaintRoadBrush(document, coordinate, brushSize, mode);
            Repaint();
        }

        private void PaintRoadDirectionCell(
            GridCell coordinate,
            RoadExitDirection direction)
        {
            if (strokeChanged && coordinate == lastPaintedCell)
            {
                return;
            }

            lastPaintedCell = coordinate;
            RoadExitDirection before = document.GetRoadExitDirection(coordinate);
            document.SetRoadExitDirection(coordinate, direction);
            strokeChanged |= before != document.GetRoadExitDirection(coordinate);
            Repaint();
        }

        private void PaintRouteCell(GridCell coordinate, bool removeLast)
        {
            if (document.RouteCount == 0)
            {
                selectedRouteIndex = document.AddRoute();
            }

            strokeChanged |= removeLast
                ? document.RemoveLastRouteCell(selectedRouteIndex)
                : document.AppendRouteCell(selectedRouteIndex, coordinate);
            Repaint();
        }

        internal static bool PaintRoadBrush(
            BoardAuthoringDocument targetDocument,
            GridCell center,
            int size,
            RoadPaintMode mode)
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
                    targetDocument.SetRoadRole(coordinate, mode);
                    changed |= before != targetDocument.GetFlags(coordinate);
                }
            }

            return changed;
        }

        private void PaintGridPlaceableCell(
            GridCell coordinate,
            GameObject prefab)
        {
            if (strokeChanged && coordinate == lastPaintedCell)
            {
                return;
            }

            lastPaintedCell = coordinate;
            strokeChanged |= PaintGridPlaceableBrush(
                document,
                coordinate,
                brushSize,
                prefab);
            Repaint();
        }

        internal static bool PaintGridPlaceableBrush(
            BoardAuthoringDocument targetDocument,
            GridCell center,
            int size,
            GameObject prefab)
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
                    GameObject before =
                        targetDocument.GetGridPlaceable(coordinate);
                    targetDocument.SetGridPlaceable(coordinate, prefab);
                    changed |= before !=
                        targetDocument.GetGridPlaceable(coordinate);
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
                string undoName = strokeIsCameraFocus
                    ? "Toggle Camera Focus"
                    : strokeIsRoadBrush
                        ? "Paint Road Cells"
                        : strokeIsRouteBrush
                            ? "Edit Route"
                        : strokeIsRoadDirectionBrush
                            ? "Paint Road Directions"
                        : strokeIsGridPlaceableBrush
                            ? "Paint Grid Prefabs"
                            : "Paint Board Cells";
                document.Commit(undoName);
            }

            strokeActive = false;
            strokeChanged = false;
            strokeIsRoadDirectionBrush = false;
            strokeIsRouteBrush = false;
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
                + $"Prefab cells: {document.ActiveGridPlaceableCount}    "
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
            overlayCellBrushActive = false;
            scrollPosition = Vector2.zero;
            SyncPendingValues();
            Repaint();
        }

        internal static bool IsValidGridPlaceableSelection(
            GridPlaceableAuthoring candidate) =>
            candidate != null
            && candidate.transform.parent == null
            && PrefabUtility.IsPartOfPrefabAsset(candidate.gameObject);

        private static GridPlaceableAuthoring FindFirstGridPlaceable()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int index = 0; index < prefabGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(
                    prefabGuids[index]);
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(path);
                GridPlaceableAuthoring candidate =
                    prefab != null ? prefab.GetComponent<GridPlaceableAuthoring>() : null;
                if (IsValidGridPlaceableSelection(candidate))
                {
                    return candidate;
                }
            }

            return null;
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
            pendingCameraPositionOffset = document.CameraPositionOffset;
            pendingCameraRotationOffsetEuler = document.CameraRotationOffsetEuler;
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

        private static bool IsRoadOverlay(OverlayPaintMode mode) =>
            mode == OverlayPaintMode.Road
            || mode == OverlayPaintMode.RoadSpawn
            || mode == OverlayPaintMode.RoadEnd;

        private static RoadPaintMode GetRoadPaintMode(OverlayPaintMode mode) =>
            mode switch
            {
                OverlayPaintMode.Road => RoadPaintMode.Road,
                OverlayPaintMode.RoadSpawn => RoadPaintMode.Spawn,
                OverlayPaintMode.RoadEnd => RoadPaintMode.End,
                _ => RoadPaintMode.None
            };

        /// <summary>
        /// A cell walked twice shows both of its step numbers, which is how a lap reads back.
        /// </summary>
        private void BuildRouteOrderLabels()
        {
            routeOrderLabels.Clear();
            IReadOnlyList<GridCell> route = document.GetRoute(selectedRouteIndex);
            for (int index = 0; index < route.Count; index++)
            {
                routeOrderLabels[route[index]] =
                    routeOrderLabels.TryGetValue(route[index], out string existing)
                        ? existing + "/" + (index + 1)
                        : (index + 1).ToString();
            }
        }

        private static string GetRoadExitArrow(RoadExitDirection direction) =>
            direction switch
            {
                RoadExitDirection.East => ">",
                RoadExitDirection.South => "v",
                RoadExitDirection.West => "<",
                RoadExitDirection.North => "^",
                _ => string.Empty
            };

        private Rect GetCellRect(
            Rect gridRect,
            GridDimensions dimensions,
            int x,
            int z,
            float cellSize)
        {
            int row = dimensions.Depth - 1 - z;
            const float gap = 1f;
            return new Rect(
                gridRect.x + HeaderSize + x * cellSize + gap,
                gridRect.y + HeaderSize + row * cellSize + gap,
                cellSize - gap * 2f,
                cellSize - gap * 2f);
        }

        private bool TryGetCell(
            Rect gridRect,
            GridDimensions dimensions,
            Vector2 mousePosition,
            float cellSize,
            out GridCell coordinate)
        {
            float localX = mousePosition.x - gridRect.x - HeaderSize;
            float localY = mousePosition.y - gridRect.y - HeaderSize;
            int x = Mathf.FloorToInt(localX / cellSize);
            int row = Mathf.FloorToInt(localY / cellSize);
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
