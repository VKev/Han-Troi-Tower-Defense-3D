using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal static class BoardSceneSynchronizer
    {
        internal const string GeneratedRootName = "Board Visualization";
        internal const string PlaceableAreaName = "Placeable Area";
        internal const string BlockedAreaName = "Blocked Area";
        private const string LegacyGeneratedRootName = "__Generated Board Geometry";
        private const string GeneratedSignaturePropertyName = "generatedSignature";
        private const string GroundMaterialPath =
            "Assets/_Project/GridPlacement/Materials/Ground.mat";
        private const string BlockerMaterialPath =
            "Assets/_Project/GridPlacement/Materials/Blocker.mat";
        private const string InvalidCameraFramingWarning = "Board camera framing skipped because the synchronized Board has no valid playable footprint or Camera setup.";
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
            Transform root = FindOwnedRoot(presenter);
            bool rootRenamed = RenameOwnedRoot(root);
            if (root != null && HasMatchingGeometry(presenter, root, plan))
            {
                bool changed = rootRenamed;
                changed |= ApplyComponentState(root, board.VisualizeInScene);
                changed |= AssignGeneratedState(presenter, root, plan.Signature);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                }

                SynchronizeCameraFramers(presenter);

                return;
            }

            if (root == null)
            {
                var rootObject = new GameObject(GeneratedRootName);
                Undo.RegisterCreatedObjectUndo(rootObject, "Create Board Geometry Root");
                root = rootObject.transform;
                root.SetParent(presenter.transform, false);
            }

            for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
            {
                Undo.DestroyObjectImmediate(root.GetChild(childIndex).gameObject);
            }

            Material groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            Material blockerMaterial = AssetDatabase.LoadAssetAtPath<Material>(BlockerMaterialPath);

            for (int index = 0; index < plan.Rectangles.Count; index++)
            {
                CreateRectangle(
                    root,
                    plan.Rectangles[index],
                    board,
                    groundMaterial,
                    blockerMaterial);
            }

            ApplyComponentState(root, board.VisualizeInScene);
            AssignGeneratedState(presenter, root, plan.Signature);
            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            SynchronizeCameraFramers(presenter);
        }

        private static void SynchronizeCameraFramers(
            BoardScenePresenter presenter)
        {
            BoardCameraFramer[] framers =
                Resources.FindObjectsOfTypeAll<BoardCameraFramer>();
            for (int index = 0; index < framers.Length; index++)
            {
                BoardCameraFramer framer = framers[index];
                if (framer == null || framer.BoardPresenter != presenter
                    || EditorUtility.IsPersistent(framer)
                    || !framer.gameObject.scene.IsValid()
                    || !framer.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (!framer.TryCalculatePosition(out Vector3 position))
                {
                    Debug.LogWarning(
                        InvalidCameraFramingWarning,
                        framer);
                    continue;
                }

                Transform cameraTransform = framer.TargetCamera.transform;
                if ((cameraTransform.position - position).sqrMagnitude
                    <= 0.000001f)
                {
                    continue;
                }

                Undo.RecordObject(cameraTransform, "Frame Board Camera");
                cameraTransform.position = position;
                EditorSceneManager.MarkSceneDirty(framer.gameObject.scene);
            }
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

            if (root.childCount != plan.Rectangles.Count)
            {
                return false;
            }

            for (int index = 0; index < plan.Rectangles.Count; index++)
            {
                Transform child = root.GetChild(index);
                if (child.name != GetRectangleName(plan.Rectangles[index])
                    || child.GetComponent<MeshRenderer>() == null
                    || child.GetComponent<BoxCollider>() == null)
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
            Material blockerMaterial)
        {
            bool isSurface = rectangle.Kind == BoardGeometryKind.PlacementSurface;
            float maximumSurfaceThickness =
                Mathf.Min(board.CellSize, board.HeightUnit) * 0.1f;
            float surfaceThickness = Mathf.Max(
                0.001f,
                Mathf.Min(SurfaceThickness, maximumSurfaceThickness));
            float height = isSurface ? surfaceThickness : board.HeightUnit;
            float top = rectangle.Y * board.HeightUnit;
            float centerY = isSurface
                ? top - height * 0.5f
                : top + height * 0.5f;

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
            renderer.sharedMaterial = isSurface ? groundMaterial : blockerMaterial;
            renderer.enabled = board.VisualizeInScene;

            BoxCollider collider = generated.GetComponent<BoxCollider>();
            collider.enabled = true;
        }

        private static string GetRectangleName(BoardGeometryRectangle rectangle)
        {
            return rectangle.Kind == BoardGeometryKind.PlacementSurface
                ? PlaceableAreaName
                : BlockedAreaName;
        }

        private static bool ApplyComponentState(Transform root, bool visualize)
        {
            bool changed = false;
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null && renderers[index].enabled != visualize)
                {
                    Undo.RecordObject(renderers[index], "Update Board Geometry Visibility");
                    renderers[index].enabled = visualize;
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

        private static string ReadGeneratedSignature(BoardScenePresenter presenter)
        {
            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty property = serializedPresenter.FindProperty(
                GeneratedSignaturePropertyName);
            return property != null ? property.stringValue : string.Empty;
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
    }
}
