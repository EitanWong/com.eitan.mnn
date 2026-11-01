using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MNNEmbeddingGraphTests
    {
        [Test]
        public void LegacyCausalGraph_EnablesCacheInOwnedCopyAndIsIdempotent()
        {
            byte[] source = Graph(false), original = (byte[])source.Clone();
            byte[] prepared = MNNEmbeddingGraph.Prepare(source);
            CollectionAssert.AreEqual(original, source);
            Assert.AreEqual(1, CacheFlag(prepared));
            Assert.AreSame(prepared, MNNEmbeddingGraph.Prepare(prepared));
        }

        [Test]
        public void CurrentGraph_IsPreserved()
        {
            byte[] source = Graph(true);
            Assert.AreSame(source, MNNEmbeddingGraph.Prepare(source));
            Assert.AreEqual(1, CacheFlag(source));
        }

        [Test]
        public void SubgraphAttention_IsAdapted()
        {
            byte[] source = Graph(false, subgraph: true);
            byte[] prepared = MNNEmbeddingGraph.Prepare(source);
            Assert.AreNotSame(source, prepared);
            Assert.AreSame(prepared, MNNEmbeddingGraph.Prepare(prepared));
        }

        [Test]
        public void OtherOperation_IsPreserved()
        {
            byte[] source = Graph(false, type: 0);
            Assert.AreSame(source, MNNEmbeddingGraph.Prepare(source));
        }

        [Test]
        public void TruncatedGraph_IsRejected()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => MNNEmbeddingGraph.Prepare(new byte[3]));
            Assert.Throws<ArgumentNullException>(() => MNNEmbeddingGraph.Prepare(null));
        }

        private static byte[] Graph(bool cache, int type = 299, bool subgraph = false)
        {
            var b = new MNNSerializedGraph.Prefix();
            int net = b.Table(12, subgraph ? 10 : 3); b.Pointer(0, net);
            int nodesField = net + 16;
            if (subgraph)
            {
                int graphs = b.Vector(new int[1]); b.Pointer(net + 44, graphs);
                int graph = b.Table(6, 4); b.Pointer(graphs + 4, graph);
                nodesField = graph + 20;
            }
            int ops = b.Vector(new int[1]); b.Pointer(nodesField, ops);
            int op = b.Op("attention", type, 98, new[] { 0, 1, 2, 3 }, new[] { 4 });
            b.Pointer(ops + 4, op);
            int parameter = b.Table(1, 0); b.Pointer(op + 12, parameter);
            b.Int(parameter + 4, cache ? 1 : 0);
            return b.Finish(Array.Empty<byte>());
        }

        private static byte CacheFlag(byte[] graph)
        {
            var r = new MNNSerializedGraph.Reader(graph);
            int ops = r.Follow(r.Field(r.Int(0), 3));
            int op = r.Follow(ops + 4), parameter = r.Follow(r.Field(op, 2));
            return graph[r.Field(parameter, 0)];
        }
    }
}
