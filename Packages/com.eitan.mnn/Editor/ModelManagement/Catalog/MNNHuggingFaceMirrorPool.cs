using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Editor
{
    /// <summary>Compatibility adapter sharing runtime source discovery and cooldown state.</summary>
    public sealed class MNNHuggingFaceMirrorPool
    {
        internal MNNHuggingFaceSourcePool RuntimePool { get; }
        public static readonly string[] DefaultDiscoveryUrls = MNNHuggingFaceSourcePool.DefaultDiscoveryUrls;
        public static MNNHuggingFaceMirrorPool Shared { get; } = new MNNHuggingFaceMirrorPool(MNNHuggingFaceSourcePool.Shared);

        public MNNHuggingFaceMirrorPool(IEnumerable<MNNHuggingFaceMirror> mirrors = null, TimeSpan? failureCooldown = null)
            : this(new MNNHuggingFaceSourcePool(mirrors?.Select(mirror => mirror?.RuntimeSource), failureCooldown)) { }

        private MNNHuggingFaceMirrorPool(MNNHuggingFaceSourcePool pool) { RuntimePool = pool; }

        public IReadOnlyList<MNNHuggingFaceMirror> GetApiCandidates() => Wrap(RuntimePool.GetApiCandidates());
        public IReadOnlyList<MNNHuggingFaceMirror> GetFileCandidates() => Wrap(RuntimePool.GetFileCandidates());
        public IReadOnlyList<MNNHuggingFaceMirror> GetApiProbeCandidates() => Wrap(RuntimePool.GetApiProbeCandidates());
        public void Add(MNNHuggingFaceMirror mirror) => RuntimePool.Add(mirror?.RuntimeSource);
        public void ReportSuccess(MNNHuggingFaceMirror mirror) => RuntimePool.ReportSuccess(mirror?.RuntimeSource);
        public void ReportFailure(MNNHuggingFaceMirror mirror) => RuntimePool.ReportFailure(mirror?.RuntimeSource);
        public Task<int> RefreshFromWebAsync(HttpClient client, IEnumerable<string> indexUrls, CancellationToken cancellationToken = default)
            => RuntimePool.RefreshFromWebAsync(client, indexUrls, cancellationToken);

        private static MNNHuggingFaceMirror[] Wrap(IReadOnlyList<MNNHuggingFaceSource> sources)
            => sources.Select(source => new MNNHuggingFaceMirror(source)).ToArray();
    }
}
