using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Immutable shared registration table. New deployments add one entry here.</summary>
    public static class MNNModelRegistry
    {
        private static readonly MNNModelRegistration[] Entries = new[] { MNNHojoTtsModel.Registration }.Concat(MNNTurnModelProfiles.All).ToArray();
        public static IReadOnlyList<MNNModelRegistration> All { get; } = Array.AsReadOnly(Entries);
        public static MNNModelRegistration Find(MNNModelSource source, string repository)
            => Entries.FirstOrDefault(entry => entry.Source == source && string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase));
        /// <summary>Finds a registered turn deployment by its marker, independent of the folder name.</summary>
        public static MNNModelRegistration FindTurn(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return null;
            foreach (var group in Entries.Where(entry => entry.TurnMarker != null).GroupBy(entry => entry.TurnMarker))
            {
                string marker = Path.Combine(directory, group.Key);
                if (!File.Exists(marker)) continue;
                string hash = MNNModelDeliveryManifest.ComputeSha256(marker);
                var match = group.FirstOrDefault(entry => entry.Files().Any(file => file.Path == group.Key && file.Sha256 == hash));
                if (match != null) return match;
            }
            return null;
        }
        internal static MNNModelRegistration FindSpeech(string directory)
            => string.IsNullOrWhiteSpace(directory) ? null : Entries.FirstOrDefault(entry => entry.Speech != null && File.Exists(Path.Combine(directory, entry.Speech.Marker)));
        internal static MNNModelRegistration FindSpeech(MNNSpeechModelKind kind)
            => Entries.FirstOrDefault(entry => entry.Speech?.Kind == kind);
    }
}
