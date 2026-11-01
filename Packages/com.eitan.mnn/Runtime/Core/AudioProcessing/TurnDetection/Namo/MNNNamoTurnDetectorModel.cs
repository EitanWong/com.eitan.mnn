using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNNamoTurnDetectorModel
    {
        internal const string Repository = "videosdk-live/Namo-Turn-Detector-v1-Multilingual";
        internal const string Revision = "59245aac1f0a0f170277ebecc32ddaea418083b4";
        internal const string Graph = "namo_tiled_fp16.mnn";
        internal const string Tokenizer = "tokenizer.txt";
        internal const string GraphHash = "f60d5c6c378b706743c7c952e1706662ea2cb22f53550061cba23c56a66056ef";
        internal const long GraphBytes = 616408948;
        internal const string TokenizerHash = "3ab50bc781a6c1ee6c57c1c43b6eba0ba1e38617e866ab127f78167be2c89f22";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNNamoTurnDetectorConfig.Read(directory);
                return config.repository == Repository && config.revision == Revision && config.source == "author-published FP32 ONNX" &&
                    config.graph == Graph && config.tokenizer == Tokenizer && config.maximum_tokens == 8192 &&
                    config.end_of_turn_class == 1 && config.input_ids == "input_ids" && config.attention_mask == "attention_mask" &&
                    config.output == "logits" && Verify(directory, Graph, GraphBytes, GraphHash) &&
                    Verify(directory, Tokenizer, 13511105, TokenizerHash);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Verify(string directory, string file, long size, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, file)))
            using (var sha = SHA256.Create())
                return stream.Length == size && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
