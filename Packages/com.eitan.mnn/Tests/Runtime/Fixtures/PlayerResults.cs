#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
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
        private static string ResultPath => Path.Combine(Application.persistentDataPath, "mnn-player-results.xml");

        public void RunStarted(ITest testsToRun)
        {
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
            Debug.Log($"MNN mobile results: passed={testResults.PassCount}, failed={testResults.FailCount}, skipped={testResults.SkipCount}; {ResultPath}");
        }

        public void TestStarted(ITest test) { }
        public void TestFinished(ITestResult result) { }
    }
}
#endif
