using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Runs every play mode test from the command line and exits with 0 (all passed) or 1.
    /// CI calls it through GameCI's unity-builder (buildMethod + manualExit), whose license
    /// activation works with a personal license. Results: artifacts/playmode-results.xml.
    /// Entering play mode reloads scripts, so the callbacks are re-registered after each reload.
    /// </summary>
    [InitializeOnLoad]
    public static class CiTests
    {
        const string RunningKey = "RacingGame.CiTests.Running";
        const string ResultsPath = "artifacts/playmode-results.xml";

        static CiTests()
        {
            if (SessionState.GetBool(RunningKey, false)) Register();
        }

        public static void RunPlayModeTests()
        {
            UrpSetup.Ensure(true);
            SessionState.SetBool(RunningKey, true);
            var api = Register();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
        }

        static TestRunnerApi Register()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            return api;
        }

        class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) => Debug.Log($"CI TESTS: running {tests.TestCaseCount} play mode tests");

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (result.HasChildren) return;
                if (result.TestStatus == TestStatus.Failed)
                    Debug.Log($"CI TEST FAILED: {result.Test.FullName}\n{result.Message}\n{result.StackTrace}");
                else
                    Debug.Log($"CI TEST {result.TestStatus.ToString().ToUpperInvariant()}: {result.Test.FullName} ({result.Duration:0.0}s)");
            }

            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ResultsPath)));
                TestRunnerApi.SaveResultToFile(result, ResultsPath);
                Debug.Log($"CI TESTS DONE: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
                SessionState.SetBool(RunningKey, false);
                if (Application.isBatchMode) EditorApplication.Exit(result.FailCount > 0 ? 1 : 0);
            }
        }
    }
}
