using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Menu: Taxi > Run PlayMode Tests. Runs the PlayMode suite and prints one console line per test
    /// plus a summary, so the result can be read from the console (also through Unity MCP).
    /// Callbacks are registered on load because the test run reloads the domain.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerShortcut
    {
        class Logger : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { Debug.Log("[TaxiTests] run started"); }
            public void RunFinished(ITestResultAdaptor r)
            {
                Debug.Log($"[TaxiTests] finished: {r.TestStatus}, passed={r.PassCount}, failed={r.FailCount}, skipped={r.SkipCount}, {r.Duration:F1}s");
            }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                string line = $"[TaxiTests] {r.Name}: {r.TestStatus} ({r.Duration:F1}s)";
                if (r.TestStatus == TestStatus.Failed) Debug.LogError(line + "\n" + r.Message + "\n" + r.StackTrace);
                else Debug.Log(line);
            }
        }

        static TestRunnerShortcut()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Logger());
        }

        [MenuItem("Taxi/Run PlayMode Tests")]
        public static void RunPlayMode()
        {
            ScriptableObject.CreateInstance<TestRunnerApi>()
                .Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode }));
        }
    }
}
