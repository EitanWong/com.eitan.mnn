using System;
using System.Collections;
using System.Text;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    // These tests deliberately FAIL on unsupported targets. Passing only the
    // PlatformContract category must never certify a platform for inference.
    [Category("NativeInference")]
    public class PlayerInferenceTests
    {
        [SetUp]
        public void RequireImplementedAbi() => Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
        [Test]
        public void BundledNativeLibrary_LoadsExpectedVersion()
        {
            Assert.AreEqual("3.6.1", MNNVersion.GetVersion());
            Assert.IsTrue(MNNVersion.IsLoaded());
        }

        [TestCase(""), TestCase("1234567890123456789012"), TestCase("12345678901234567890123"), TestCase("你好，世界🙂一二三四五六七八九十"), TestCase("before\0after")]
        public void NativeString_RoundTripsOwnedUtf8OnPlayerArchitecture(string value)
        {
            MNNInterop.RequireAbi();
            byte[] actual;
            using (var native = new MNNInterop.NativeString(value))
                actual = MNNInterop.StringBytes(ref native.Value);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(value), actual);
        }

        [TestCase(1), TestCase(4)]
        public void BufferModel_ComputesAndResizesAfterGc(int threads)
        {
            using (var model = MNNInterpreter.CreateFromBuffer(AffineFixture.Bytes))
            {
                GC.Collect();
                using (var session = model.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU, ThreadCount = threads}))
                {
                    Check(session, new[]{-3f, 0f, 1.5f, 100f});
                    var oldView = session.GetInput();
                    session.ResizeInput(null, new[]{1, 6});
                    Assert.IsTrue(oldView.IsDisposed);
                    Assert.Throws<ObjectDisposedException>(() => oldView.CopyToArray<float>());
                    Check(session, new[]{-1f, 2f, 3.25f, 4f, 0f, 100f});
                    session.Dispose();
                    Assert.Throws<ObjectDisposedException>(() => session.Run());
                }
            }
        }

        [UnityTest]
        public IEnumerator AsyncSession_ReturnsActualNumericResult()
        {
            using (var model = MNNInterpreter.CreateFromBuffer(AffineFixture.Bytes))
            using (var session = model.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU}))
            {
                session.GetInput().CopyFromArray(new[]{-1f, 0f, 0.5f, 5f});
                var task = session.RunAsync();
                while (!task.IsCompleted)
                    yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                CollectionAssert.AreEqual(new[]{-1f, 1f, 2f, 11f}, session.GetOutput().CopyToArray<float>());
            }
        }

        [UnityTest]
        public IEnumerator AutoSession_UsesPreferredDeviceAndRunsOnWorker()
        {
            using (var model = MNNInterpreter.CreateFromBuffer(AffineFixture.Bytes))
            using (var session = model.CreateSession())
            {
                Assert.AreEqual(MNNBackendType.Auto, session.RequestedBackend);
                Assert.AreEqual(MNNAcceleration.PreferredBackend, session.ActualBackend);
                TestContext.WriteLine("Actual Player backend=" + session.ActualBackend);
                session.GetInput().CopyFromArray(new[]{-1f, 0f, 0.5f, 5f});
                var task = session.RunAsync();
                while (!task.IsCompleted)
                    yield return null;
                Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
                CollectionAssert.AreEqual(new[]{-1f, 1f, 2f, 11f}, session.GetOutput().CopyToArray<float>());
            }
        }

        private static void Check(MNNSession session, float[] input)
        {
            session.GetInput().CopyFromArray(input);
            session.Run();
            var actual = session.GetOutput().CopyToArray<float>();
            Assert.AreEqual(input.Length, actual.Length);
            for (int i = 0; i < input.Length; i++)
                Assert.That(actual[i], Is.EqualTo(input[i] * 2f + 1f).Within(0.00001f));
        }
    }
}
