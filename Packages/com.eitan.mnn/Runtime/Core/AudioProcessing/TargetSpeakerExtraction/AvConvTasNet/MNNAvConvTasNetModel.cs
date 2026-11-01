using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNAvConvTasNetModel
    {
        internal const string VisualGraph = "av_convtasnet_visual_fp32.mnn";
        internal const string AudioGraph = "av_convtasnet_audio_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNAvConvTasNetConfig.Read(directory);
                return config.repository == "alibabasglab/log_LRS2_lip_convtasnet_2spk" &&
                    config.revision == "0b05921c9bf896a0cf772ac75521b7a35a671c4f" &&
                    config.source_revision == "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" &&
                    config.visual_graph == VisualGraph && config.audio_graph == AudioGraph &&
                    config.visual_input == "frames" && config.visual_output == "visual_features" &&
                    config.audio_input == "mixture" && config.audio_output == "target" &&
                    config.normalization == "(gray/255-0.4161)/0.1688; no audio normalization" &&
                    config.sample_rate == 16000 && config.frames_per_second == 25 && config.face_size == 112 &&
                    config.visual_features == 256 && config.minimum_samples == 640 && config.maximum_samples == 96000 && !config.causal &&
                    Matches(directory, VisualGraph, 50731584, "7488bda0cf68fffc5280c23aac60b05eccabd8a3b31911ba128e38f681a19103") &&
                    Matches(directory, AudioGraph, 35400952, "8fb05b146cf34c0dfba3d790a5a52074a6662dff7c3cee881e5550f46eb5de59");
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
