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
        private MNNEmbedding(TaskModelOwner<EmbeddingHandle> owner)
        {
            _owner = owner;
        }

        public MNNBackendType Backend => _owner.Backend;
        public static MNNEmbedding Load(string modelDirectory, int threadCount = 4, string cacheDirectory = null, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, MNNBackendType backendType = MNNBackendType.Auto) => new MNNEmbedding(TaskModelOwner<EmbeddingHandle>.Load(modelDirectory, threadCount, cacheDirectory, precisionMode, MNNInterop.MNN_Embedding_create, backendType, "The bundled GPU embedding path fails the retrieval-quality regression, including High precision."));
        public int Dimension => _owner.Use(MNNInterop.MNN_Embedding_getDimension);
        /// <summary>Returns an owned vector; Qwen3 retrieval queries should include the model's Instruct/Query prefix.</summary>
        public float[] Encode(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));
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
        public void Dispose() => _owner.Dispose();
    }
}
