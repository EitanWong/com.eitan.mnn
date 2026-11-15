using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Immutable shared registration table. New deployments add one entry here.</summary>
    public static class MNNModelRegistry
    {
        private static readonly MNNModelRegistration[] Entries = { MNNHojoTtsModel.Registration };
        public static IReadOnlyList<MNNModelRegistration> All { get; } = Array.AsReadOnly(Entries);
        public static MNNModelRegistration Find(MNNModelSource source, string repository)
            => Entries.FirstOrDefault(entry => entry.Source == source && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase));
        internal static MNNModelRegistration FindSpeech(string directory)
            => string.IsNullOrWhiteSpace(directory) ? null : Entries.FirstOrDefault(entry => entry.Speech != null && File.Exists(Path.Combine(directory, entry.Speech.Marker)));
        internal static MNNModelRegistration FindSpeech(MNNSpeechModelKind kind)
            => Entries.FirstOrDefault(entry => entry.Speech?.Kind == kind);
    }
}
