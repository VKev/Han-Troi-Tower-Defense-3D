#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    /// <summary>
    /// Persists focused Test Runner evidence across PlayMode domain reloads.
    /// </summary>
    [InitializeOnLoad]
    public static class GridPlacementTestRunnerBridge
    {
        private const string StatusKey = "TowerDefense3D.GridPlacement.Tests.Status";
        private const string ResultKey = "TowerDefense3D.GridPlacement.Tests.Result";

        static GridPlacementTestRunnerBridge()
        {
            TestRunnerApi.RegisterTestCallback(new ResultCallbacks(), 100);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Grid Placement EditMode")]
        public static void RunEditMode()
        {
            Start(
                TestMode.EditMode,
                "TowerDefense3D.GridPlacement.EditModeTests",
                runSynchronously: true);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run Grid Placement PlayMode")]
        public static void RunPlayMode()
        {
            Start(
                TestMode.PlayMode,
                "TowerDefense3D.GridPlacement.PlayModeTests",
                runSynchronously: false);
        }

        [MenuItem("Tools/Tower Defense/Tests/Log Last Grid Placement Result")]
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
            var filter = new Filter
            {
                testMode = mode,
                assemblyNames = new[] { assemblyName }
            };
            var settings = new ExecutionSettings(filter)
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
                SessionState.SetString(StatusKey, "RUNNING " + testsToRun.Name);
            }

            public void RunFinished(ITestResultAdaptor result)
            {
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
                Debug.Log("Grid Placement tests: " + GetStatus());
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
        }
    }
}
#endif
