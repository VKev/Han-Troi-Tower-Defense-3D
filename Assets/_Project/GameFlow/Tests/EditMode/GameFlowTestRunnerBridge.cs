#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// Runs Game Flow tests from a save-safe empty host scene and restores the prior clean scene.
    /// </summary>
    [InitializeOnLoad]
    public static class GameFlowTestRunnerBridge
    {
        private const string TestHostPath =
            "Assets/_Project/GameFlow/Tests/PlayMode/GameFlowPlayModeTestHost.unity";
        private const string StatusKey = "TowerDefense3D.GameFlow.Tests.Status";
        private const string ResultKey = "TowerDefense3D.GameFlow.Tests.Result";
        private const string RestoreSceneKey = "TowerDefense3D.GameFlow.Tests.RestoreScene";
        private const string RestorePendingKey = "TowerDefense3D.GameFlow.Tests.RestorePending";
        private const string OwnedTestNamespace = "TowerDefense3D.GameFlow.Tests.";

        static GameFlowTestRunnerBridge()
        {
            TestRunnerApi.RegisterTestCallback(new ResultCallbacks(), 100);
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.delayCall += TryRestoreScene;
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Game Flow EditMode")]
        public static void RunEditMode()
        {
            Start(TestMode.EditMode, "TowerDefense3D.GameFlow.EditModeTests", true);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Game Flow PlayMode")]
        public static void RunPlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Game Flow PlayMode tests require idle Edit Mode.");
                return;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isDirty)
                {
                    Debug.LogError(
                        "Save or discard dirty scene changes before Game Flow PlayMode tests: "
                        + scene.path);
                    return;
                }
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestHostPath) == null)
            {
                Debug.LogError("Game Flow PlayMode test host is missing at " + TestHostPath);
                return;
            }

            string restorePath = SceneManager.GetActiveScene().path;
            SessionState.SetString(RestoreSceneKey, restorePath);
            SessionState.SetBool(RestorePendingKey, true);
            EditorSceneManager.OpenScene(TestHostPath, OpenSceneMode.Single);
            Start(TestMode.PlayMode, "TowerDefense3D.GameFlow.PlayModeTests", false);
        }

        [MenuItem("Tools/Tower Defense/Tests/Log Last Game Flow Result")]
        public static void LogLastResult()
        {
            Debug.Log(GetStatus());
        }

        public static string GetStatus()
        {
            return SessionState.GetString(StatusKey, "NOT_RUN")
                + " | "
                + SessionState.GetString(ResultKey, "No result recorded.");
        }

        private static void Start(TestMode mode, string assemblyName, bool runSynchronously)
        {
            SessionState.SetString(StatusKey, "STARTING " + mode);
            SessionState.SetString(ResultKey, "Awaiting Test Runner callback.");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var settings = new ExecutionSettings(
                new Filter
                {
                    testMode = mode,
                    assemblyNames = new[] { assemblyName }
                })
            {
                runSynchronously = runSynchronously
            };
            api.Execute(settings);
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += TryRestoreScene;
            }
        }

        private static void TryRestoreScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode
                || !SessionState.GetBool(RestorePendingKey, false))
            {
                return;
            }

            string restorePath = SessionState.GetString(RestoreSceneKey, string.Empty);
            SessionState.SetBool(RestorePendingKey, false);
            SessionState.EraseString(RestoreSceneKey);
            if (!string.IsNullOrWhiteSpace(restorePath)
                && AssetDatabase.LoadAssetAtPath<SceneAsset>(restorePath) != null)
            {
                EditorSceneManager.OpenScene(restorePath, OpenSceneMode.Single);
            }
        }

        [Serializable]
        private sealed class ResultCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
                if (ContainsOwnedTest(testsToRun))
                {
                    SessionState.SetString(StatusKey, "RUNNING " + testsToRun.Name);
                }
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                if (!ContainsOwnedTest(result))
                {
                    return;
                }

                var failures = new List<string>();
                CollectFailures(result, failures);
                var summary = new StringBuilder();
                summary.Append("pass=").Append(result.PassCount)
                    .Append(" fail=").Append(result.FailCount)
                    .Append(" skip=").Append(result.SkipCount)
                    .Append(" inconclusive=").Append(result.InconclusiveCount)
                    .Append(" duration=").Append(result.Duration.ToString("F3")).Append('s');
                foreach (string failure in failures)
                {
                    summary.Append(" | ").Append(failure);
                }

                SessionState.SetString(
                    StatusKey,
                    result.FailCount == 0 ? "PASSED" : "FAILED");
                SessionState.SetString(ResultKey, summary.ToString());
                Debug.Log("Game Flow tests: " + GetStatus());
                EditorApplication.delayCall += TryRestoreScene;
            }

            public void TestStarted(ITestAdaptor test)
            {
            }

            public void TestFinished(ITestResultAdaptor result)
            {
            }

            private static void CollectFailures(
                ITestResultAdaptor result,
                ICollection<string> failures)
            {
                if (!result.HasChildren
                    && result.ResultState.StartsWith("Failed", StringComparison.Ordinal))
                {
                    failures.Add(result.FullName + ": " + result.Message);
                }

                if (!result.HasChildren)
                {
                    return;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    CollectFailures(child, failures);
                }
            }

            private static bool ContainsOwnedTest(ITestAdaptor test)
            {
                if (test.FullName.IndexOf(OwnedTestNamespace, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                foreach (ITestAdaptor child in test.Children)
                {
                    if (ContainsOwnedTest(child))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool ContainsOwnedTest(ITestResultAdaptor result)
            {
                if (result.FullName.IndexOf(OwnedTestNamespace, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }

                foreach (ITestResultAdaptor child in result.Children)
                {
                    if (ContainsOwnedTest(child))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
#endif
