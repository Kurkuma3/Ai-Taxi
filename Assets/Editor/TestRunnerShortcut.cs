using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Taxi.EditorTools
{
    /// <summary>
    /// Menu: Taxi > Run PlayMode Tests. Runs the PlayMode suite and prints one console line per test plus a
    /// summary. The same lines go to Library/TaxiTests.log, because the console is cleared between play-mode
    /// runs. Callbacks are registered on load because the test run reloads the domain.
    /// </summary>
    [InitializeOnLoad]
    public static class TestRunnerShortcut
    {
        public const string LogPath = "Library/TaxiTests.log";

        class Logger : ICallbacks
        {
            static void Line(string s) { System.IO.File.AppendAllText(LogPath, System.DateTime.Now.ToString("HH:mm:ss ") + s + "\n"); }

            public void RunStarted(ITestAdaptor tests)
            {
                System.IO.File.WriteAllText(LogPath, "");
                Line("run started");
                Debug.Log("[TaxiTests] run started");
            }

            public void RunFinished(ITestResultAdaptor r)
            {
                string s = $"finished: {r.TestStatus}, passed={r.PassCount}, failed={r.FailCount}, skipped={r.SkipCount}, {r.Duration:F1}s";
                Line(s);
                Debug.Log("[TaxiTests] " + s);
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                string line = $"{r.Name}: {r.TestStatus} ({r.Duration:F1}s)";
                Line(line + (r.TestStatus == TestStatus.Failed ? " | " + r.Message : ""));
                if (r.TestStatus == TestStatus.Failed) Debug.LogError("[TaxiTests] " + line + "\n" + r.Message + "\n" + r.StackTrace);
                else Debug.Log("[TaxiTests] " + line);
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
