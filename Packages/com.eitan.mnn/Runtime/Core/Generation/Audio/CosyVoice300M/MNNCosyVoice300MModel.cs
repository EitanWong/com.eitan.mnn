using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    internal static class MNNCosyVoice300MModel
    {
        internal const string ConfigName = "mnn_cosyvoice300m.json";
        internal const string CompactGraph = "fp16/llm_decode_compact.mnn";
        private const string LegacyCompactGraph = "experimental-decode/llm_decode_compact.mnn";
        internal const string QuantizedGraph = "experimental-decode/llm_decode_split_body4out8_b64_nopost_fixedshape.mnn";
        internal static string ConservativeGraph(MNNCosyVoice300MDecoderMode mode)
        {
            switch (mode)
            {
                case MNNCosyVoice300MDecoderMode.ExperimentalInt8: return "experimental-int8/llm_decode_int8.mnn";
                case MNNCosyVoice300MDecoderMode.ExperimentalFfnInt8: return "experimental-int8/llm_decode_ffn_int8.mnn";
                default: return null;
            }
        }
        internal static readonly string[] Stages = {"text_encoder", "llm_cached_projected", "flow_encoder", "flow_regulator", "flow_estimator", "hift_f0", "hift_core"};
        internal static readonly string[] Common = {"llm_embeddings.npz", "projected_positions.npy", "flow_speaker.npz", "hift_source.npz", "prompt.npz", "whisper.tiktoken", "whisper-special-tokens.json"};

        internal static bool HasLayout(string directory) => !string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, ConfigName));
        internal static string ResolveCompactGraph(string directory)
        {
            foreach (string relative in new[] {CompactGraph, LegacyCompactGraph})
            {
                string path = Path.Combine(directory, relative);
                if (File.Exists(path) && File.Exists(path + ".weight")) return path;
            }
            return null;
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                if (!HasLayout(directory)) return false;
                var config = MNNModelData.Read(Path.Combine(directory, ConfigName));
                if (!Equals(MNNModelData.Get(config, "model"), "iic/CosyVoice-300M") ||
                    MNNModelData.Integer(MNNModelData.Get(config, "schema_version")) != 1 ||
                    MNNModelData.Integer(MNNModelData.Get(config, "sample_rate")) != 22050)
                    return false;
                if (MNNModelData.Object(config).TryGetValue("decoder_graph", out var decoderGraph) && (!Equals(decoderGraph, CompactGraph) ||
                    !File.Exists(Path.Combine(directory, CompactGraph)) || !File.Exists(Path.Combine(directory, CompactGraph + ".weight"))))
                    return false;
                return Stages.All(s => File.Exists(Path.Combine(directory, "fp16", s + ".mnn")) && File.Exists(Path.Combine(directory, "fp16", s + ".mnn.weight"))) &&
                    Common.All(s => File.Exists(Path.Combine(directory, "common", s)));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is ArgumentException || e is FormatException)
            {
                return false;
            }
        }
    }
}
