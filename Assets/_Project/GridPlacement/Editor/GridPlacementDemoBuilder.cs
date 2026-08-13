#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using TowerDefense3D.Mobile;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense3D.GridPlacement.Editor
{
    /// <summary>
    /// Rebuilds the minimal project-owned SampleScene demonstration deterministically.
    /// </summary>
    public static class GridPlacementDemoBuilder
    {
        private const string DataFolder = "Assets/_Project/GridPlacement/Data";
        private const string PrefabFolder = "Assets/_Project/GridPlacement/Prefabs";
        private const string MaterialFolder = "Assets/_Project/GridPlacement/Materials";

        [MenuItem("Tools/Tower Defense/Rebuild Grid Placement Demo")]
        public static void Build()
        {
            EnsureFolder("Assets/_Project/GridPlacement");
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            Material previewMaterial = CreateMaterial(
                MaterialFolder + "/PreviewTransparent.mat",
                new Color(0.15f, 1f, 0.25f, 0.38f),
                true);
            Material groundMaterial = CreateMaterial(
                MaterialFolder + "/Ground.mat",
                new Color(0.18f, 0.42f, 0.24f, 1f),
                false);
            Material height1Material = CreateMaterial(
                MaterialFolder + "/Height1.mat",
                new Color(0.18f, 0.5f, 0.54f, 1f),
                false);
            Material height2Material = CreateMaterial(
                MaterialFolder + "/Height2.mat",
                new Color(0.28f, 0.36f, 0.68f, 1f),
                false);
            Material blockerMaterial = CreateMaterial(
                MaterialFolder + "/Blocker.mat",
                new Color(0.8f, 0.22f, 0.16f, 1f),
                false);
            Material towerBaseMaterial = CreateMaterial(
                MaterialFolder + "/TowerBase.mat",
                new Color(0.14f, 0.18f, 0.25f, 1f),
                false);
            Material towerAccentMaterial = CreateMaterial(
                MaterialFolder + "/TowerAccent.mat",
                new Color(1f, 0.68f, 0.12f, 1f),
                false);

            BoardDefinition boardDefinition = CreateBoardDefinition();
            GameObject towerPrefab = CreateTowerPrefab(towerBaseMaterial, towerAccentMaterial);
            TowerDefinition towerDefinition = CreateTowerDefinition(towerPrefab);

            DestroySceneObject("Grid Placement Demo");
            DestroySceneObject("Placement UI");
            DestroySceneObject("EventSystem");

            GameObject demoRoot = new GameObject("Grid Placement Demo");
            Undo.RegisterCreatedObjectUndo(demoRoot, "Create Grid Placement Demo");

            GameObject boardOrigin = CreateChild("Board Origin", demoRoot.transform);
            CreatePlatform(
                "Ground (Height 0)",
                boardOrigin.transform,
                new Vector3(5f, -0.1f, 4f),
                new Vector3(10f, 0.2f, 8f),
                groundMaterial);
            CreatePlatform(
                "Platform (Height 1)",
                boardOrigin.transform,
                new Vector3(2.5f, 0.75f, 2.5f),
                new Vector3(3f, 0.5f, 3f),
                height1Material);
            CreatePlatform(
                "Platform (Height 2)",
                boardOrigin.transform,
                new Vector3(7.5f, 1.75f, 5.5f),
                new Vector3(3f, 0.5f, 3f),
                height2Material);

            GameObject blockers = CreateChild("Static Blockers", boardOrigin.transform);
            CreatePlatform(
                "Ground Blocker",
                blockers.transform,
                new Vector3(8.5f, 0.45f, 1.5f),
                new Vector3(0.72f, 0.9f, 0.72f),
                blockerMaterial);
            CreatePlatform(
                "Height 1 Blocker",
                blockers.transform,
                new Vector3(1.5f, 1.45f, 1.5f),
                new Vector3(0.72f, 0.9f, 0.72f),
                blockerMaterial);
            CreatePlatform(
                "Height 2 Blocker",
                blockers.transform,
                new Vector3(8.5f, 2.45f, 6.5f),
                new Vector3(0.72f, 0.9f, 0.72f),
                blockerMaterial);

            GameObject placedRoot = CreateChild("Placed Towers", demoRoot.transform);
            GridPlacementPreview preview = CreatePreview(demoRoot.transform, previewMaterial);
            GridPlacementController controller = CreateSystems(
                demoRoot.transform,
                boardOrigin.transform,
                placedRoot.transform,
                boardDefinition,
                towerDefinition,
                preview);

            BuildUi(controller, towerDefinition);
            ConfigureCameraAndLight();

            Selection.activeGameObject = demoRoot;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(
                SceneManager.GetActiveScene(),
                "Assets/Scenes/SampleScene.unity");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Grid Placement demo rebuilt: 10x8x5 board, Y=0/1/2 surfaces, "
                + "2x2x2 tower, combined preview and Safe Area UI.");
        }

        private static BoardDefinition CreateBoardDefinition()
        {
            const string path = DataFolder + "/DemoBoard.asset";
            BoardDefinition asset = AssetDatabase.LoadAssetAtPath<BoardDefinition>(path);
            bool isNewAsset = asset == null;
            if (isNewAsset)
            {
                asset = ScriptableObject.CreateInstance<BoardDefinition>();
            }

            var cells = new List<BoardCellDefinition>(98);
            BoardCellFlags buildable =
                BoardCellFlags.SupportsPlacement | BoardCellFlags.Buildable;

            for (int z = 0; z < 8; z++)
            {
                for (int x = 0; x < 10; x++)
                {
                    BoardCellFlags flags = buildable;
                    if (x == 8 && z == 1)
                    {
                        flags |= BoardCellFlags.StaticBlocker;
                    }

                    cells.Add(new BoardCellDefinition(new GridCell(x, z, 0), flags));
                }
            }

            AddRaisedSurface(cells, 1, 3, 1, 3, 1, new GridCell(1, 1, 1), buildable);
            AddRaisedSurface(cells, 6, 8, 4, 6, 2, new GridCell(8, 6, 2), buildable);

            SetPrivateField(asset, "dimensions", new GridDimensions(10, 8, 5));
            SetPrivateField(asset, "cellSize", 1f);
            SetPrivateField(asset, "heightUnit", 1f);
            SetPrivateField(asset, "cells", cells.ToArray());
            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(asset, path);
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void AddRaisedSurface(
            ICollection<BoardCellDefinition> cells,
            int minX,
            int maxX,
            int minZ,
            int maxZ,
            int y,
            GridCell blocker,
            BoardCellFlags buildable)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    BoardCellFlags flags = buildable;
                    if (x == blocker.X && z == blocker.Z && y == blocker.Y)
                    {
                        flags |= BoardCellFlags.StaticBlocker;
                    }

                    cells.Add(new BoardCellDefinition(new GridCell(x, z, y), flags));
                }
            }
        }

        private static TowerDefinition CreateTowerDefinition(GameObject prefab)
        {
            const string path = DataFolder + "/BasicTower.asset";
            TowerDefinition asset = AssetDatabase.LoadAssetAtPath<TowerDefinition>(path);
            bool isNewAsset = asset == null;
            if (isNewAsset)
            {
                asset = ScriptableObject.CreateInstance<TowerDefinition>();
            }

            SetPrivateField(asset, "prefab", prefab);
            SetPrivateField(asset, "footprint", new TowerFootprint(2, 2, 2));
            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(asset, path);
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject CreateTowerPrefab(Material baseMaterial, Material accentMaterial)
        {
            GameObject root = new GameObject("BasicTower");
            AddTowerPart(
                root.transform,
                PrimitiveType.Cylinder,
                "Base",
                new Vector3(0f, 0.22f, 0f),
                new Vector3(0.72f, 0.22f, 0.72f),
                baseMaterial);
            AddTowerPart(
                root.transform,
                PrimitiveType.Cylinder,
                "Body",
                new Vector3(0f, 0.88f, 0f),
                new Vector3(0.34f, 0.55f, 0.34f),
                accentMaterial);
            AddTowerPart(
                root.transform,
                PrimitiveType.Sphere,
                "Head",
                new Vector3(0f, 1.48f, 0f),
                Vector3.one * 0.52f,
                baseMaterial);
            AddTowerPart(
                root.transform,
                PrimitiveType.Cube,
                "Barrel",
                new Vector3(0f, 1.5f, 0.48f),
                new Vector3(0.18f, 0.18f, 0.72f),
                accentMaterial);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                root,
                PrefabFolder + "/BasicTower.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AddTowerPart(
            Transform parent,
            PrimitiveType primitive,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static GridPlacementPreview CreatePreview(Transform parent, Material material)
        {
            GameObject root = CreateChild("Placement Preview", parent);
            GridPlacementPreview preview = root.AddComponent<GridPlacementPreview>();
            GameObject footprint = CreateChild("Footprint", root.transform);
            MeshFilter footprintFilter = footprint.AddComponent<MeshFilter>();
            MeshRenderer footprintRenderer = footprint.AddComponent<MeshRenderer>();
            footprintRenderer.sharedMaterial = material;
            GameObject ghost = CreateChild("Ghost Volume", root.transform);
            MeshFilter ghostFilter = ghost.AddComponent<MeshFilter>();
            MeshRenderer ghostRenderer = ghost.AddComponent<MeshRenderer>();
            ghostRenderer.sharedMaterial = material;

            SerializedObject serialized = new SerializedObject(preview);
            serialized.FindProperty("footprintMeshFilter").objectReferenceValue = footprintFilter;
            serialized.FindProperty("footprintRenderer").objectReferenceValue = footprintRenderer;
            serialized.FindProperty("ghostMeshFilter").objectReferenceValue = ghostFilter;
            serialized.FindProperty("ghostRenderer").objectReferenceValue = ghostRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return preview;
        }

        private static GridPlacementController CreateSystems(
            Transform parent,
            Transform boardOrigin,
            Transform placedRoot,
            BoardDefinition boardDefinition,
            TowerDefinition towerDefinition,
            GridPlacementPreview preview)
        {
            GameObject systems = CreateChild("Systems", parent);
            GridPlacementController controller = systems.AddComponent<GridPlacementController>();
            systems.AddComponent<MobileFrameRatePolicy>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("boardDefinition").objectReferenceValue = boardDefinition;
            serialized.FindProperty("boardOrigin").objectReferenceValue = boardOrigin;
            serialized.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            serialized.FindProperty("placementSurfaceMask").intValue = ~0;
            serialized.FindProperty("maxRayDistance").floatValue = 300f;
            serialized.FindProperty("preview").objectReferenceValue = preview;
            serialized.FindProperty("placedObjectsRoot").objectReferenceValue = placedRoot;
            serialized.FindProperty("initialTower").objectReferenceValue = towerDefinition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return controller;
        }

        private static void BuildUi(
            GridPlacementController controller,
            TowerDefinition towerDefinition)
        {
            GameObject canvasObject = new GameObject(
                "Placement UI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Placement UI");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject safeArea = CreateUiObject("Safe Area", canvasObject.transform);
            Stretch(safeArea.GetComponent<RectTransform>());
            SafeAreaFitter safeAreaFitter = safeArea.AddComponent<SafeAreaFitter>();
            SerializedObject safeAreaSerialized = new SerializedObject(safeAreaFitter);
            safeAreaSerialized.FindProperty("target").objectReferenceValue =
                safeArea.GetComponent<RectTransform>();
            safeAreaSerialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject instructions = CreateUiObject("Instructions", safeArea.transform);
            RectTransform instructionRect = instructions.GetComponent<RectTransform>();
            instructionRect.anchorMin = new Vector2(0.5f, 1f);
            instructionRect.anchorMax = new Vector2(0.5f, 1f);
            instructionRect.pivot = new Vector2(0.5f, 1f);
            instructionRect.anchoredPosition = new Vector2(0f, -24f);
            instructionRect.sizeDelta = new Vector2(960f, 90f);
            UnityEngine.UI.Image instructionImage =
                instructions.AddComponent<UnityEngine.UI.Image>();
            instructionImage.color = new Color(0.03f, 0.04f, 0.065f, 0.82f);
            Text instructionText = CreateText(
                "Label",
                instructions.transform,
                "DRAG ON A PLATFORM  -  RELEASE TO PLACE  -  GREEN = VALID",
                30);
            instructionText.color = new Color(0.91f, 0.95f, 1f, 1f);

            Button selectButton = CreateButton(
                "Select Tower",
                safeArea.transform,
                "PLACE 2x2x2",
                new Color(0.12f, 0.58f, 0.28f, 0.95f),
                false);
            TowerSelectionButton selectionAdapter =
                selectButton.gameObject.AddComponent<TowerSelectionButton>();
            SerializedObject selectionSerialized = new SerializedObject(selectionAdapter);
            selectionSerialized.FindProperty("button").objectReferenceValue = selectButton;
            selectionSerialized.FindProperty("controller").objectReferenceValue = controller;
            selectionSerialized.FindProperty("towerDefinition").objectReferenceValue =
                towerDefinition;
            selectionSerialized.ApplyModifiedPropertiesWithoutUndo();

            Button cancelButton = CreateButton(
                "Cancel",
                safeArea.transform,
                "CANCEL",
                new Color(0.72f, 0.16f, 0.14f, 0.95f),
                true);
            UnityEventTools.AddPersistentListener(
                cancelButton.onClick,
                controller.CancelPlacement);
            EditorUtility.SetDirty(cancelButton);

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Color color,
            bool alignRight)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = alignRight ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = alignRight ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
            rect.anchoredPosition = alignRight ? new Vector2(-40f, 40f) : new Vector2(40f, 40f);
            rect.sizeDelta = new Vector2(320f, 112f);
            UnityEngine.UI.Image image = buttonObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.15f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            CreateText("Label", buttonObject.transform, label, 36);
            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize)
        {
            GameObject textObject = CreateUiObject(name, parent);
            Stretch(textObject.GetComponent<RectTransform>());
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void ConfigureCameraAndLight()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(12.5f, 13.5f, -10.5f);
            camera.transform.rotation = Quaternion.LookRotation(
                new Vector3(5f, 0.8f, 4f) - camera.transform.position,
                Vector3.up);
            camera.fieldOfView = 43f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.07f, 0.105f, 1f);

            Light directionalLight = Object.FindFirstObjectByType<Light>();
            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                directionalLight.intensity = 1.25f;
                directionalLight.color = new Color(1f, 0.94f, 0.84f);
            }
        }

        private static GameObject CreatePlatform(
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            platform.transform.SetParent(parent, false);
            platform.transform.localPosition = localPosition;
            platform.transform.localScale = localScale;
            platform.GetComponent<MeshRenderer>().sharedMaterial = material;
            return platform;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Material CreateMaterial(string path, Color color, bool transparent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNewAsset = material == null;
            if (isNewAsset)
            {
                material = new Material(shader);
            }
            else
            {
                material.shader = shader;
            }

            material.name = System.IO.Path.GetFileNameWithoutExtension(path);
            material.color = color;
            material.SetColor("_BaseColor", color);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(material, path);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetPrivateField<T>(Object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new System.MissingFieldException(target.GetType().FullName, fieldName);
            }

            field.SetValue(target, value);
        }

        private static void DestroySceneObject(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject != null)
            {
                Undo.DestroyObjectImmediate(gameObject);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
#endif
