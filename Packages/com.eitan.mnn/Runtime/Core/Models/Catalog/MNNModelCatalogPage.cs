using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>A provider page with the complete continuation across official, tag and name streams.</summary>
    public sealed class MNNModelCatalogPage
    {
        public IReadOnlyList<MNNRemoteModel> Models { get; internal set; }
        public IReadOnlyList<string> Warnings { get; internal set; } = Array.Empty<string>();
        public int NextStream { get; internal set; }
        public int NextPage { get; internal set; }
        public string NextCursor { get; internal set; }
        public bool HasMore { get; internal set; }
    }
}
