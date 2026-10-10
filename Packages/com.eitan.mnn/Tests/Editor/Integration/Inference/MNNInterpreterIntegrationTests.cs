using NUnit.Framework;
using System;
using System.IO;
using System.Collections;
using UnityEngine.TestTools;
using UnityEditor.PackageManager;

namespace MNN.Unity.Tests
{
    public class MNNInterpreterIntegrationTests
    {
        private MNNInterpreter _interpreter;
        private MNNSession _session;
        private static string ModelPath => Path.Combine(PackageInfo.FindForAssembly(typeof(MNNInterpreterIntegrationTests).Assembly).resolvedPath, "Tests", "Fixtures", "affine.mnn");
        [SetUp]
        public void Setup()
        {
            _interpreter = MNNInterpreter.CreateFromFile(ModelPath);
            _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU, ThreadCount = 2});
        }

        [TearDown]
        public void Cleanup()
        {
            _session?.Dispose();
            _interpreter?.Dispose();
        }

        private void AssertInference(float[] values)
        {
            _session.GetInput("input").CopyFromArray(values);
            _session.Run();
            var actual = _session.GetOutput("output").CopyToArray<float>();
            Assert.AreEqual(values.Length, actual.Length);
            for (int i = 0; i < values.Length; i++)
                Assert.That(actual[i], Is.EqualTo(values[i] * 2 + 1).Within(0.00001f));
        }

        [Test]
        public void FileModel_ComputesActualAffineFunction() => AssertInference(new[]{-3f, 0f, 1.5f, 100f});
        [Test]
        public void BufferModel_ComputesActualAffineFunction()
        {
            _session.Dispose();
            _interpreter.Dispose();
            _interpreter = MNNInterpreter.CreateFromBuffer(File.ReadAllBytes(ModelPath));
            GC.Collect();
            _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU});
            AssertInference(new[]{-1f, 1f, 2f, 3f});
        }

        [Test]
        public void RepeatedRuns_UpdateOutput()
        {
            AssertInference(new[]{0f, 1f, 2f, 3f});
            AssertInference(new[]{4f, -1f, 0.25f, 9f});
        }

        [Test]
        public void Resize_InvalidatesOldViews_AndComputesNewShape()
        {
            var oldInput = _session.GetInput("input");
            var oldOutput = _session.GetOutput("output");
            _session.ResizeInput("input", new[]{1, 6});
            Assert.IsTrue(oldInput.IsDisposed);
            Assert.IsTrue(oldOutput.IsDisposed);
            Assert.Throws<ObjectDisposedException>(() => oldInput.CopyToArray<float>());
            CollectionAssert.AreEqual(new[]{1, 6}, _session.GetInput("input").Shape);
            AssertInference(new[]{1f, 2f, 3f, 4f, 5f, 6f});
        }

        [Test]
        public void Resize_UnmapsBorrowedViewBeforeReallocation()
        {
            var oldInput = _session.GetInput();
            oldInput.MapForWrite<float>();
            _session.ResizeInput(null, new[]{1, 8});
            Assert.IsTrue(oldInput.IsDisposed);
            AssertInference(new[]{1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f});
        }

        [Test]
        public void DisposedBorrowedTensor_CanBeRequestedAgain()
        {
            var oldInput = _session.GetInput();
            oldInput.Dispose();
            Assert.AreNotSame(oldInput, _session.GetInput());
            AssertInference(new[]{1f, 2f, 3f, 4f});
        }

        [Test]
        public void ReleaseModel_PreservesExistingSession_RejectsNewSession()
        {
            _interpreter.ReleaseModel();
            _interpreter.ReleaseModel();
            Assert.IsTrue(_interpreter.IsModelReleased);
            AssertInference(new[]{1f, 2f, 3f, 4f});
            Assert.Throws<InvalidOperationException>(() => _interpreter.CreateSession());
        }

        [Test]
        public void DisposeInterpreter_InvalidatesSessionAndTensor()
        {
            var input = _session.GetInput();
            _interpreter.Dispose();
            Assert.IsTrue(_session.IsDisposed);
            Assert.IsTrue(input.IsDisposed);
            Assert.Throws<ObjectDisposedException>(_session.Run);
            Assert.DoesNotThrow(_interpreter.Dispose);
            Assert.DoesNotThrow(_session.Dispose);
        }

        [Test]
        public void DisposeSession_LeavesInterpreterUsable()
        {
            _session.Dispose();
            _session = _interpreter.CreateSession(new MNNSessionConfig{BackendType = MNNBackendType.CPU});
            AssertInference(new[]{1f, 2f, 3f, 4f});
        }

        [Test]
        public void Tensor_ValidatesNullLengthAndType()
        {
            var input = _session.GetInput();
            Assert.Throws<ArgumentNullException>(() => input.CopyFromArray<float>(null));
            Assert.Throws<ArgumentException>(() => input.CopyFromArray(new[]{1f}));
            Assert.Throws<InvalidOperationException>(() => input.CopyFromArray(new[]{1, 2, 3, 4}));
            Assert.Throws<InvalidOperationException>(() => input.CopyToArray<int>());
            Assert.Throws<ArgumentNullException>(() => _session.ResizeInput(null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _session.ResizeInput(null, new[]{-1, 4}));
        }

        [Test]
        public void Tensor_MappingPreventsDoubleMap_AndAllowsReuse()
        {
            var input = _session.GetInput();
            var mapped = input.MapForWrite<float>();
            for (int i = 0; i < mapped.Length; i++)
                mapped[i] = i;
            Assert.Throws<InvalidOperationException>(() => input.CopyToArray<float>());
            input.Unmap();
            input.Unmap();
            CollectionAssert.AreEqual(new[]{0f, 1f, 2f, 3f}, input.CopyToArray<float>());
            Assert.AreEqual(MNNDataType.Float, input.DataType);
            Assert.AreEqual(4, input.ElementCount);
            Assert.AreEqual(16, input.ByteSize);
        }

        [UnityTest]
        public IEnumerator RunAsync_ComputesActualResult()
        {
            _session.GetInput().CopyFromArray(new[]{1f, 2f, 3f, 4f});
            var task = _session.RunAsync();
            while (!task.IsCompleted)
                yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
            CollectionAssert.AreEqual(new[]{3f, 5f, 7f, 9f}, _session.GetOutput().CopyToArray<float>());
        }

        [Test]
        public void InvalidPathsAndBuffers_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => MNNInterpreter.CreateFromFile(null));
            Assert.Throws<ArgumentNullException>(() => MNNInterpreter.CreateFromFile(""));
            Assert.Throws<FileNotFoundException>(() => MNNInterpreter.CreateFromFile(Path.Combine(Path.GetDirectoryName(ModelPath), "missing.mnn")));
            Assert.Throws<ArgumentNullException>(() => MNNInterpreter.CreateFromBuffer(null));
            Assert.Throws<ArgumentNullException>(() => MNNInterpreter.CreateFromBuffer(new byte[0]));
        }
    }
}
