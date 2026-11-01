using System;

namespace MNN.Unity
{
    /// <summary>One file in a remote model repository, including verification and revision metadata.</summary>
    [Serializable]
    public class MNNModelRepositoryFile
    {
        public string Path;
        public string Type;
        public long Size;
        public string Revision;
        public string Sha256;
        public string SourceRepository;
    }
}
