#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    /// <summary>
    /// Runs focused Game Flow tests while Unity Test Framework owns scene setup and restoration.
    /// </summary>
    [InitializeOnLoad]
    public static class GameFlowTestRunnerBridge
    {
        private const string StatusKey = "TowerDefense3D.GameFlow.Tests.Status";
        private const string ResultKey = "TowerDefense3D.GameFlow.Tests.Result";
        private const string OwnedTestNamespace = "TowerDefense3D.GameFlow.Tests.";
        private const string TowerTestNamespace = "TowerDefense3D.Towers.Tests.";

        static GameFlowTestRunnerBridge()
        {
            TestRunnerApi.RegisterTestCallback(new ResultCallbacks(), 100);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Game Flow EditMode")]
        public static void RunEditMode()
        {
            Start(TestMode.EditMode, "TowerDefense3D.GameFlow.EditModeTests", true);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Tower Network EditMode")]
        public static void RunTowerNetworkEditMode()
        {
            Start(TestMode.EditMode, "TowerDefense3D.GridPlacement.EditModeTests", true);
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
                if (IsOwnedTestName(test.FullName))
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
                if (IsOwnedTestName(result.FullName))
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

            private static bool IsOwnedTestName(string fullName)
            {
                return fullName.IndexOf(OwnedTestNamespace, StringComparison.Ordinal) >= 0
                    || fullName.IndexOf(TowerTestNamespace, StringComparison.Ordinal) >= 0;
            }
        }
    }
}
#endif
