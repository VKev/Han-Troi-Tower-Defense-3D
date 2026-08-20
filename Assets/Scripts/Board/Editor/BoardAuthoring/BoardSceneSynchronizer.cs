using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GridPlacement.Editor
{
    public static class BoardSceneSynchronizer
    {
        internal const string GeneratedRootName = "Board Visualization";
        internal const string PlaceableAreaName = "Placeable Area";
        internal const string BlockedAreaName = "Blocked Area";
        internal const string CameraFocusRegionName = "Camera Focus Region";
        internal const string RoadAreaName = "Road Area";
        internal const string RoadSpawnAreaName = "Road Spawn Area";
        internal const string RoadEndAreaName = "Road End Area";
        internal const string GeneratedGridPlaceableRootName =
            "Generated Grid Placeables";
        internal const int BoardVisualizationSortingOrder = -100;
        private const string LegacyGeneratedRootName = "__Generated Board Geometry";
        private const string LegacyGeneratedRoadVisualRootName =
            "Generated Road Visuals";
        private const string GeneratedSignaturePropertyName = "generatedSignature";
        private const string GeneratedGridPlaceableRootPropertyName =
            "generatedGridPlaceableRoot";
        private const string GeneratedGridPlaceableSignaturePropertyName =
            "generatedGridPlaceableSignature";
        private const string GroundMaterialPath =
            "Assets/Resources/Materials/BoardSurface.mat";
        private const string BlockerMaterialPath =
            "Assets/Resources/Materials/Blocker.mat";
        private const string CameraFocusMaterialPath =
            "Assets/Resources/Materials/CameraFocusRegion.mat";
        private const string RoadMaterialPath =
            "Assets/Resources/Materials/Road.mat";
        private const string RoadSpawnMaterialPath =
            "Assets/Resources/Materials/RoadSpawn.mat";
        private const string RoadEndMaterialPath =
            "Assets/Resources/Materials/RoadEnd.mat";
        private const float CameraFocusOverlayLift = 0.01f;
        private const float SurfaceThickness = 0.05f;

        public static void Synchronize(BoardDefinition board)
        {
            if (board == null || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling)
            {
                return;
            }

            BoardGeometryPlan plan = BoardGeometryPlanner.Create(board);
            BoardScenePresenter[] presenters =
                Resources.FindObjectsOfTypeAll<BoardScenePresenter>();

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Synchronize Board Geometry");

            try
            {
                for (int index = 0; index < presenters.Length; index++)
                {
                    BoardScenePresenter presenter = presenters[index];
                    if (!IsLoadedMatch(presenter, board))
                    {
                        continue;
                    }

                    SynchronizePresenter(presenter, board, plan);
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        private static bool IsLoadedMatch(
            BoardScenePresenter presenter,
            BoardDefinition board)
        {
            if (presenter == null || presenter.Board != board
                || EditorUtility.IsPersistent(presenter))
            {
                return false;
            }

            Scene scene = presenter.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static void SynchronizePresenter(
            BoardScenePresenter presenter,
            BoardDefinition board,
            BoardGeometryPlan plan)
        {
            bool changed = SynchronizeDebugGeometry(presenter, board, plan);
            changed |= SynchronizeGridPlaceables(presenter, plan);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            }

            BoardCameraAuthoringSynchronizer.Synchronize(presenter);
        }

        private static bool SynchronizeDebugGeometry(
            BoardScenePresenter presenter,
            BoardDefinition board,
            BoardGeometryPlan plan)
        {
            Transform root = FindOwnedRoot(presenter);
            bool rootRenamed = RenameOwnedRoot(root);
            if (root != null && HasMatchingGeometry(presenter, root, plan))
            {
                bool changed = rootRenamed;
                changed |= ApplyComponentState(root, board.VisualizeInScene);
                changed |= AssignGeneratedState(presenter, root, plan.Signature);
                return changed;
            }

            if (root == null)
            {
                var rootObject = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(
                    rootObject,
                    "Create Board Geometry Root");
                root = rootObject.transform;
                root.SetParent(presenter.transform, false);
            }

            for (int childIndex = root.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                Undo.DestroyObjectImmediate(
                    root.GetChild(childIndex).gameObject);
            }

            Material groundMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            Material blockerMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(BlockerMaterialPath);
            Material cameraFocusMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    CameraFocusMaterialPath);
            Material roadMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(RoadMaterialPath);
            Material roadSpawnMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    RoadSpawnMaterialPath);
            Material roadEndMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    RoadEndMaterialPath);

            for (int index = 0; index < plan.Rectangles.Count; index++)
            {
                CreateRectangle(
                    root,
                    plan.Rectangles[index],
                    board,
                    groundMaterial,
                    blockerMaterial,
                    roadMaterial,
                    roadSpawnMaterial,
                    roadEndMaterial);
            }

            if (plan.FocusRegion.HasValue)
            {
                CreateCameraFocusRegion(
                    root,
                    plan.FocusRegion.Value,
                    board,
                    cameraFocusMaterial);
            }

            ApplyComponentState(root, board.VisualizeInScene);
            AssignGeneratedState(presenter, root, plan.Signature);
            return true;
        }

        private static bool SynchronizeGridPlaceables(
            BoardScenePresenter presenter,
            BoardGeometryPlan plan)
        {
            Transform root = FindOwnedGridPlaceableRoot(presenter);
            bool rootRenamed = RenameOwnedGridPlaceableRoot(root);
            if (plan.GridPlaceableVisuals.Count == 0)
            {
                bool changed = AssignGridPlaceableState(
                    presenter,
                    null,
                    string.Empty);
                if (root != null)
                {
                    Undo.DestroyObjectImmediate(root.gameObject);
                    changed = true;
                }

                return changed;
            }

            if (root != null
                && HasMatchingGridPlaceables(
                    presenter,
                    root,
                    plan,
                    plan.GridPlaceableSignature))
            {
                bool changed = rootRenamed;
                changed |= AssignGridPlaceableState(
                    presenter,
                    root,
                    plan.GridPlaceableSignature);
                for (int index = 0; index < root.childCount; index++)
                {
                    changed |= ApplyStaticHierarchy(root.GetChild(index));
                }

                return changed;
            }

            if (root == null)
            {
                var rootObject = new GameObject(
                    GeneratedGridPlaceableRootName);
                Undo.RegisterCreatedObjectUndo(
                    rootObject,
                    "Create Generated Grid Placeables Root");
                root = rootObject.transform;
                root.SetParent(presenter.transform, false);
            }

            for (int childIndex = root.childCount - 1;
                 childIndex >= 0;
                 childIndex--)
            {
                Undo.DestroyObjectImmediate(
                    root.GetChild(childIndex).gameObject);
            }

            for (int index = 0;
                 index < plan.GridPlaceableVisuals.Count;
                 index++)
            {
                CreateGridPlaceableVisual(
                    root,
                    plan.GridPlaceableVisuals[index]);
            }

            AssignGridPlaceableState(
                presenter,
                root,
                plan.GridPlaceableSignature);
            return true;
        }

        private static bool HasMatchingGridPlaceables(
            BoardScenePresenter presenter,
            Transform root,
            BoardGeometryPlan plan,
            string signature)
        {
            if (ReadGeneratedGridPlaceableSignature(presenter) != signature
                || root.childCount != plan.GridPlaceableVisuals.Count)
            {
                return false;
            }

            for (int index = 0;
                 index < plan.GridPlaceableVisuals.Count;
                 index++)
            {
                Transform child = root.GetChild(index);
                BoardGridPlaceableVisual visual =
                    plan.GridPlaceableVisuals[index];
                if (child.name != GetGridPlaceableVisualName(visual)
                    || PrefabUtility
                        .GetCorrespondingObjectFromOriginalSource(
                            child.gameObject) != visual.Prefab
                    || !Approximately(
                        child.localPosition,
                        visual.LocalPosition)
                    || !Approximately(
                        child.localRotation,
                        visual.LocalRotation)
                    || !Approximately(
                        child.localScale,
                        visual.LocalScale)
                    || !HasMatchingSharedMaterials(
                        child,
                        visual.Prefab.transform)
                    || !HasMatchingSortingOrder(
                        child,
                        visual.SortingOrder))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasMatchingSharedMaterials(
            Transform instanceRoot,
            Transform prefabRoot)
        {
            Renderer[] instanceRenderers =
                instanceRoot.GetComponentsInChildren<Renderer>(true);
            Renderer[] prefabRenderers =
                prefabRoot.GetComponentsInChildren<Renderer>(true);
            if (instanceRenderers.Length != prefabRenderers.Length)
            {
                return false;
            }

            for (int rendererIndex = 0;
                 rendererIndex < instanceRenderers.Length;
                 rendererIndex++)
            {
                Renderer instanceRenderer =
                    instanceRenderers[rendererIndex];
                Renderer prefabRenderer =
                    prefabRenderers[rendererIndex];
                if (instanceRenderer.enabled != prefabRenderer.enabled
                    || instanceRenderer.shadowCastingMode
                        != prefabRenderer.shadowCastingMode
                    || instanceRenderer.receiveShadows
                        != prefabRenderer.receiveShadows)
                {
                    return false;
                }

                Material[] instanceMaterials =
                    instanceRenderer.sharedMaterials;
                Material[] prefabMaterials =
                    prefabRenderer.sharedMaterials;
                if (instanceMaterials.Length != prefabMaterials.Length)
                {
                    return false;
                }

                for (int materialIndex = 0;
                     materialIndex < instanceMaterials.Length;
                     materialIndex++)
                {
                    if (instanceMaterials[materialIndex]
                        != prefabMaterials[materialIndex])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool HasMatchingSortingOrder(
            Transform root,
            int sortingOrder)
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].sortingOrder != sortingOrder)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CreateGridPlaceableVisual(
            Transform root,
            BoardGridPlaceableVisual visual)
        {
            var instance = PrefabUtility.InstantiatePrefab(
                visual.Prefab,
                root.gameObject.scene) as GameObject;
            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(
                instance,
                "Create Grid Placeable Visual");
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(root, false);
            instance.name = GetGridPlaceableVisualName(visual);
            instanceTransform.localPosition = visual.LocalPosition;
            instanceTransform.localRotation = visual.LocalRotation;
            instanceTransform.localScale = visual.LocalScale;
            ApplyStaticHierarchy(instanceTransform);
            ApplyRendererSortingOrder(
                instanceTransform,
                visual.SortingOrder,
                "Order Grid Placeable Visualization");
        }

        private static bool ApplyStaticHierarchy(Transform root)
        {
            bool changed = false;
            Transform[] hierarchy =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < hierarchy.Length; index++)
            {
                GameObject gameObject = hierarchy[index].gameObject;
                if (gameObject.isStatic)
                {
                    continue;
                }

                Undo.RecordObject(
                    gameObject,
                    "Make Grid Placeable Static");
                gameObject.isStatic = true;
                changed = true;
            }

            return changed;
        }

        private static string GetGridPlaceableVisualName(
            BoardGridPlaceableVisual visual)
        {
            return string.Format(
                "{0} Cell ({1}, {2}, {3})",
                visual.DisplayName,
                visual.Coordinate.X,
                visual.Coordinate.Y,
                visual.Coordinate.Z);
        }

        private static bool Approximately(
            Vector3 left,
            Vector3 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }

        private static bool Approximately(
            Quaternion left,
            Quaternion right)
        {
            return Mathf.Abs(Quaternion.Dot(left, right))
                >= 0.999999f;
        }

        private static bool HasMatchingGeometry(
            BoardScenePresenter presenter,
            Transform root,
            BoardGeometryPlan plan)
        {
            if (ReadGeneratedSignature(presenter) != plan.Signature)
            {
                return false;
            }

            int expectedChildCount = plan.Rectangles.Count + (plan.FocusRegion.HasValue ? 1 : 0);
            if (root.childCount != expectedChildCount)
            {
                return false;
            }

            for (int index = 0; index < plan.Rectangles.Count; index++)
            {
                Transform child = root.GetChild(index);
                BoardGeometryRectangle rectangle = plan.Rectangles[index];
                if (child.name != GetRectangleName(rectangle)
                    || child.GetComponent<MeshRenderer>() == null)
                {
                    return false;
                }

                if (IsRoadKind(rectangle.Kind))
                {
                    // Road/Spawn/End overlays carry no gameplay function yet and
                    // must not expose a Collider.
                    if (child.GetComponent<Collider>() != null)
                    {
                        return false;
                    }
                }
                else if (child.GetComponent<BoxCollider>() == null)
                {
                    return false;
                }
            }

            if (plan.FocusRegion.HasValue)
            {
                Transform focusChild = root.GetChild(plan.Rectangles.Count);
                if (focusChild.name != CameraFocusRegionName
                    || focusChild.GetComponent<MeshRenderer>() == null
                    || focusChild.GetComponent<Collider>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static Transform FindOwnedRoot(BoardScenePresenter presenter)
        {
            Transform presenterTransform = presenter.transform;
            Transform assignedRoot = presenter.GeneratedRoot;
            if (assignedRoot != null && assignedRoot.parent == presenterTransform)
            {
                return assignedRoot;
            }

            for (int index = 0; index < presenterTransform.childCount; index++)
            {
                Transform child = presenterTransform.GetChild(index);
                if (child.name == GeneratedRootName
                    || child.name == LegacyGeneratedRootName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Transform FindOwnedGridPlaceableRoot(
            BoardScenePresenter presenter)
        {
            Transform presenterTransform = presenter.transform;
            Transform assignedRoot =
                presenter.GeneratedGridPlaceableRoot;
            if (assignedRoot != null
                && assignedRoot.parent == presenterTransform)
            {
                return assignedRoot;
            }

            for (int index = 0;
                 index < presenterTransform.childCount;
                 index++)
            {
                Transform child =
                    presenterTransform.GetChild(index);
                if (child.name == GeneratedGridPlaceableRootName
                    || child.name == LegacyGeneratedRoadVisualRootName)
                {
                    return child;
                }
            }

            return null;
        }

        private static bool RenameOwnedGridPlaceableRoot(Transform root)
        {
            if (root == null
                || root.name == GeneratedGridPlaceableRootName)
            {
                return false;
            }

            Undo.RecordObject(
                root.gameObject,
                "Rename Grid Placeable Visualization");
            root.name = GeneratedGridPlaceableRootName;
            return true;
        }

        private static bool RenameOwnedRoot(Transform root)
        {
            if (root == null || root.name == GeneratedRootName)
            {
                return false;
            }

            Undo.RecordObject(root.gameObject, "Rename Board Visualization");
            root.name = GeneratedRootName;
            return true;
        }

        private static void CreateRectangle(
            Transform root,
            BoardGeometryRectangle rectangle,
            BoardDefinition board,
            Material groundMaterial,
            Material blockerMaterial,
            Material roadMaterial,
            Material roadSpawnMaterial,
            Material roadEndMaterial)
        {
            bool isPlacementSurface = rectangle.Kind == BoardGeometryKind.PlacementSurface;
            bool isRoadKind = IsRoadKind(rectangle.Kind);
            bool isThinSlab = isPlacementSurface || isRoadKind;

            float maximumSurfaceThickness =
                Mathf.Min(board.CellSize, board.HeightUnit) * 0.1f;
            float thinSlabThickness = Mathf.Max(
                0.001f,
                Mathf.Min(SurfaceThickness, maximumSurfaceThickness));
            float height = isThinSlab ? thinSlabThickness : board.HeightUnit;
            float top = rectangle.Y * board.HeightUnit;
            float centerY;
            if (isPlacementSurface)
            {
                centerY = top - height * 0.5f;
            }
            else if (isRoadKind)
            {
                // Sits just above the level's ground plane so it does not
                // z-fight with a placement-surface slab on the same cell.
                centerY = top + CameraFocusOverlayLift + height * 0.5f;
            }
            else
            {
                centerY = top + height * 0.5f;
            }

            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Cube);
            generated.name = GetRectangleName(rectangle);
            Undo.RegisterCreatedObjectUndo(generated, "Create Board Geometry");
            generated.transform.SetParent(root, false);
            generated.transform.localPosition = new Vector3(
                (rectangle.X + rectangle.Width * 0.5f) * board.CellSize,
                centerY,
                (rectangle.Z + rectangle.Depth * 0.5f) * board.CellSize);
            generated.transform.localRotation = Quaternion.identity;
            generated.transform.localScale = new Vector3(
                rectangle.Width * board.CellSize,
                height,
                rectangle.Depth * board.CellSize);

            MeshRenderer renderer = generated.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(
                rectangle.Kind,
                groundMaterial,
                blockerMaterial,
                roadMaterial,
                roadSpawnMaterial,
                roadEndMaterial);
            renderer.enabled = board.VisualizeInScene;
            renderer.sortingOrder = BoardVisualizationSortingOrder;

            if (isRoadKind)
            {
                // Road/Spawn/End cells have no gameplay function yet; the
                // generated overlay must not be mistaken for a physical
                // obstacle or double up with a placement/blocker collider
                // already on the same cell.
                Collider generatedCollider = generated.GetComponent<Collider>();
                if (generatedCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(generatedCollider);
                }
            }
            else
            {
                BoxCollider collider = generated.GetComponent<BoxCollider>();
                collider.enabled = true;
            }
        }

        private static bool IsRoadKind(BoardGeometryKind kind)
        {
            return kind == BoardGeometryKind.RoadSurface
                || kind == BoardGeometryKind.RoadSpawnSurface
                || kind == BoardGeometryKind.RoadEndSurface;
        }

        private static Material GetMaterial(
            BoardGeometryKind kind,
            Material groundMaterial,
            Material blockerMaterial,
            Material roadMaterial,
            Material roadSpawnMaterial,
            Material roadEndMaterial)
        {
            switch (kind)
            {
                case BoardGeometryKind.PlacementSurface:
                    return groundMaterial;
                case BoardGeometryKind.RoadSurface:
                    return roadMaterial;
                case BoardGeometryKind.RoadSpawnSurface:
                    return roadSpawnMaterial;
                case BoardGeometryKind.RoadEndSurface:
                    return roadEndMaterial;
                default:
                    return blockerMaterial;
            }
        }

        private static void CreateCameraFocusRegion(
            Transform root,
            LowestBoardLevelBounds focusRegion,
            BoardDefinition board,
            Material cameraFocusMaterial)
        {
            int spanX = focusRegion.MaxXExclusive - focusRegion.MinX;
            int spanZ = focusRegion.MaxZExclusive - focusRegion.MinZ;
            if (spanX <= 0 || spanZ <= 0)
            {
                return;
            }

            float centerX = (focusRegion.MinX + spanX * 0.5f) * board.CellSize;
            float centerZ = (focusRegion.MinZ + spanZ * 0.5f) * board.CellSize;
            float overlayY = focusRegion.Level * board.HeightUnit + CameraFocusOverlayLift;

            GameObject generated = GameObject.CreatePrimitive(PrimitiveType.Quad);
            generated.name = CameraFocusRegionName;
            Undo.RegisterCreatedObjectUndo(generated, "Create Camera Focus Region");

            // This is a pure visual indicator and must carry no collider.
            // Unity's Quad primitive includes a MeshCollider by default; remove it.
            Collider primitiveCollider = generated.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                UnityEngine.Object.DestroyImmediate(primitiveCollider);
            }

            generated.transform.SetParent(root, false);
            generated.transform.localPosition = new Vector3(centerX, overlayY, centerZ);
            generated.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            generated.transform.localScale = new Vector3(
                spanX * board.CellSize,
                spanZ * board.CellSize,
                1f);

            MeshRenderer renderer = generated.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = cameraFocusMaterial;
            renderer.enabled = board.VisualizeInScene;
            renderer.sortingOrder = BoardVisualizationSortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static string GetRectangleName(BoardGeometryRectangle rectangle)
        {
            switch (rectangle.Kind)
            {
                case BoardGeometryKind.PlacementSurface:
                    return PlaceableAreaName;
                case BoardGeometryKind.RoadSurface:
                    return RoadAreaName;
                case BoardGeometryKind.RoadSpawnSurface:
                    return RoadSpawnAreaName;
                case BoardGeometryKind.RoadEndSurface:
                    return RoadEndAreaName;
                default:
                    return BlockedAreaName;
            }
        }

        private static bool ApplyComponentState(Transform root, bool visualize)
        {
            bool changed = false;
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                MeshRenderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (renderer.sortingOrder != BoardVisualizationSortingOrder)
                {
                    Undo.RecordObject(renderer, "Order Board Visualization");
                    renderer.sortingOrder = BoardVisualizationSortingOrder;
                    changed = true;
                }

                if (renderer.enabled != visualize)
                {
                    Undo.RecordObject(renderer, "Update Board Geometry Visibility");
                    renderer.enabled = visualize;
                    changed = true;
                }
            }

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null && !colliders[index].enabled)
                {
                    Undo.RecordObject(colliders[index], "Enable Board Geometry Collider");
                    colliders[index].enabled = true;
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ApplyRendererSortingOrder(
            Transform root,
            int sortingOrder,
            string undoName)
        {
            bool changed = false;
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer.sortingOrder == sortingOrder)
                {
                    continue;
                }

                Undo.RecordObject(renderer, undoName);
                renderer.sortingOrder = sortingOrder;
                changed = true;
            }

            return changed;
        }

        private static string ReadGeneratedSignature(BoardScenePresenter presenter)
        {
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty property = serializedPresenter.FindProperty(
                GeneratedSignaturePropertyName);
            return property != null ? property.stringValue : string.Empty;
        }

        private static string ReadGeneratedGridPlaceableSignature(
            BoardScenePresenter presenter)
        {
            var serializedPresenter =
                new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty property =
                serializedPresenter.FindProperty(
                    GeneratedGridPlaceableSignaturePropertyName);
            return property != null
                ? property.stringValue
                : string.Empty;
        }

        private static bool AssignGeneratedState(
            BoardScenePresenter presenter,
            Transform root,
            string signature)
        {
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty rootProperty = serializedPresenter.FindProperty("generatedRoot");
            SerializedProperty signatureProperty = serializedPresenter.FindProperty(
                GeneratedSignaturePropertyName);
            bool rootChanged = rootProperty != null
                && rootProperty.objectReferenceValue != root;
            bool signatureChanged = signatureProperty != null
                && signatureProperty.stringValue != signature;
            if (!rootChanged && !signatureChanged)
            {
                return false;
            }

            Undo.RecordObject(presenter, "Update Board Visualization State");
            if (rootChanged)
            {
                rootProperty.objectReferenceValue = root;
            }

            if (signatureChanged)
            {
                signatureProperty.stringValue = signature;
            }

            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool AssignGridPlaceableState(
            BoardScenePresenter presenter,
            Transform root,
            string signature)
        {
            var serializedPresenter =
                new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty rootProperty =
                serializedPresenter.FindProperty(
                    GeneratedGridPlaceableRootPropertyName);
            SerializedProperty signatureProperty =
                serializedPresenter.FindProperty(
                    GeneratedGridPlaceableSignaturePropertyName);
            int expectedRootInstanceId = root != null
                ? root.GetInstanceID()
                : 0;
            bool rootChanged = rootProperty != null
                && rootProperty.objectReferenceInstanceIDValue
                    != expectedRootInstanceId;
            bool signatureChanged = signatureProperty != null
                && signatureProperty.stringValue != signature;
            if (!rootChanged && !signatureChanged)
            {
                return false;
            }

            Undo.RecordObject(
                presenter,
                "Update Generated Grid Placeable State");
            if (rootChanged)
            {
                rootProperty.objectReferenceValue = root;
            }

            if (signatureChanged)
            {
                signatureProperty.stringValue = signature;
            }

            serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
