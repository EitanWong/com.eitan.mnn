using System;
using System.Collections;
using System.IO;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class MNNModelProvisionTests
    {
        [UnityTest, Explicit("Downloads the repositories explicitly listed in MNN_TEST_PROVISION_MODELS.")]
        public IEnumerator DownloadSelectedRepositories()
        {
            var selection = Environment.GetEnvironmentVariable("MNN_TEST_PROVISION_MODELS");
            var root = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            var artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.IsFalse(string.IsNullOrWhiteSpace(selection), "Set MNN_TEST_PROVISION_MODELS (semicolon-separated repository names).");
            Assert.IsFalse(string.IsNullOrWhiteSpace(root), "Set MNN_TEST_MODEL_ROOT to the intended local model directory.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(artifacts), "Set MNN_TEST_ARTIFACT_ROOT to a project-local directory.");
            Directory.CreateDirectory(artifacts);
            using (var cancellation = new CancellationTokenSource())
            using (var service = new MNNModelDownloadService(options: new MNNModelDownloadOptions{MaxConnections = 8, RetryCount = 4, PreservePartialDownloads = true}))
            {
                try
                {
                    foreach (var name in selection.Split(';'))
                    {
                        Assert.AreEqual(name, Path.GetFileName(name), "Use an MNN repository basename.");
                        var progressPath = Path.Combine(artifacts, "download-progress.txt");
                        long lastUpdate = 0;
                        var task = service.DownloadRepositoryAsync("MNN/" + name, Path.Combine(root, name), (received, total, file) =>
                        {
                            var now = DateTime.UtcNow.Ticks;
                            if (now - lastUpdate < TimeSpan.TicksPerSecond && received != total)
                                return;
                            lastUpdate = now;
                            File.WriteAllText(progressPath, name + "\n" + received + " / " + total + "\n" + file);
                        }, cancellation.Token);
                        while (!task.IsCompleted)
                            yield return null;
                        Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                        Assert.IsFalse(task.IsCanceled);
                        TestContext.WriteLine(name + ": downloaded and verified " + task.Result + " bytes");
                        File.AppendAllText(Path.Combine(artifacts, "downloads-completed.txt"), name + ": " + task.Result + " bytes\n");
                    }
                }
                finally
                {
                    cancellation.Cancel();
                }
            }
        }
    }
}
