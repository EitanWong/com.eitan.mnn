using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Published image graph metadata; a null URL requires preparation with the package tooling.</summary>
    public sealed class MNNImageModelSource
    {
        public MNNImageTaskProfile Profile { get; }
        public string Repository { get; }
        public string Url { get; }
        private MNNImageModelSource(MNNImageTaskProfiles.Source source)
        { Profile = source.Profile; Repository = source.Repository; Url = source.Url; }
        /// <summary>Returns a snapshot of supported image export sources. No network requests are made.</summary>
        public static IReadOnlyList<MNNImageModelSource> GetAll() =>
            MNNImageTaskProfiles.All.Select(source => new MNNImageModelSource(source)).ToArray();
    }
}
