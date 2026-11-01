using System;
using System.Threading.Tasks;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;

namespace MNN.Unity
{
    /// <summary>Qwen3 reranker returning a relevance probability for each query/document pair.</summary>
    public sealed class MNNReranker : IDisposable
    {
        private readonly TaskModelOwner<RerankerHandle> _owner;
        private MNNReranker(TaskModelOwner<RerankerHandle> owner)
        {
            _owner = owner;
        }

        public MNNBackendType Backend => _owner.Backend;
        public static MNNReranker Load(string modelDirectory, int threadCount = 4, string cacheDirectory = null, MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, MNNBackendType backendType = MNNBackendType.Auto) => new MNNReranker(TaskModelOwner<RerankerHandle>.Load(modelDirectory, threadCount, cacheDirectory, precisionMode, MNNInterop.MNN_Reranker_create, backendType));
        public float Score(string query, string document)
        {
            if (string.IsNullOrWhiteSpace(query))
                throw new ArgumentNullException(nameof(query));
            if (string.IsNullOrWhiteSpace(document))
                throw new ArgumentNullException(nameof(document));
            return _owner.Use(handle =>
            {
                if (MNNInterop.MNN_Reranker_score(handle, query, document, out float score) != 0 || float.IsNaN(score) || float.IsInfinity(score) || score < 0 || score > 1)
                    throw new MNNException(MNNErrorCode.NoExecution, "Reranker inference failed.");
                return score;
            });
        }

        public Task<float> ScoreAsync(string query, string document) => Task.Run(() => Score(query, document));
        public void Dispose() => _owner.Dispose();
    }
}
