using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNSegModel
    {
        internal const string Graph = "seg_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNSegConfig.Read(directory);
                if (c.repository != "alibabasglab/log_YGD_gesture_seg_2spk" || c.revision != "9d5b3e567d04abc7190d49325e79101e52a28930" ||
                    c.source_revision != "6b3774dc79c46ae8bed2a4fa5f706f0ac8c75c61" || c.graph != Graph || c.causal ||
                    c.sample_rate != 16000 || c.gesture_fps != 15 || c.gesture_features != 30 || c.minimum_samples != 1067 || c.maximum_samples != 160000 ||
                    c.audio_input != "mixture" || c.gesture_input != "gestures" || c.audio_output != "target" ||
                    c.normalization != "none; synchronized frame-major gesture features") return false;
                using (var stream = File.OpenRead(Path.Combine(directory, Graph)))
                using (var sha = SHA256.Create())
                    return stream.Length == 17876476 && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                        "41d406810dc43fc821c12013962de565a19a8487530e866c5d8f4b734020495a";
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
