using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("NativeInference")]
    public sealed class ExpressPlayerTests
    {
        [UnityTest]
        public IEnumerator MemoryGraph_ReusesInputsAndKeepsExecutorOwnershipIndependent()
        {
            Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
            var task = Task.Run(() =>
            {
                var graph = new MNNExpressGraph(null, 1, MNNBackendType.CPU, AffineFixture.Bytes);
                try
                {
                    var first = Run(graph, new[] {-3f, 0f, 1.5f, 100f});
                    CollectionAssert.AreEqual(new[] {-5f, 1f, 4f, 201f}, first);
                    using (var independent = new MNNExpressGraph(null, 2, MNNBackendType.CPU, AffineFixture.Bytes))
                    {
                        CollectionAssert.AreEqual(new[] {3f, 5f, 7f, 9f}, Run(independent, new[] {1f, 2f, 3f, 4f}));
                        CollectionAssert.AreEqual(new[] {-1f, 1f, 2f, 11f}, Run(graph, new[] {-1f, 0f, 0.5f, 5f}));
                        CollectionAssert.AreEqual(new[] {5f, 7f, 9f, 11f}, Run(independent, new[] {2f, 3f, 4f, 5f}));
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    CollectionAssert.AreEqual(new[] {3f, 5f, 7f, 9f}, Run(graph, new[] {1f, 2f, 3f, 4f}));
                    CollectionAssert.AreEqual(new[] {-5f, 1f, 4f, 201f}, first);
                    var resized = graph.RunOutputs(new[] { "output" }, new[] {
                        new MNNGenerationGraph.Input("input", new[] { -1f, 2f, 3.25f, 4f, 0f, 100f }, 1, 6)
                    })[0];
                    CollectionAssert.AreEqual(new[] { 1, 6 }, resized.Shape);
                    CollectionAssert.AreEqual(new[] { -1f, 5f, 7.5f, 9f, 1f, 201f }, resized.Values);
                    Assert.Throws<InvalidDataException>(() => graph.Run("missing", Array.Empty<MNNGenerationGraph.Input>()));
                }
                finally
                {
                    graph.Dispose();
                    graph.Dispose();
                }

                Assert.Throws<ObjectDisposedException>(() => graph.Run("output", Array.Empty<MNNGenerationGraph.Input>()));
            });
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        [UnityTest]
        public IEnumerator Module_ConsumesOwnedConstantAndReturnsIndependentOutput()
        {
            Assert.IsTrue(MNNPlatformSupport.IsSupported, MNNPlatformSupport.UnsupportedReason);
            var task = Task.Run(() =>
            {
                using (MNNInterop.BeginNativeCall())
                using (var executor = new MNNInterop.ExpressExecutor(1, MNNBackendType.CPU))
                using (executor.Enter())
                {
                    var values = new[] { -1f, 0f, .5f, 5f };
                    var pin = GCHandle.Alloc(values, GCHandleType.Pinned);
                    var input = default(MNNInterop.CppShared);
                    try
                    {
                        input = MNNInterop.Constant(pin.AddrOfPinnedObject(), new[] { 1, 4 }, 2,
                            new MNNInterop.HalideType { Code = 2, Bits = 32, Lanes = 1 });
                        CollectionAssert.AreEqual(values, MNNInterop.ReadFloat(input, out var shape));
                        CollectionAssert.AreEqual(new[] { 1, 4 }, shape);
                        GC.Collect(); GC.WaitForPendingFinalizers();
                        var result = MNNInterop.RunSingleInputModule(AffineFixture.Bytes, "input", "output", input);
                        CollectionAssert.AreEqual(new[] { -1f, 1f, 2f, 11f }, result);
                        MNNInterop.DestroyVariable(ref input);
                        input = default;
                        GC.Collect(); GC.WaitForPendingFinalizers();
                        CollectionAssert.AreEqual(new[] { -1f, 1f, 2f, 11f }, result);
                    }
                    finally
                    {
                        if (input.Object != IntPtr.Zero) MNNInterop.DestroyVariable(ref input);
                        pin.Free();
                    }
                }
            });
            while (!task.IsCompleted) yield return null;
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString());
        }

        private static float[] Run(MNNExpressGraph graph, float[] values)
        {
            var result = graph.RunOutputs(new[] {"output"}, new[] {new MNNGenerationGraph.Input("input", values, 1, 4)})[0];
            CollectionAssert.AreEqual(new[] {1, 4}, result.Shape);
            return result.Values;
        }
    }
}
