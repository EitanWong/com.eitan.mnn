using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNAvDprnnModel
    {
        internal const string VisualGraph = "av_dprnn_visual_fp32.mnn";
        internal const string AudioGraph = "av_dprnn_audio_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNAvDprnnConfig.Read(directory);
                return config.repository == "alibabasglab/log_VoxCeleb2_lip_dprnn_2spk" &&
                    config.revision == "6a356000cef95fd605ca1dc6619d314bca5acb32" &&
                    config.source_revision == "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" &&
                    config.visual_graph == VisualGraph && config.audio_graph == AudioGraph &&
                    config.visual_input == "frames" && config.visual_output == "visual_features" &&
                    config.audio_input == "mixture" && config.audio_output == "target" &&
                    config.normalization == "(gray/255-0.4161)/0.1688; no audio normalization" &&
                    config.sample_rate == 16000 && config.frames_per_second == 25 && config.face_size == 112 &&
                    config.visual_features == 256 && config.minimum_samples == 640 && config.maximum_samples == 96000 && !config.causal &&
                    Matches(directory, VisualGraph, 50731576, "87c3c9ca27ab2074b7d620d770e31091676d6841f4c249485892c07a561eabf5") &&
                    Matches(directory, AudioGraph, 10815560, "d6608598e57137a77306c0759368e28de6521371b7ff2d6e149af5d4c796e58b");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Matches(string directory, string name, long length, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var sha = SHA256.Create())
                return stream.Length == length && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == hash;
        }
    }
}
