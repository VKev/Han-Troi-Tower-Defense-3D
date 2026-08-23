using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Editor
{
    internal static class BoardCameraAuthoringSynchronizer
    {
        private const string InvalidCameraFramingWarning =
            "Board camera framing skipped because the synchronized Board has "
            + "no valid playable footprint or Camera setup.";

        internal static void Synchronize(BoardView boardView)
        {
            BoardCameraView[] cameraViews =
                Resources.FindObjectsOfTypeAll<BoardCameraView>();
            for (int index = 0; index < cameraViews.Length; index++)
            {
                BoardCameraView cameraView = cameraViews[index];
                if (cameraView.BoardView != boardView
                    || EditorUtility.IsPersistent(cameraView)
                    || !cameraView.gameObject.scene.IsValid()
                    || !cameraView.gameObject.scene.isLoaded)
                {
                    continue;
                }

                if (cameraView.TargetCamera == null || boardView.Board == null)
                {
                    Debug.LogWarning(InvalidCameraFramingWarning, cameraView);
                    continue;
                }

                var cameraSystem = new BoardCameraSystem(cameraView);
                if (!cameraSystem.TryCalculatePose(
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    Debug.LogWarning(
                        InvalidCameraFramingWarning,
                        cameraView);
                    continue;
                }

                Transform cameraTransform = cameraView.TargetCamera.transform;
                if ((cameraTransform.position - position).sqrMagnitude
                        <= 0.000001f
                    && Quaternion.Angle(cameraTransform.rotation, rotation)
                        <= 0.0001f)
                {
                    continue;
                }

                Undo.RecordObject(cameraTransform, "Frame Board Camera");
                cameraTransform.SetPositionAndRotation(position, rotation);
                EditorSceneManager.MarkSceneDirty(cameraView.gameObject.scene);
            }
        }
    }
}
