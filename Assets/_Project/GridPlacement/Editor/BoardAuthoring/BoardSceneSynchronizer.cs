using TowerDefense3D.GridPlacement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal static class BoardSceneSynchronizer
    {
        internal const string GeneratedRootName = "__Generated Board Geometry";
        private const string SignaturePrefix = "__Signature_";
        private const string GroundMaterialPath =
            "Assets/_Project/GridPlacement/Materials/Ground.mat";
        private const string BlockerMaterialPath =
            "Assets/_Project/GridPlacement/Materials/Blocker.mat";
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
            Transform root = FindOwnedRoot(presenter.transform);
            if (root != null && HasMatchingGeometry(root, plan))
            {
                bool changed = ApplyComponentState(root, board.VisualizeInScene);
                changed |= AssignGeneratedRoot(presenter, root);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
                }

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

            var marker = new GameObject(SignaturePrefix + plan.Signature);
            Undo.RegisterCreatedObjectUndo(marker, "Create Board Geometry Signature");
            marker.transform.SetParent(root, false);
            ApplyComponentState(root, board.VisualizeInScene);
            AssignGeneratedRoot(presenter, root);
            EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
        }

        private static bool HasMatchingGeometry(
            Transform root,
            BoardGeometryPlan plan)
        {
            if (root.childCount != plan.Rectangles.Count + 1)
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

            Transform marker = root.GetChild(plan.Rectangles.Count);
            return marker.name == SignaturePrefix + plan.Signature
                && marker.GetComponents<Component>().Length == 1;
        }

        private static Transform FindOwnedRoot(Transform presenterTransform)
        {
            for (int index = 0; index < presenterTransform.childCount; index++)
            {
                Transform child = presenterTransform.GetChild(index);
                if (child.name == GeneratedRootName)
                {
                    return child;
                }
            }

            return null;
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
            string kind = rectangle.Kind == BoardGeometryKind.PlacementSurface
                ? "Surface"
                : "Blocker";
            return $"{kind} Y{rectangle.Y} X{rectangle.X} Z{rectangle.Z} "
                + $"{rectangle.Width}x{rectangle.Depth}";
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

        private static bool AssignGeneratedRoot(
            BoardScenePresenter presenter,
            Transform root)
        {
            if (presenter.GeneratedRoot == root)
            {
                return false;
            }

            var serializedPresenter = new SerializedObject(presenter);
            serializedPresenter.Update();
            SerializedProperty property = serializedPresenter.FindProperty("generatedRoot");
            if (property != null)
            {
                Undo.RecordObject(presenter, "Assign Generated Board Root");
                property.objectReferenceValue = root;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }

            return false;
        }
    }
}
