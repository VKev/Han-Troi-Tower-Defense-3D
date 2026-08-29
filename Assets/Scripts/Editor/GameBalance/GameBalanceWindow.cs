using System;
using System.Collections.Generic;
using TowerDefense3D.Enemies;
using TowerDefense3D.GameFlow;
using TowerDefense3D.GameFlow.Editor;
using TowerDefense3D.GridPlacement;
using TowerDefense3D.GridPlacement.Editor;
using TowerDefense3D.Towers;
using TowerDefense3D.Waves;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameBalance.Editor
{
    /// <summary>
    /// Shows a whole balance section as one spreadsheet. Rows are the smallest repeated thing a
    /// designer tunes rather than the asset file: a catalog of eight levels reads as eight rows,
    /// and a wave schedule reads as one row per spawn batch. Nested profile structs are flattened
    /// into their own columns so every authored number is visible and editable in place.
    /// </summary>
    public sealed class GameBalanceWindow : EditorWindow
    {
        private enum BalanceTab
        {
            Levels,
            Waves,
            Enemies,
            Towers,
            ElementalReactions
        }

        private static readonly string[] TabLabels =
        {
            "Levels",
            "Waves",
            "Enemies",
            "Towers",
            "Reactions"
        };

        private const float NameColumnWidth = 150f;
        private const float IndexColumnWidth = 52f;
        private const float ActionColumnWidth = 94f;
        private const float MinimumColumnWidth = 58f;
        private const float MaximumColumnWidth = 120f;
        private const float WideValueColumnWidth = 150f;
        private const float HeaderRowHeight = 30f;
        private const float DetailPanelHeight = 260f;
        private const float DetailPanelWidth = 460f;
        private const string MissingValueLabel = "–";

        private readonly List<SerializedObject> tabSerializedAssets = new List<SerializedObject>();
        private readonly List<BalanceRow> rows = new List<BalanceRow>();
        private readonly List<BalanceColumn> columns = new List<BalanceColumn>();
        private readonly Dictionary<string, int> columnIndexByKey = new Dictionary<string, int>();
        private readonly HashSet<string> hiddenColumnKeys = new HashSet<string>();
        private readonly Dictionary<ScriptableObject, int> rowCountByAsset =
            new Dictionary<ScriptableObject, int>();

        private BalanceTab selectedTab;
        private ScriptableObject detailAsset;
        private ScriptableObject pendingDetailAsset;
        private bool hasPendingDetailChange;
        private UnityEditor.Editor detailAssetEditor;
        private Vector2 tableScroll;
        private Vector2 detailScroll;
        private List<string> validationMessages = new List<string>();
        private bool isTableStale = true;
        private bool isDetailOnRight;
        private bool hasIndexColumn;
        private bool useSections;
        private bool showAssetColumn;
        private GUIStyle sectionHeaderStyle;
        private GUIStyle headerCellStyle;
        private GUIStyle missingValueStyle;

        [MenuItem("Tools/Tower Defense/Game Balance Center")]
        public static void Open()
        {
            GetWindow<GameBalanceWindow>("Game Balance Center");
        }

        private GUIStyle HeaderCellStyle
        {
            get
            {
                if (headerCellStyle == null)
                {
                    headerCellStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                    {
                        wordWrap = true,
                        alignment = TextAnchor.LowerLeft
                    };
                }

                return headerCellStyle;
            }
        }

        private GUIStyle SectionHeaderStyle
        {
            get
            {
                if (sectionHeaderStyle == null)
                {
                    sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(6, 6, 0, 0)
                    };
                }

                return sectionHeaderStyle;
            }
        }

        private GUIStyle MissingValueStyle
        {
            get
            {
                if (missingValueStyle == null)
                {
                    missingValueStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
                }

                return missingValueStyle;
            }
        }

        private void OnDisable()
        {
            DestroyDetailAssetEditor();
        }

        private void OnProjectChange()
        {
            isTableStale = true;
            Repaint();
        }

        private void OnFocus()
        {
            isTableStale = true;
        }

        private void OnGUI()
        {
            if (isTableStale)
            {
                RebuildTable();
            }

            // Latched before the toolbar so switching tabs cannot change the window's layout
            // groups halfway through an event, which IMGUI reads back as a group mismatch.
            isDetailOnRight = detailAsset != null;
            DrawToolbar();
            DrawWorkspaceHeader();
            if (isDetailOnRight)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUILayout.VerticalScope())
                    {
                        DrawTable();
                    }

                    DrawDetail();
                }
            }
            else
            {
                DrawTable();
                DrawDetail();
            }

            DrawValidationMessages();
            ApplyPendingDetailAsset();
        }

        /// <summary>
        /// Showing or hiding the detail panel changes how many layout groups the window draws, so
        /// the swap waits until every group of the current event has been closed.
        /// </summary>
        private void RequestDetailAsset(ScriptableObject asset)
        {
            pendingDetailAsset = asset;
            hasPendingDetailChange = true;
        }

        private void ApplyPendingDetailAsset()
        {
            if (!hasPendingDetailChange)
            {
                return;
            }

            hasPendingDetailChange = false;
            SelectDetailAsset(pendingDetailAsset);
            pendingDetailAsset = null;
            Repaint();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                int selected = GUILayout.Toolbar(
                    (int)selectedTab,
                    TabLabels,
                    EditorStyles.toolbarButton);
                if (selected != (int)selectedTab)
                {
                    selectedTab = (BalanceTab)selected;
                    RequestDetailAsset(null);
                    isTableStale = true;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(
                    $"{rows.Count} rows · {CountVisibleColumns()}/{columns.Count} fields",
                    EditorStyles.miniLabel,
                    GUILayout.Width(140f));
                if (GUILayout.Button(
                        "Columns",
                        EditorStyles.toolbarDropDown,
                        GUILayout.Width(70f)))
                {
                    ShowColumnMenu();
                }

                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(62f)))
                {
                    validationMessages = CollectValidationMessages();
                }

                if (selectedTab == BalanceTab.Towers
                    && GUILayout.Button("Tower Settings", EditorStyles.toolbarButton, GUILayout.Width(96f)))
                {
                    ShowTowerSettingsMenu();
                }

                using (new EditorGUI.DisabledScope(detailAsset == null))
                {
                    if (GUILayout.Button("Add", EditorStyles.toolbarButton, GUILayout.Width(45f)))
                    {
                        ShowAddMenu();
                    }
                }
            }
        }

        private void DrawWorkspaceHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 40f);
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.17f, 0.25f, 1f));
            EditorGUI.LabelField(
                new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 18f),
                TabLabels[(int)selectedTab] + " Balance",
                EditorStyles.boldLabel);
            EditorGUI.LabelField(
                new Rect(rect.x + 10f, rect.y + 21f, rect.width - 20f, 16f),
                GetTabDescription(selectedTab),
                EditorStyles.miniLabel);
        }

        private static string GetTabDescription(BalanceTab tab) => tab switch
        {
            BalanceTab.Levels => "Economy and scene order. Add a level, then assign its wave schedule.",
            BalanceTab.Waves => "Tune rewards and spawn batches. Add batches directly to a selected wave.",
            BalanceTab.Enemies => "Health, speed, rewards, and special-enemy rules.",
            BalanceTab.Towers => "Tower stats, combat rules, and placement definitions.",
            BalanceTab.ElementalReactions => "Element pairs, damage, burn, lift, and reaction durations.",
            _ => string.Empty
        };

        private void ShowColumnMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Show All"), false, () => hiddenColumnKeys.Clear());
            menu.AddSeparator(string.Empty);
            for (int index = 0; index < columns.Count; index++)
            {
                string key = columns[index].Key;
                menu.AddItem(
                    new GUIContent(columns[index].Tooltip),
                    !hiddenColumnKeys.Contains(key),
                    () => ToggleColumn(key));
            }

            menu.ShowAsContext();
        }

        private void ShowTowerSettingsMenu()
        {
            var menu = new GenericMenu();
            AddDetailMenuItem(
                menu,
                "Global/Combat Rules",
                AssetDatabase.LoadAssetAtPath<TowerCombatRules>(
                    "Assets/Config/Towers/Global/TowerCombatRules.asset"));
            AddDetailMenuItem(
                menu,
                "Catalog/Tower Catalog",
                AssetDatabase.LoadAssetAtPath<TowerCatalog>(
                    "Assets/Config/Towers/Catalogs/TowerCatalog.asset"));
            menu.AddSeparator("Placement Definitions/");

            string[] guids = AssetDatabase.FindAssets(
                "t:TowerDefinition",
                new[] { "Assets/Config/GridPlacement" });
            for (int index = 0; index < guids.Length; index++)
            {
                TowerDefinition definition = AssetDatabase.LoadAssetAtPath<TowerDefinition>(
                    AssetDatabase.GUIDToAssetPath(guids[index]));
                AddDetailMenuItem(menu, "Placement Definitions/" + definition.name, definition);
            }

            menu.ShowAsContext();
        }

        private void AddDetailMenuItem(
            GenericMenu menu,
            string label,
            ScriptableObject asset)
        {
            if (asset == null)
            {
                menu.AddDisabledItem(new GUIContent(label));
                return;
            }

            menu.AddItem(
                new GUIContent(label),
                asset == detailAsset,
                () => RequestDetailAsset(asset));
        }

        private void ToggleColumn(string key)
        {
            if (!hiddenColumnKeys.Remove(key))
            {
                hiddenColumnKeys.Add(key);
            }

            Repaint();
        }

        private int CountVisibleColumns()
        {
            int count = 0;
            for (int index = 0; index < columns.Count; index++)
            {
                if (!hiddenColumnKeys.Contains(columns[index].Key))
                {
                    count++;
                }
            }

            return count;
        }

        private void DrawTable()
        {
            if (rows.Count == 0)
            {
                EditorGUILayout.HelpBox("No matching authoring assets found.", MessageType.Info);
                GUILayout.FlexibleSpace();
                return;
            }

            for (int index = 0; index < tabSerializedAssets.Count; index++)
            {
                tabSerializedAssets[index].UpdateIfRequiredOrScript();
            }

            tableScroll = EditorGUILayout.BeginScrollView(tableScroll);
            DrawHeaderRow();
            ScriptableObject sectionAsset = null;
            for (int index = 0; index < rows.Count; index++)
            {
                if (useSections
                    && UsesSection(rows[index].Asset)
                    && rows[index].Asset != sectionAsset)
                {
                    sectionAsset = rows[index].Asset;
                    DrawSectionHeader(sectionAsset);
                }

                DrawRow(index);
            }

            EditorGUILayout.EndScrollView();
            for (int index = 0; index < tabSerializedAssets.Count; index++)
            {
                tabSerializedAssets[index].ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// Rows that belong to one asset are gathered under its own heading, which reads better
        /// than repeating the file name down a column when every level owns a block of rows.
        /// </summary>
        private void DrawSectionHeader(ScriptableObject asset)
        {
            EditorGUILayout.Space(2f);
            Rect rect = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.07f));
            bool isDetailAsset = asset == detailAsset;
            if (GUI.Button(rect, asset.name, SectionHeaderStyle))
            {
                RequestDetailAsset(isDetailAsset ? null : asset);
                EditorGUIUtility.PingObject(asset);
            }
        }

        private void DrawHeaderRow()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (showAssetColumn)
                {
                    DrawHeaderCell("Asset", null, NameColumnWidth);
                }
                if (hasIndexColumn)
                {
                    DrawHeaderCell("#", null, IndexColumnWidth);
                }

                for (int index = 0; index < columns.Count; index++)
                {
                    BalanceColumn column = columns[index];
                    if (!hiddenColumnKeys.Contains(column.Key))
                    {
                        DrawHeaderCell(column.Label, column.Tooltip, column.Width);
                    }
                }

                DrawHeaderCell("Actions", "Duplicate or delete this authored item.", ActionColumnWidth);
            }

            DrawSeparator();
        }

        private void DrawHeaderCell(string label, string tooltip, float width)
        {
            EditorGUI.LabelField(
                EditorGUILayout.GetControlRect(false, HeaderRowHeight, GUILayout.Width(width)),
                new GUIContent(label, tooltip),
                HeaderCellStyle);
        }

        private void DrawRow(int rowIndex)
        {
            BalanceRow row = rows[rowIndex];
            if (row.Asset == null)
            {
                return;
            }

            Rect rowRect = EditorGUILayout.BeginHorizontal();
            if (Event.current.type == EventType.Repaint && rowIndex % 2 == 1)
            {
                EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.06f));
            }

            if (showAssetColumn)
            {
                if (UsesSection(row.Asset))
                {
                    EditorGUI.LabelField(
                        EditorGUILayout.GetControlRect(
                            false,
                            EditorGUIUtility.singleLineHeight,
                            GUILayout.Width(NameColumnWidth)),
                        GUIContent.none);
                }
                else
                {
                    bool isDetailRow = row.Asset == detailAsset;
                    if (GUILayout.Toggle(
                            isDetailRow,
                            row.AssetName,
                            "Button",
                            GUILayout.Width(NameColumnWidth))
                        != isDetailRow)
                    {
                        RequestDetailAsset(isDetailRow ? null : row.Asset);
                        EditorGUIUtility.PingObject(row.Asset);
                    }
                }
            }

            if (hasIndexColumn)
            {
                EditorGUI.LabelField(
                    EditorGUILayout.GetControlRect(
                        false,
                        EditorGUIUtility.singleLineHeight,
                        GUILayout.Width(IndexColumnWidth)),
                    row.IndexLabel,
                    EditorStyles.miniLabel);
            }

            for (int index = 0; index < columns.Count; index++)
            {
                BalanceColumn column = columns[index];
                if (hiddenColumnKeys.Contains(column.Key))
                {
                    continue;
                }

                Rect cellRect = EditorGUILayout.GetControlRect(
                    false,
                    EditorGUIUtility.singleLineHeight,
                    GUILayout.Width(column.Width));
                DrawCell(cellRect, row, column);
            }

            DrawRowActions(row);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawRowActions(BalanceRow row)
        {
            using (new EditorGUI.DisabledScope(!row.CanModify))
            {
                if (GUILayout.Button("Copy", GUILayout.Width(46f)))
                {
                    DuplicateRow(row);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(48f)))
                {
                    DeleteRow(row);
                }
            }
        }

        /// <summary>
        /// Cells draw their own control rather than going through PropertyField, which would also
        /// draw the field's Header and Space decorators and blow every row up to several lines.
        /// </summary>
        private void DrawCell(Rect rect, BalanceRow row, BalanceColumn column)
        {
            if (!row.PathByColumnKey.TryGetValue(column.Key, out string propertyPath))
            {
                EditorGUI.LabelField(rect, MissingValueLabel, MissingValueStyle);
                return;
            }

            SerializedProperty property = row.Serialized.FindProperty(propertyPath);
            if (property == null)
            {
                EditorGUI.LabelField(rect, MissingValueLabel, MissingValueStyle);
                return;
            }

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(rect, $"{property.arraySize} items", EditorStyles.miniLabel);
                return;
            }

            EditorGUI.BeginProperty(rect, GUIContent.none, property);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    EditorGUI.BeginChangeCheck();
                    float floatValue = EditorGUI.FloatField(rect, property.floatValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.floatValue = floatValue;
                    }

                    break;
                case SerializedPropertyType.Integer:
                    EditorGUI.BeginChangeCheck();
                    int intValue = EditorGUI.IntField(rect, property.intValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.intValue = intValue;
                    }

                    break;
                case SerializedPropertyType.Boolean:
                    EditorGUI.BeginChangeCheck();
                    bool boolValue = EditorGUI.Toggle(rect, property.boolValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.boolValue = boolValue;
                    }

                    break;
                case SerializedPropertyType.String:
                    EditorGUI.BeginChangeCheck();
                    string stringValue = EditorGUI.TextField(rect, property.stringValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.stringValue = stringValue;
                    }

                    break;
                case SerializedPropertyType.Enum:
                    EditorGUI.BeginChangeCheck();
                    int enumIndex = EditorGUI.Popup(
                        rect,
                        property.enumValueIndex,
                        property.enumDisplayNames);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.enumValueIndex = enumIndex;
                    }

                    break;
                case SerializedPropertyType.Color:
                    EditorGUI.BeginChangeCheck();
                    Color colorValue = EditorGUI.ColorField(rect, property.colorValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.colorValue = colorValue;
                    }

                    break;
                case SerializedPropertyType.Vector2:
                    EditorGUI.BeginChangeCheck();
                    Vector2 vector2Value = EditorGUI.Vector2Field(
                        rect,
                        GUIContent.none,
                        property.vector2Value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.vector2Value = vector2Value;
                    }

                    break;
                case SerializedPropertyType.Vector3:
                    EditorGUI.BeginChangeCheck();
                    Vector3 vector3Value = EditorGUI.Vector3Field(
                        rect,
                        GUIContent.none,
                        property.vector3Value);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.vector3Value = vector3Value;
                    }

                    break;
                case SerializedPropertyType.AnimationCurve:
                    EditorGUI.BeginChangeCheck();
                    AnimationCurve curveValue = EditorGUI.CurveField(
                        rect,
                        property.animationCurveValue);
                    if (EditorGUI.EndChangeCheck())
                    {
                        property.animationCurveValue = curveValue;
                    }

                    break;
                case SerializedPropertyType.ObjectReference:
                    EditorGUI.ObjectField(rect, property, GUIContent.none);
                    break;
                default:
                    EditorGUI.LabelField(rect, "…", MissingValueStyle);
                    break;
            }

            EditorGUI.EndProperty();
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            if (detailAsset is LevelCatalog)
            {
                AddMenuItem(menu, "Level", "levels", "Add Level");
            }
            else if (detailAsset is WaveScheduleDefinition)
            {
                AddMenuItem(menu, "Wave", "waves", "Add Wave");
                var serialized = new SerializedObject(detailAsset);
                SerializedProperty waves = serialized.FindProperty("waves");
                for (int index = 0; waves != null && index < waves.arraySize; index++)
                {
                    string path = waves.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("spawnBatches")
                        .propertyPath;
                    AddMenuItem(
                        menu,
                        $"Spawn Batch/Wave {index + 1}",
                        path,
                        "Add Spawn Batch");
                }
            }
            else if (detailAsset is EnemyCatalog)
            {
                AddMenuItem(menu, "Enemy Slot", "definitions", "Add Enemy Slot");
            }
            else if (detailAsset is TowerCatalog)
            {
                AddMenuItem(menu, "Tower Slot", "definitions", "Add Tower Slot");
            }
            else if (detailAsset is ElementReactionCatalog)
            {
                AddMenuItem(menu, "Reaction Slot", "definitions", "Add Reaction Slot");
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("No add action for this asset"));
            }

            menu.ShowAsContext();
        }

        private void AddMenuItem(
            GenericMenu menu,
            string label,
            string arrayPath,
            string undoName)
        {
            menu.AddItem(
                new GUIContent(label),
                false,
                () => AddArrayElement(detailAsset, arrayPath, undoName));
        }

        private void AddArrayElement(
            ScriptableObject asset,
            string arrayPath,
            string undoName)
        {
            var serialized = new SerializedObject(asset);
            SerializedProperty array = serialized.FindProperty(arrayPath);
            if (array == null || !array.isArray)
            {
                return;
            }

            Undo.RecordObject(asset, undoName);
            array.arraySize++;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            isTableStale = true;
            RequestDetailAsset(asset);
        }

        private void DuplicateRow(BalanceRow row)
        {
            if (!TryGetRowArray(row, out SerializedObject serialized, out SerializedProperty array))
            {
                return;
            }

            Undo.RecordObject(row.Asset, "Duplicate Balance Item");
            array.InsertArrayElementAtIndex(row.ArrayIndex);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(row.Asset);
            isTableStale = true;
            RequestDetailAsset(row.Asset);
            GUIUtility.ExitGUI();
        }

        private void DeleteRow(BalanceRow row)
        {
            if (!TryGetRowArray(row, out SerializedObject serialized, out SerializedProperty array)
                || !EditorUtility.DisplayDialog(
                    "Delete Balance Item",
                    "Delete the selected authored item? This can be undone with Ctrl+Z.",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(row.Asset, "Delete Balance Item");
            int originalSize = array.arraySize;
            array.DeleteArrayElementAtIndex(row.ArrayIndex);
            if (array.arraySize == originalSize)
            {
                array.DeleteArrayElementAtIndex(row.ArrayIndex);
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(row.Asset);
            isTableStale = true;
            RequestDetailAsset(row.Asset);
            GUIUtility.ExitGUI();
        }

        private static bool TryGetRowArray(
            BalanceRow row,
            out SerializedObject serialized,
            out SerializedProperty array)
        {
            serialized = null;
            array = null;
            if (!row.CanModify)
            {
                return false;
            }

            serialized = new SerializedObject(row.Asset);
            array = serialized.FindProperty(row.ArrayPath);
            return array != null
                && array.isArray
                && row.ArrayIndex >= 0
                && row.ArrayIndex < array.arraySize;
        }

        private void DrawDetail()
        {
            if (detailAsset == null)
            {
                return;
            }

            if (!isDetailOnRight)
            {
                DrawSeparator();
            }

            using (new EditorGUILayout.VerticalScope(GetDetailPanelLayout()))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(detailAsset.name, EditorStyles.boldLabel);
                    if (detailAsset is BoardDefinition
                        && GUILayout.Button("Open Board Painter", GUILayout.Width(130f)))
                    {
                        BoardPainterWindow.Open((BoardDefinition)detailAsset);
                    }

                    if (GUILayout.Button("Ping", GUILayout.Width(50f)))
                    {
                        EditorGUIUtility.PingObject(detailAsset);
                    }

                    if (GUILayout.Button("Close", GUILayout.Width(50f)))
                    {
                        RequestDetailAsset(null);
                    }
                }

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                detailAssetEditor?.OnInspectorGUI();
                EditorGUILayout.EndScrollView();
            }
        }

        private GUILayoutOption GetDetailPanelLayout()
        {
            return isDetailOnRight
                ? GUILayout.Width(Mathf.Min(DetailPanelWidth, position.width * 0.55f))
                : GUILayout.Height(DetailPanelHeight);
        }

        private void DrawValidationMessages()
        {
            if (validationMessages.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);
                for (int index = 0; index < validationMessages.Count; index++)
                {
                    EditorGUILayout.HelpBox(validationMessages[index], MessageType.Warning);
                }
            }
        }

        private static void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.2f));
        }

        private void RebuildTable()
        {
            isTableStale = false;
            tabSerializedAssets.Clear();
            rows.Clear();
            columns.Clear();
            columnIndexByKey.Clear();
            rowCountByAsset.Clear();
            hasIndexColumn = false;

            useSections = false;
            var assets = new List<ScriptableObject>();
            FindAssetsForTab(selectedTab, assets);
            for (int index = 0; index < assets.Count; index++)
            {
                var serialized = new SerializedObject(assets[index]);
                tabSerializedAssets.Add(serialized);
                ExpandLevel(
                    serialized,
                    assets[index],
                    null,
                    string.Empty,
                    new Dictionary<string, string>(),
                    string.Empty,
                    null,
                    -1);
            }

            for (int index = 0; index < rows.Count; index++)
            {
                ScriptableObject asset = rows[index].Asset;
                rowCountByAsset.TryGetValue(asset, out int count);
                rowCountByAsset[asset] = count + 1;
            }

            useSections = false;
            showAssetColumn = false;
            foreach (int count in rowCountByAsset.Values)
            {
                if (count > 1)
                {
                    useSections = true;
                }
                else
                {
                    showAssetColumn = true;
                }
            }
        }

        private bool UsesSection(ScriptableObject asset) =>
            rowCountByAsset.TryGetValue(asset, out int count) && count > 1;

        /// <summary>
        /// Walks one nesting level: plain fields become columns, nested structs are flattened into
        /// more columns, and the first list of structs becomes rows. Fields of the enclosing level
        /// are carried into those rows, so a wave's reward stays readable next to each of its
        /// spawn batches and stays editable from any of them.
        /// </summary>
        private void ExpandLevel(
            SerializedObject serialized,
            ScriptableObject asset,
            SerializedProperty container,
            string keyPrefix,
            Dictionary<string, string> inheritedPaths,
            string indexLabel,
            string sourceArrayPath,
            int sourceArrayIndex)
        {
            var paths = new Dictionary<string, string>(inheritedPaths);
            SerializedProperty rowSource = null;
            List<SerializedProperty> children = GetChildren(serialized, container);
            for (int index = 0; index < children.Count; index++)
            {
                SerializedProperty child = children[index];
                if (rowSource == null && IsListOfStructs(child))
                {
                    rowSource = child;
                    continue;
                }

                CollectColumns(child, keyPrefix, paths);
            }

            if (rowSource == null)
            {
                rows.Add(new BalanceRow(
                    serialized,
                    asset,
                    asset.name,
                    indexLabel,
                    paths,
                    sourceArrayPath,
                    sourceArrayIndex));
                return;
            }

            hasIndexColumn = true;
            string childPrefix = keyPrefix + rowSource.name + ".";
            for (int index = 0; index < rowSource.arraySize; index++)
            {
                ExpandLevel(
                    serialized,
                    asset,
                    rowSource.GetArrayElementAtIndex(index),
                    childPrefix,
                    paths,
                    indexLabel.Length == 0 ? index.ToString() : indexLabel + "." + index,
                    rowSource.propertyPath,
                    index);
            }
        }

        private void CollectColumns(
            SerializedProperty property,
            string keyPrefix,
            Dictionary<string, string> paths)
        {
            if (property.propertyType == SerializedPropertyType.Generic
                && !property.isArray
                && property.hasVisibleChildren)
            {
                List<SerializedProperty> children = GetChildren(null, property);
                string childPrefix = keyPrefix + property.name + ".";
                for (int index = 0; index < children.Count; index++)
                {
                    CollectColumns(children[index], childPrefix, paths);
                }

                return;
            }

            string key = keyPrefix + property.name;
            paths[key] = property.propertyPath;
            if (columnIndexByKey.ContainsKey(key))
            {
                return;
            }

            columnIndexByKey.Add(key, columns.Count);
            columns.Add(new BalanceColumn(
                key,
                property.displayName,
                key.Replace(".", " ▸ "),
                MeasureColumnWidth(property)));
        }

        private static bool IsListOfStructs(SerializedProperty property)
        {
            return property.isArray
                && property.propertyType == SerializedPropertyType.Generic
                && property.arraySize > 0
                && property.GetArrayElementAtIndex(0).propertyType
                    == SerializedPropertyType.Generic;
        }

        private static List<SerializedProperty> GetChildren(
            SerializedObject serialized,
            SerializedProperty container)
        {
            var children = new List<SerializedProperty>();
            if (container == null)
            {
                SerializedProperty iterator = serialized.GetIterator();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = false;
                    if (iterator.propertyPath != "m_Script")
                    {
                        children.Add(iterator.Copy());
                    }
                }

                return children;
            }

            SerializedProperty end = container.GetEndProperty();
            SerializedProperty child = container.Copy();
            if (!child.NextVisible(true))
            {
                return children;
            }

            do
            {
                if (SerializedProperty.EqualContents(child, end))
                {
                    break;
                }

                children.Add(child.Copy());
            }
            while (child.NextVisible(false));

            return children;
        }

        /// <summary>
        /// Header labels wrap over two lines, so a column only has to be wide enough for its
        /// longest word rather than its whole name.
        /// </summary>
        private static float MeasureColumnWidth(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                case SerializedPropertyType.Vector4:
                case SerializedPropertyType.Rect:
                case SerializedPropertyType.Bounds:
                case SerializedPropertyType.Quaternion:
                    return WideValueColumnWidth;
                case SerializedPropertyType.String:
                case SerializedPropertyType.ObjectReference:
                    return MaximumColumnWidth;
                default:
                    return Mathf.Clamp(
                        MeasureLongestWord(property.displayName) * 7f + 12f,
                        MinimumColumnWidth,
                        MaximumColumnWidth);
            }
        }

        private static int MeasureLongestWord(string label)
        {
            int longest = 0;
            int current = 0;
            for (int index = 0; index < label.Length; index++)
            {
                if (label[index] == ' ')
                {
                    current = 0;
                    continue;
                }

                current++;
                if (current > longest)
                {
                    longest = current;
                }
            }

            return longest;
        }

        private void SelectDetailAsset(ScriptableObject asset)
        {
            if (detailAsset == asset)
            {
                return;
            }

            DestroyDetailAssetEditor();
            detailAsset = asset;
            if (detailAsset != null)
            {
                detailAssetEditor = UnityEditor.Editor.CreateEditor(detailAsset);
            }

            detailScroll = Vector2.zero;
        }

        private void DestroyDetailAssetEditor()
        {
            if (detailAssetEditor != null)
            {
                DestroyImmediate(detailAssetEditor);
                detailAssetEditor = null;
            }
        }

        /// <summary>
        /// Board definitions are authored in the Board Painter, not here, so they stay out of the
        /// table even though they share the Grid Placement folder with the tower placements.
        /// </summary>
        private static void FindAssetsForTab(BalanceTab tab, List<ScriptableObject> assets)
        {
            string[] folders = tab switch
            {
                BalanceTab.Levels => new[] { "Assets/Config/GameFlow" },
                BalanceTab.Waves => new[] { "Assets/Config/Waves" },
                BalanceTab.Enemies => new[] { "Assets/Config/Enemies" },
                BalanceTab.Towers => new[]
                {
                    "Assets/Config/Towers",
                    "Assets/Config/GridPlacement"
                },
                BalanceTab.ElementalReactions => new[] { "Assets/Config/Combat" },
                _ => Array.Empty<string>()
            };
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", folders);
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset != null
                    && !(asset is BoardDefinition)
                    && (tab != BalanceTab.Towers || asset is TowerCombatDefinition))
                {
                    assets.Add(asset);
                }
            }

            assets.Sort((left, right) => string.CompareOrdinal(
                left.GetType().Name + "/" + AssetDatabase.GetAssetPath(left),
                right.GetType().Name + "/" + AssetDatabase.GetAssetPath(right)));
        }

        private static List<string> CollectValidationMessages()
        {
            var messages = new List<string>();
            ValidateCatalog<EnemyCatalog>(
                "Assets/Config/Enemies",
                catalog => catalog.CollectValidationErrors(),
                messages);
            ValidateCatalog<ElementReactionCatalog>(
                "Assets/Config/Combat",
                catalog => catalog.CollectValidationErrors(),
                messages);
            ValidateCatalog<WaveScheduleDefinition>(
                "Assets/Config/Waves",
                schedule => schedule.CollectValidationErrors(),
                messages);

            LevelCatalog levelCatalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(
                LevelCatalogValidator.DefaultCatalogPath);
            messages.AddRange(LevelCatalogValidator.CollectErrors(levelCatalog));

            if (messages.Count == 0)
            {
                messages.Add("All checked balance assets are valid.");
            }

            return messages;
        }

        private static void ValidateCatalog<T>(
            string folder,
            Func<T, IReadOnlyList<string>> getErrors,
            List<string> messages)
            where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder });
            for (int index = 0; index < guids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                IReadOnlyList<string> errors = getErrors(asset);
                for (int errorIndex = 0; errorIndex < errors.Count; errorIndex++)
                {
                    messages.Add($"{asset.name}: {errors[errorIndex]}");
                }
            }
        }

        private readonly struct BalanceColumn
        {
            public BalanceColumn(string key, string label, string tooltip, float width)
            {
                Key = key;
                Label = label;
                Tooltip = tooltip;
                Width = width;
            }

            public string Key { get; }
            public string Label { get; }
            public string Tooltip { get; }
            public float Width { get; }
        }

        private sealed class BalanceRow
        {
            public BalanceRow(
                SerializedObject serialized,
                ScriptableObject asset,
                string assetName,
                string indexLabel,
                Dictionary<string, string> pathByColumnKey,
                string arrayPath,
                int arrayIndex)
            {
                Serialized = serialized;
                Asset = asset;
                AssetName = assetName;
                IndexLabel = indexLabel;
                PathByColumnKey = pathByColumnKey;
                ArrayPath = arrayPath;
                ArrayIndex = arrayIndex;
            }

            public SerializedObject Serialized { get; }
            public ScriptableObject Asset { get; }
            public string AssetName { get; }
            public string IndexLabel { get; }
            public Dictionary<string, string> PathByColumnKey { get; }
            public string ArrayPath { get; }
            public int ArrayIndex { get; }
            public bool CanModify => !string.IsNullOrEmpty(ArrayPath) && ArrayIndex >= 0;
        }
    }
}
