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

        internal static void Synchronize(BoardScenePresenter presenter)
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

                if (!framer.TryCalculatePose(
                        out Vector3 position,
                        out Quaternion rotation))
                {
                    Debug.LogWarning(
                        InvalidCameraFramingWarning,
                        framer);
                    continue;
                }

                Transform cameraTransform = framer.TargetCamera.transform;
                if ((cameraTransform.position - position).sqrMagnitude
                        <= 0.000001f
                    && Quaternion.Angle(cameraTransform.rotation, rotation)
                        <= 0.0001f)
                {
                    continue;
                }

                Undo.RecordObject(cameraTransform, "Frame Board Camera");
                cameraTransform.SetPositionAndRotation(position, rotation);
                EditorSceneManager.MarkSceneDirty(framer.gameObject.scene);
            }
        }
    }
}
