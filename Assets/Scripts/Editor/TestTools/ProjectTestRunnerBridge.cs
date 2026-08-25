#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense3D.Tests.EditMode
{
    /// <summary>
    /// Runs the consolidated project test assemblies through Unity Test Framework.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectTestRunnerBridge
    {
        private const string StatusKey = "TowerDefense3D.Tests.Status";
        private const string ResultKey = "TowerDefense3D.Tests.Result";
        private const string EditModeAssemblyName = "TowerDefense3D.EditModeTests";
        private const string PlayModeAssemblyName = "TowerDefense3D.PlayModeTests";

        private static readonly string[] OwnedTestNamespaces =
        {
            "TowerDefense3D.Core.Tests.",
            "TowerDefense3D.Enemies.Tests.",
            "TowerDefense3D.GameFlow.Tests.",
            "TowerDefense3D.GridPlacement.Tests.",
            "TowerDefense3D.Simulation.Tests.",
            "TowerDefense3D.Towers.Tests.",
            "TowerDefense3D.Waves.Tests."
        };

        static ProjectTestRunnerBridge()
        {
            TestRunnerApi.RegisterTestCallback(new ResultCallbacks(), 100);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run All EditMode")]
        public static void RunEditMode()
        {
            Start(TestMode.EditMode, EditModeAssemblyName, true);
        }

        [MenuItem("Tools/Tower Defense/Tests/Run All PlayMode")]
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

            Start(TestMode.PlayMode, PlayModeAssemblyName, false);
        }

        [MenuItem("Tools/Tower Defense/Tests/Log Last Result")]
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
                Debug.Log("Project tests: " + GetStatus());
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
                for (int index = 0; index < OwnedTestNamespaces.Length; index++)
                {
                    if (fullName.IndexOf(OwnedTestNamespaces[index], StringComparison.Ordinal) >= 0)
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
