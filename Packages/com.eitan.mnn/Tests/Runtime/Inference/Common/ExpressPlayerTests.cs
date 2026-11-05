using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
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

        private static float[] Run(MNNExpressGraph graph, float[] values)
        {
            var result = graph.RunOutputs(new[] {"output"}, new[] {new MNNGenerationGraph.Input("input", values, 1, 4)})[0];
            CollectionAssert.AreEqual(new[] {1, 4}, result.Shape);
            return result.Values;
        }
    }
}
