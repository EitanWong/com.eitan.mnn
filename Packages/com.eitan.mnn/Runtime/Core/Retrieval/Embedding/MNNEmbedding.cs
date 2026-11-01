using System;
using System.Threading.Tasks;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    /// <summary>MNN text embedding model. Call Load on the Unity main thread; EncodeAsync runs inference in a worker.</summary>
    public sealed class MNNEmbedding : IDisposable
    {
        private readonly TaskModelOwner<EmbeddingHandle> _owner;
        private readonly MNNBertEmbedding _bert;
        private readonly MNNEmbeddingGraph _graph;
        private MNNEmbedding(TaskModelOwner<EmbeddingHandle> owner, MNNEmbeddingGraph graph)
        {
            _owner = owner;
            _graph = graph;
        }

        private MNNEmbedding(MNNBertEmbedding bert) { _bert = bert; }

        public MNNBackendType Backend => _bert != null ? _bert.Backend : _owner.Backend;
        public static MNNEmbedding Load(string modelDirectory, int threadCount = 4, string cacheDirectory = null, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (MNNBertEmbeddingConfig.IsConfigured(modelDirectory))
                return new MNNEmbedding(MNNBertEmbedding.Load(modelDirectory, threadCount: threadCount, precisionMode: precisionMode, backendType: backendType));
            var graph = new MNNEmbeddingGraph();
            try
            {
                return new MNNEmbedding(TaskModelOwner<EmbeddingHandle>.Load(modelDirectory, threadCount, cacheDirectory, precisionMode, graph.Create, backendType, "The bundled GPU embedding path fails the retrieval-quality regression, including High precision."), graph);
            }
            catch
            {
                graph.Dispose();
                throw;
            }
        }
        public int Dimension => _bert != null ? _bert.Dimension : _owner.Use(MNNInterop.MNN_Embedding_getDimension);
        /// <summary>Returns an owned vector; Qwen3 retrieval queries should include the model's Instruct/Query prefix.</summary>
        public float[] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));
            if (_bert != null)
                return _bert.Encode(text);
            return _owner.Use(handle =>
            {
                int dimension = MNNInterop.MNN_Embedding_getDimension(handle);
                if (dimension <= 0)
                    throw new MNNException(MNNErrorCode.NoExecution, "Invalid embedding dimension.");
                var values = new float[dimension];
                if (MNNInterop.MNN_Embedding_encode(handle, text, values, dimension) != dimension)
                    throw new MNNException(MNNErrorCode.NoExecution, "Embedding inference failed.");
                foreach (float value in values)
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new MNNException(MNNErrorCode.NoExecution, "Embedding returned a non-finite vector.");
                return values;
            });
        }

        public Task<float[]> EncodeAsync(string text) => Task.Run(() => Encode(text));
        public float[] EncodeQuery(string text) => _bert != null ? _bert.EncodeQuery(text) : Encode(text);
        public void Dispose()
        {
            _bert?.Dispose();
            _owner?.Dispose();
            _graph?.Dispose();
        }
    }
}
