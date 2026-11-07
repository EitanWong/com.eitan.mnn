using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    public enum MNNAcousticModelTask { NoiseReduction, WakeDetection, EnglishWakeDetection, Separation, SpeakerEmbedding, VoiceActivity }

    /// <summary>Discovers structurally complete local acoustic exports, without loading models or claiming inference validation.</summary>
    public static class MNNAcousticModelCatalog
    {
        public sealed class Entry
        {
            public string Directory { get; internal set; }
            public MNNAcousticModelTask Task { get; internal set; }
            public long Bytes { get; internal set; }
        }
        public static IReadOnlyList<Entry> Discover(IEnumerable<string> directories)
        {
            if (directories == null) throw new ArgumentNullException(nameof(directories));
            var results = new List<Entry>();
            foreach (string directory in directories.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                Add(results, directory, MNNAcousticModelTask.NoiseReduction, MNNNoiseSuppressor.ProfileFile(directory), MNNAcousticModelFiles.Denoiser);
                Add(results, directory, MNNAcousticModelTask.WakeDetection, MNNKeywordSpotterConfig.FileName, MNNAcousticModelFiles.Wake);
                Add(results, directory, MNNAcousticModelTask.Separation, MNNSpeechSeparator.ProfileFile(directory), MNNAcousticModelFiles.Separation);
                Add(results, directory, MNNAcousticModelTask.SpeakerEmbedding, MNNSpeakerEncoder.ProfileFile(directory), MNNAcousticModelFiles.Speaker);
                Add(results, directory, MNNAcousticModelTask.VoiceActivity, MNNVoiceActivityDetector.ProfileFile(directory), MNNAcousticModelFiles.Vad);
            }
            return results.AsReadOnly();
        }
        private static void Add(List<Entry> results, string directory, MNNAcousticModelTask task, string profile, Func<string, string[]> readFiles)
        {
            try
            {
                if (profile == null) return;
                string manifest = MNNModelAssetDownloader.ResolvePath(directory, profile);
                if (!File.Exists(manifest) || new FileInfo(manifest).Length == 0) return;
                if (task == MNNAcousticModelTask.WakeDetection && MNNKeywordSpotterConfig.Read(directory).Language == MNNKeywordLanguage.English)
                    task = MNNAcousticModelTask.EnglishWakeDetection;
                var files = readFiles(directory).Select(path => new FileInfo(MNNModelAssetDownloader.ResolvePath(directory, path))).ToArray();
                if (files.Length == 0 || files.Any(file => !file.Exists || file.Length == 0)) return;
                results.Add(new Entry { Directory = directory, Task = task, Bytes = files.Sum(file => file.Length) });
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException ||
                error is NotSupportedException || error is System.Security.SecurityException)
            { }
        }
    }
}
