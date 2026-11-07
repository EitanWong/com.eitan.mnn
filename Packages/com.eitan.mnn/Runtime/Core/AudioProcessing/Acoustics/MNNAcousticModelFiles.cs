using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>File hints for the existing pinned loaders. Integrity and graph protocols remain owned by MNN.Unity.</summary>
    public static class MNNAcousticModelFiles
    {
        public static string[] Denoiser(string directory) =>
            MNNAudioProfileFiles.Graphs(directory, MNNNoiseSuppressor.ProfileFile(directory));

        public static string[] Wake(string directory)
        {
            var profile = MNNKeywordSpotterConfig.Read(directory);
            const string encoder = "encoder-epoch-12-avg-2-chunk-16-left-64.mnn";
            string prefix = File.Exists(Path.Combine(directory, encoder)) ? "" : "kws-zipformer-wenetspeech-3.3M-MNN/";
            var files = new[] { prefix + encoder, prefix + "decoder-epoch-12-avg-2-chunk-16-left-64.mnn",
                prefix + "joiner-epoch-12-avg-2-chunk-16-left-64.mnn", prefix + "tokens.txt" };
            return profile.Language == MNNKeywordLanguage.English ? System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(files, new[] { prefix + "bpe.model" })) : files;
        }

        public static string[] Separation(string directory) =>
            MNNAudioProfileFiles.Graphs(directory, MNNSpeechSeparator.ProfileFile(directory));

        public static string[] Speaker(string directory)
        {
            if (MNNSpeakerEncoder.ProfileFile(directory) == MNNVbxConfig.FileName)
                return MNNAudioProfileFiles.Graphs(directory, MNNVbxConfig.FileName);
            var profile = JsonUtility.FromJson<MNNSpeakerModelConfig>(File.ReadAllText(Path.Combine(directory, MNNSpeakerEncoder.ProfileFile(directory) ?? MNNSpeakerModelConfig.FileName)));
            if (profile == null) throw new InvalidDataException("Invalid speaker profile.");
            return Required(profile.graph);
        }

        public static string[] Vad(string directory)
        {
            if (MNNVoiceActivityDetector.ProfileFile(directory) != MNNSileroVadConfig.FileName)
                return MNNAudioProfileFiles.Graphs(directory, MNNVoiceActivityDetector.ProfileFile(directory));
            var profile = MNNSileroVadConfig.Read(directory);
            return Required(profile.repository == "snakers4/silero-vad" ? profile.graph : "silero_vad.mnn");
        }

        private static string[] Required(params string[] paths)
        {
            foreach (string path in paths)
                if (string.IsNullOrWhiteSpace(path) || path.IndexOf('\0') >= 0)
                    throw new InvalidDataException("Model profile must name its required graph files.");
            return paths;
        }
    }
}
