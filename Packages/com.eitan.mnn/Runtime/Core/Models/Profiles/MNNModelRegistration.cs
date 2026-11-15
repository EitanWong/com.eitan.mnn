using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>One deployment contract shared by catalog, installer and task adapters.</summary>
    public sealed class MNNModelRegistration
    {
        private readonly Func<MNNRemoteModel> _metadata;
        private readonly Func<MNNModelRepositoryFile[]> _files;
        private readonly Func<string, bool> _complete;
        public MNNModelSource Source { get; }
        public string Repository { get; }
        public string Revision { get; }
        public string InstallationName { get; }
        internal MNNRegisteredSpeechAdapter Speech { get; }

        internal MNNModelRegistration(MNNModelSource source, string repository, string revision, string installationName,
            Func<MNNRemoteModel> metadata, Func<MNNModelRepositoryFile[]> files, Func<string, bool> complete,
            MNNRegisteredSpeechAdapter speech)
        {
            Source = source; Repository = repository; Revision = revision; InstallationName = installationName;
            _metadata = metadata; _files = files; _complete = complete; Speech = speech;
        }

        public string ResolveRevision(string requested = null)
        {
            if (string.IsNullOrWhiteSpace(requested) || requested == "main" || requested == "master" || requested == Revision) return Revision;
            throw new ArgumentException("This deployment requires its verified release revision: " + Repository, nameof(requested));
        }

        /// <summary>Returns a caller-owned pinned download plan.</summary>
        public MNNModelRepositoryFile[] Files() => _files();
        public bool IsComplete(string directory) => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) && _complete(directory);
        internal void Validate(string directory)
        {
            if (!IsComplete(directory)) throw new InvalidDataException("Incomplete deployment: " + Repository);
        }
        public MNNRemoteModel CreateModel()
        {
            var model = _metadata();
            model.id = model.repositoryId = Repository; model.provider = Source;
            model.modelScopeId = Source == MNNModelSource.ModelScope ? Repository : null;
            model.revision = model.version = Revision; model.name = InstallationName;
            model.isRepository = true; model.runtimeAvailable = true;
            model.fileSize = Files().Sum(file => file.Size);
            model.mnnFiles = Files().Where(file => file.Path.EndsWith(".mnn", StringComparison.Ordinal)).Select(file => file.Path).ToArray();
            return model;
        }
    }
}
