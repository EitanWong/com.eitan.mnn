using System;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace MNN.Unity.Tests
{
    public sealed class SessionHintIntegrationTests
    {
        private static string Affine => Path.Combine(
            PackageInfo.FindForAssembly(typeof(MNNInterpreter).Assembly).resolvedPath,
            "Tests/Fixtures/affine.mnn");

        [TestCase(0), TestCase(3)]
        public void OfficialConvolutionHintBeforeSessionPreservesInference(int level)
        {
            using (var interpreter = MNNInterpreter.CreateFromFile(Affine))
            {
                interpreter.SetWinogradMemoryLevel(level);
                using (var session = interpreter.CreateSession(new MNNSessionConfig { BackendType = MNNBackendType.CPU }))
                {
                    session.GetInput().CopyFromArray(new[] { -1f, 0f, 1f, 2f });
                    session.Run();
                    CollectionAssert.AreEqual(new[] { -1f, 1f, 3f, 5f }, session.GetOutput().CopyToArray<float>());
                }
            }
        }

        [Test]
        public void HintRejectsInvalidLevelAndSessionAlreadyCreated()
        {
            using (var interpreter = MNNInterpreter.CreateFromFile(Affine))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => interpreter.SetWinogradMemoryLevel(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => interpreter.SetWinogradMemoryLevel(4));
                using (var session = interpreter.CreateSession(new MNNSessionConfig { BackendType = MNNBackendType.CPU }))
                    Assert.Throws<InvalidOperationException>(() => interpreter.SetWinogradMemoryLevel(0));
            }
        }

        [Test]
        public void DisposedInterpreterRejectsHint()
        {
            var interpreter = MNNInterpreter.CreateFromFile(Affine);
            interpreter.Dispose();
            Assert.Throws<ObjectDisposedException>(() => interpreter.SetWinogradMemoryLevel(0));
        }

        [Test]
        public void GraphHintRequiresExplicitCpuInterpreter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNGenerationGraph(
                Affine, 1, backendType: MNNBackendType.Metal, cpuWinogradMemoryLevel: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNGenerationGraph(
                Affine, 1, express: true, backendType: MNNBackendType.CPU, cpuWinogradMemoryLevel: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNGenerationGraph(
                Affine, 1, backendType: MNNBackendType.CPU, cpuWinogradMemoryLevel: 4));
        }
    }
}
