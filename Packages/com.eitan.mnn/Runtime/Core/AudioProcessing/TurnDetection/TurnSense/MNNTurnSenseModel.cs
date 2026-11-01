using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNTurnSenseModel
    {
        internal const string Graph = "model_fp32.mnn", Tokenizer = "tokenizer.json";
        internal const string GraphHash = "5b7b543d349ebaa2906cb6c21dd690c80f8f81e1ae1846cd1bbc952344b05757";
        internal const string TokenizerHash = "f41789312f1e8ba7c3d23aed1506bafa29e298c0061d9c6bdc002bde3ef620b3";

        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNTurnSenseConfig.Read(directory);
                return c.repository == "latishab/turnsense" && c.revision == "1ddc8f679abf3d9a42a93373b0e709f0c9d7fe63" &&
                    c.source_sha256 == "ef6f383f3b15d3ff1477d690aed80dbfc7915f121f975457feef08b4b02804ba" &&
                    c.graph == Graph && c.tokenizer == Tokenizer && c.prefix == "<|user|> " && c.output == "probabilities" &&
                    c.maximum_tokens == MNNTurnSense.MaximumTokens && c.pad_token_id == 2 && c.end_of_utterance_class == 1 &&
                    Matches(directory, Graph, 540120312, GraphHash) && Matches(directory, Tokenizer, 3522828, TokenizerHash) &&
                    Matches(directory, "tokenizer-unicode.json", 9663, "545939fab00ee08600f19fd23f99662326ca64ffc48ebc010caa37f4285b4fbd");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool Matches(string directory, string name, long bytes, string expected)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
