#if (UNITY_ANDROID || UNITY_IOS || UNITY_STANDALONE_WIN) && !UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(MNN.Unity.Tests.PlayerResults))]

namespace MNN.Unity.Tests
{
    // Keep device-side evidence when Unity's Editor connection cannot receive
    // results (for example, two Editors competing for the default USB port).
    [Preserve]
    public sealed class PlayerResults : ITestRunCallback
    {
        private static string ResultDirectory => Environment.GetEnvironmentVariable("MNN_PLAYER_RESULTS_DIRECTORY") ?? Application.persistentDataPath;
        private static string ResultPath => Path.Combine(ResultDirectory, "mnn-player-results.xml");

        public void RunStarted(ITest testsToRun)
        {
            Directory.CreateDirectory(ResultDirectory);
            if (File.Exists(ResultPath))
                File.Delete(ResultPath);
        }

        public void RunFinished(ITestResult testResults)
        {
            string temporary = ResultPath + ".tmp";
            var xml = testResults.ToXml(true);
            var buildId = Resources.Load<TextAsset>("MNNValidationBuildId");
            if (buildId != null) xml.AddAttribute("mnn-build-id", buildId.text.Trim());
            File.WriteAllText(temporary, xml.OuterXml);
            if (File.Exists(ResultPath))
                File.Delete(ResultPath);
            File.Move(temporary, ResultPath);
            Debug.Log($"MNN Player results: passed={testResults.PassCount}, failed={testResults.FailCount}, skipped={testResults.SkipCount}; {ResultPath}");
#if UNITY_STANDALONE_WIN
            if (Environment.GetEnvironmentVariable("MNN_PLAYER_RESULTS_DIRECTORY") != null)
                Application.Quit(testResults.FailCount == 0 ? 0 : 2);
#endif
        }

        public void TestStarted(ITest test)
        {
#if UNITY_STANDALONE_WIN
            if (!test.IsSuite)
                File.WriteAllText(Path.Combine(ResultDirectory, "current-test.txt"), test.FullName);
#endif
        }
        public void TestFinished(ITestResult result)
        {
#if UNITY_STANDALONE_WIN
            if (result.Test.IsSuite) return;
            string directory = Path.Combine(ResultDirectory, "TestCases");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, result.Test.Id + ".xml"), result.ToXml(true).OuterXml);
#endif
        }
    }
}
#endif
