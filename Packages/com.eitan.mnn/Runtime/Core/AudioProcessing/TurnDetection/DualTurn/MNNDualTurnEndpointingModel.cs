using System;
using System.IO;

namespace MNN.Unity
{
    internal static class MNNDualTurnEndpointingModel
    {
        internal const string Graph = "model_fp32.mnn";
        internal const string Weights = Graph + ".weight";

        internal static bool IsComplete(string directory)
        {
            try
            {
                if (!IsProfileValid(MNNDualTurnEndpointingConfig.Read(directory))) return false;
                return (Matches(directory, Graph, 620344, "7adbd866711d96dc028efed15e35c27bd9c252c963f4931d24f6f2acfb1e4a11") &&
                    Matches(directory, Weights, 161380012, "57ee670f09318a789157ab9d596850689e4ec77f88ce4716486a0f1d832485cf")) ||
                    (Matches(directory, Graph, 647168, "d8846e630f53e8a2c9fcc9d71f5dbf8f1a0335f8c6d151d05757992974b3e0e9") &&
                    Matches(directory, Weights, 262256300, "e3889903498299efeab92a65e06750cccb0ba2aabff30149bca60a9fb5e9bd65"));
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsProfileValid(MNNDualTurnEndpointingConfig config)
        {
            return IsSourceValid(config) && IsAudioProfileValid(config) &&
                config.audio_context == 7680 && config.kv_frames == 250 && config.transformer_history == 260;
        }

        private static bool IsSourceValid(MNNDualTurnEndpointingConfig config)
        {
            return config.repository == "anyreach-ai/dualturn-endpointing" &&
                config.revision == "c3860ed71210fe0144af35af340fd7a4dec3d2d3" &&
                config.source_sha256 == "6700ea2f919b4c66355b3dafb4f91f2db9dae9e18a302b812730c04340bf819f";
        }

        private static bool IsAudioProfileValid(MNNDualTurnEndpointingConfig config)
        {
            return config.graph == Graph && config.weights == Weights &&
                config.sample_rate == MNNDualTurnEndpointing.SampleRate && config.tick_samples == MNNDualTurnEndpointing.TickSamples;
        }

        private static bool Matches(string directory, string name, long bytes, string expected)
        {
            string path = Path.Combine(directory, name);
            return new FileInfo(path).Length == bytes && MNNModelDeliveryManifest.ComputeSha256(path) == expected;
        }
    }
}
