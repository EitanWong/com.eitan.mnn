namespace MNN.Unity.Editor
{
    /// <summary>Compatibility view of a runtime Hugging Face source.</summary>
    public sealed class MNNHuggingFaceMirror
    {
        internal MNNHuggingFaceSource RuntimeSource { get; }
        public string Name => RuntimeSource.Name;
        public string ApiRoot => RuntimeSource.ApiRoot;
        public string FileRoot => RuntimeSource.FileRoot;
        public bool SupportsApi => RuntimeSource.SupportsApi;

        public MNNHuggingFaceMirror(string name, string apiRoot, string fileRoot = null, bool supportsApi = true)
            : this(new MNNHuggingFaceSource(name, apiRoot, fileRoot, supportsApi)) { }

        internal MNNHuggingFaceMirror(MNNHuggingFaceSource source) { RuntimeSource = source; }

        public string GetFileUrl(string repositoryId, string revision, string path)
            => RuntimeSource.GetFileUrl(repositoryId, revision, path);
    }
}
